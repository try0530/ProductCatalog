# Product Catalog Assessment
A small product catalog and ordering service with C#, ASP.NET Core (.NET 10), Entity Framework Core, and SQLite.
The service supports authenticated product lookup, concurrency-safe order creation, request idempotency, 
transactional stock updates, and asynchronous shipment webhook delivery with retry/backoff,

## Technology
- C#
- ASP.NET Core / .NET 10
- Entity Framework Core
- SQLite
- Swagger / OpenAPI
- Docker
- GitHub Actions for Docker validation

## Project Structure
```
ProductCatalog/
├── Background/
├── Controllers/
├── Data/
├── DTOs/
├── Middelware/
├── Models/
├── Services/
├── Program.cs
├── ProductCatalog.csproj
├── appsettings.json
├── Dockerfile
├── .dockerignore
└── README.md
```

## Build and Run with Docker
Build the image from the project root
```
docker build -t submission .
```

Run the container
```
docker run --rm -p 8080:8080 -e API_KEY=test-key submission
```

The API will be available at: http://localhost:8080

Swagger UI will be available at: http://localhost:8080/swagger

All /api/* endpoints require an API key in the following header:
```
X-API-KEY: test-key
```

The API key is configurable through the API_KEY environment variable.
If no environment variable is supplied, the application uses the development default configured in the application.

## Database and Seed Data
The application uses SQLite so the assessment can run as a self-contained service without a separate database container.
The database is intentionally recreated on application startup so every new container starts with the required seed data

## API Endpoints
1. **GET /api/products **
	Returns a paginated list of products

	Supported query parameters
		- page
		- size
		- category

	e.g.
	```
	GET /api/products?page=1&size=10&category=Electronics
	X-API-Key: test-key
	```

2. ** GET /api/products/{id} **
	Returns a single product.
	Returns 404 Not Found when the product does not exist.

3. ** POST /api/orders **
	Creates a new order.
	Required headers:
	```
	X-API-KEY: test-key
	Idempotency-Key: <client-generated-unique-key>
	```

	Example request
	```
	{
	  "customerEmail": "jane@example.com",
	  "items": [
		{
		  "productId": 1,
		  "quantity": 2
		},
		{
		  "productId": 5,
		  "quantity": 1
		}
	  ]
	}
	```
	Responses:
	- 201 Created: a new order was created successfully
	- 200 Ok: the supplied idempotency key already created an order, so the original order is returned.
	- 400 Bad Request: required input is missing or invalid.
	- 404 Not Found: a referenced product does not exist
	- 409 Conflict: one or more products do not have enough stock

4. ** POST /api/orders/{id}/ship **
	Marks an order as SHIPPED and queues webhook delivery
	Example request: 
	```
	{
	  "webhookUrl": "https://client-system.example.com/webhooks/order-shipped"
	}
	```

	Responses:
	- 200 Ok: the order was marked as shipped
	- 404 Not Found: the order does not exist
	- 409 Conflict: the order is not in a shippable state

The API does not wait for webhook delivery. Webhook work is placed on a background queue and processed by a hosted background service.
Failed webhook requests are retired up to three times with a short exponential backoff.
If all attempts fail, the final failure is logged.

## Order Transaction and Concurrency Design
Order creation is handled inside a database transaction so the operation is all or nothing.
For every requested product, stock is deducted using a single conditional database update.
```
UPDATE products
   SET stock_quantity = stock_quantity - @quantity
 WHERE id = @productId
   AND stock_quantity >= @quantity;
```

The stock check and deduction therefore happen atomically at the database level.
The application checks the number of affected rows:
- 1 affected row means the deduction succeeded
- 0 affected rows means insufficient stock, so the order fails with 409 Conflict.

If any item fails, the transaction is rolled back. This prevents both partial orders and partial stock deductions.

Requested quantities are aggregated by product before stock deduction so duplicate product entries in the smae request are evaluated against
their total requested quantity.

Product stock updates are processed in a deterministic product ID order to reduce deadlock risk on relational databases that use row-level locking

## Idempotency Design
POST /api/orders requires a client-supplied Idempotency-Key.

If the key has already been used to successfully create an order, the API returns the original order with 200 OK instead of creating a duplicate.

The implementation uses two layers:

1. An initial lookup provides a fast path for normal client retries.
2. A database UNIQUE constraint on orders.idempotency_key provides the final concurrency-safe guarantee if two requests with the same key arrive at nearly the same time.

This prevents a retry after a timeout or network failure from accidentally creating and charging stock for a second order.

## Shipment Webhook Design
Shipping is also performed with a conditional database update so two concurrent ship requests cannot both transition the same order from CREATED to SHIPPED.

After the order is successfully marked as shipped, a webhook job is queued and the API returns immediately.

The background worker sends a payload in the following form:
```
{
  "orderId": 123,
  "status": "SHIPPED"
}
```

Webhook delivery failures caused by network errors, timeouts, or non-success HTTP responses are retried a small number of times with backoff.
Exhausted retries are logged rather than crashing the original API request.

## Part 1a - Analytical Query With Ranking
The following SQLite query returns, for each product category, the top products by total quantity sold over the trailing 90 days and their rank within the category.
```
WITH product_sales AS (
	SELECT p.category,
		   p.id AS product_id,
		   p.name AS product_name,
		   SUM(oi.quantity) AS total_quantity_solid
	  FROM orders AS o
	 INNER JOIN order_items AS oi
	    ON oi.order_id = o.id
	 INNER JOIN products AS p
	    ON p.id = oi.product_id
	 WHERE o.created_at >= datetime('now', '-90 days')
	 GROUP BY p.category,
			  p.id,
			  p.name
),
ranked_products AS (
	SELECT category,
		   product_name,
		   total_quantity_sold,
		   RANK() OVER (
				PARTITION BY category
				    ORDER BY total_quantity_sold DESC
		   ) AS rank_within_category
	  FROM product_sales
)
SELECT category,
	   product_name,
	   total_quantity_sold
	   rank_within_category
  FROM ranked_products
 WHERE rank_within_category <= 3
 ORDER BY category,
		  rank_within_category,
		  product_name;
```

RANK() preserves equal rankings when multiple products have the same total quantity sold.
As a result, a tie at rank 3 can return more than three rows for a category, which is intentional ranking behavior
