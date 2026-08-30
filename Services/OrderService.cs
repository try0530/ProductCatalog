using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Background;
using ProductCatalog.Data;
using ProductCatalog.DTOs;
using ProductCatalog.Models;

namespace ProductCatalog.Services
{
    public sealed class OrderService
    {
        private readonly AppDbContext _dbContext;

        private readonly IWebhookQueue _webhookQueue;

        public OrderService(
            AppDbContext dbContext,
            IWebhookQueue webhookQueue)
        {
            _dbContext = dbContext;
            _webhookQueue = webhookQueue;
        }

        public async Task<CreateOrderResult> CreateOrderAsync(
            CreateOrderRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            // Fast path idempotency check.
            // If this key has already successfully created an order,
            // return that original order before processing the request again
            var existsingOrder = await FindOrderByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

            if (existsingOrder is not null)
            {
                return new CreateOrderResult(
                    CreateOrderOutcome.IdempotentReplay,
                    MapOrder(existsingOrder),
                    null
                );
            }

            // request validation
            if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            {
                return new CreateOrderResult(
                    CreateOrderOutcome.BadRequest,
                    null,
                    "Customer Email must be present and non-blank."
                );
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return new CreateOrderResult(
                    CreateOrderOutcome.BadRequest,
                    null,
                    "Items must contain at least one item."
                );
            }

            if (request.Items.Any(item => item.Quantity <= 0))
            {
                return new CreateOrderResult(
                    CreateOrderOutcome.BadRequest,
                    null,
                    "Each item quantity must a positive interger."
                );
            }

            // Get all unique product IDs from the order
            var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();

            // Load product information
            var products = await _dbContext.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id, cancellationToken);

            // Check whether every requested product exists
            var missingProductIds = productIds.Where(productId => !products.ContainsKey(productId)).ToList();

            if (missingProductIds.Count > 0)
            {
                return new CreateOrderResult(
                    CreateOrderOutcome.ProductNotFound,
                    null,
                    $"Product {missingProductIds[0]} was not found.");
            }


            // Aggregate quantities by product ID
            // Ensures the stock check considers the total requested quantity for each product.
            var requestedQuantities = request.Items.GroupBy(item => item.ProductId).ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Create the Order first
                var order = new Order
                {
                    CustomerEmail = request.CustomerEmail.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    Status = OrderStatuses.Created,
                    IdempotencyKey = idempotencyKey
                };

                _dbContext.Orders.Add(order);

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Perform stock update in deterministic product ID order.
                foreach (var requestedProduct in requestedQuantities.OrderBy(entry => entry.Key))
                {
                    var productId = requestedProduct.Key;

                    var quantity = requestedProduct.Value;

                    // one conditional UPDATE statement
                    //The stock check and stock deduction happen atomically at the database level.
                    var affectedRows = await _dbContext.Products
                        .Where(product => product.Id == productId && product.StockQuantity >= quantity)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(
                                product => product.StockQuantity, 
                                product => product.StockQuantity - quantity
                            ), 
                            cancellationToken
                        );

                    // Zero rows updated means the product did not have enough stock at the exact moment the UPDATE executed
                    if (affectedRows == 0)
                    {
                        await transaction.RollbackAsync(
                            CancellationToken.None
                        );

                        _dbContext.ChangeTracker.Clear();

                        return new CreateOrderResult(
                            CreateOrderOutcome.InsufficientStock,
                            null,
                            $"Product {productId} does not have enough stock."
                        );
                    }
                }

                // All stock deductions succeeded
                // Now create the order item records.
                foreach (var requestedItem in request.Items)
                {
                    var product = products[requestedItem.ProductId];

                    order.Items.Add(
                        new OrderItem
                        {
                            ProductId = requestedItem.ProductId,
                            Quantity = requestedItem.Quantity,
                            UnitPrice = product.Price
                        }
                    );
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Only commit after
                // Order exists, Every stock deduction succeeded, Every OrderItem was created.
                await transaction.CommitAsync(cancellationToken);

                return new CreateOrderResult(
                    CreateOrderOutcome.Created,
                    MapOrder(order),
                    null
                );
            }
            catch (DbUpdateException)
            {
                // Concurrent inserting the same IdempotencyKey first
                // Roll back transaction, then check whether another successful order now owns that key.
                await transaction.RollbackAsync(CancellationToken.None);

                _dbContext.ChangeTracker.Clear();

                var concurrentlyCreatedOrder = await FindOrderByIdempotencyKeyAsync(
                    idempotencyKey,
                    CancellationToken.None
                );

                if (concurrentlyCreatedOrder is not null)
                {
                    return new CreateOrderResult(
                        CreateOrderOutcome.IdempotentReplay,
                        MapOrder(concurrentlyCreatedOrder),
                        null
                    );
                }

                // If there is still no order with the key, then the DbUpdateException was caused by something else.
                throw;
            }
            catch
            {
                await transaction.RollbackAsync(
                    CancellationToken.None
                );

                _dbContext.ChangeTracker.Clear();

                throw;
            }
        }

        public async Task<ShipOrderResult> ShipOrderAsync(long orderId, Uri webhookUrl, CancellationToken cancellationToken = default)
        {
            // Update only an order that is currently CREATED.
            var affectedRows = await _dbContext.Orders
                .Where(order => order.Id == orderId && order.Status == OrderStatuses.Created)
                .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Status, OrderStatuses.Shipped), cancellationToken);
            
            if (affectedRows == 0)
            {
                var currentStatus = await _dbContext.Orders.AsNoTracking()
                    .Where(order => order.Id == orderId)
                    .Select(order => order.Status)
                    .FirstOrDefaultAsync(cancellationToken);

                if (currentStatus is null)
                {
                    return new ShipOrderResult(
                        ShipOrderOutcome.Conflict,
                        null,
                        $"Order {orderId} cannot be shipped because its current status {currentStatus}."
                    );
                }
            }

            // Queue the webhook instead of delivering it inside the HTTP request.
            await _webhookQueue.QueueAsync(
                new WebhookDeliveryJob(
                    orderId,
                    webhookUrl
                ),
                CancellationToken.None
            );

            var shippedOrder = await _dbContext.Orders.AsNoTracking()
                .Include(order => order.Items)
                .FirstAsync(order => order.Id == orderId, cancellationToken);

            return new ShipOrderResult(
                ShipOrderOutcome.Shipped,
                MapOrder(shippedOrder),
                null
            );
        }

        private async Task<Order?> FindOrderByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
        {
            return await _dbContext.Orders.AsNoTracking()
                .Include(order => order.Items)
                .FirstOrDefaultAsync(order => order.IdempotencyKey == idempotencyKey, cancellationToken);
        }

        private static OrderResponse MapOrder(Order order)
        {
            return new OrderResponse
            {
                Id = order.Id,
                CustomerEmail = order.CustomerEmail,
                CreatedAt = order.CreatedAt,
                Status = order.Status,
                Items = order.Items
                    .OrderBy(item => item.Id)
                    .Select(item =>
                        new OrderItemResponse
                        {
                            Id = item.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice
                        })
                    .ToList()
            };
        }
    }

    public enum CreateOrderOutcome
    {
        Created,
        IdempotentReplay,
        BadRequest,
        ProductNotFound,
        InsufficientStock
    }

    public sealed record CreateOrderResult(CreateOrderOutcome Outcome, OrderResponse? Order, string? Message);

    public enum ShipOrderOutcome
    {
        Shipped,
        NotFound,
        Conflict
    }

    public sealed record ShipOrderResult(ShipOrderOutcome Outcome, OrderResponse? Order, string? Message);
}
