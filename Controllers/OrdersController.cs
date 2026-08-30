using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;
using ProductCatalog.DTOs;
using ProductCatalog.Services;

namespace ProductCatalog.Controllers
{
    [ApiController]
    [Route("api/orders")]

    public class OrdersController : ControllerBase
    {
        private readonly OrderService _orderService;

        public OrdersController(OrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateOrder([FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromBody] CreateOrderRequest? request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest(
                    new
                    {
                        message = "Idempotency-Key is required."
                    }
                );
            }

            if (idempotencyKey.Length > 100)
            {
                return BadRequest(
                    new
                    {
                        message = "Idempotency-Key must not exceed 100 characters."
                    }
                );
            }

            if (request is null)
            {
                return BadRequest(
                    new
                    {
                        message = "Request body is required."
                    }
                );
            }

            var result = await _orderService.CreateOrderAsync(request, idempotencyKey, cancellationToken);

            return result.Outcome switch
            {
                CreateOrderOutcome.Created => StatusCode(StatusCodes.Status201Created, result.Order),
                CreateOrderOutcome.IdempotentReplay => Ok(result.Order),
                CreateOrderOutcome.BadRequest => BadRequest(new { message = result.Message }),
                CreateOrderOutcome.ProductNotFound => NotFound(new { message = result.Message }),
                CreateOrderOutcome.InsufficientStock => Conflict(new { message = result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        [HttpPost("{id:long}/ship")]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ShipOrder(long id, [FromBody] ShipOrderRequest? request, CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.WebhookUrl))
            {
                return BadRequest(
                    new
                    {
                        message = "webhookUrl is required."
                    }
                );
            }

            if (!Uri.TryCreate(request.WebhookUrl, UriKind.Absolute, out var webhookUri))
            {
                return BadRequest(
                    new
                    {
                        message = "webhookUrl must be a valid absolute URL."
                    }
                );
            }

            if (webhookUri.Scheme != Uri.UriSchemeHttp && webhookUri.Scheme != Uri.UriSchemeHttps)
            {
                return BadRequest(
                    new
                    {
                        message = "webhookUrl must use HTTP or HTTPS."
                    }
                );
            }

            var result = await _orderService.ShipOrderAsync(id, webhookUri, cancellationToken);

            return result.Outcome switch
            {
                ShipOrderOutcome.Shipped => Ok(result.Order),
                ShipOrderOutcome.NotFound => NotFound(new { message = result.Message }),
                ShipOrderOutcome.Conflict => Conflict(new { message = result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };

        }
    }
}
