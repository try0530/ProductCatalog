/*
 * POST webhook
 *      if failed: wait -> retry -> max 3 times -> if still fail, log error
 */
using ProductCatalog.Models;

namespace ProductCatalog.Background
{
    public class WebhookBackgroundService : BackgroundService
    {
        private const int MaxAttempts = 3;

        private readonly IWebhookQueue _webhookQueue;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly ILogger<WebhookBackgroundService> _logger;

        public WebhookBackgroundService(
            IWebhookQueue webhookQueue,
            IHttpClientFactory httpClientFactory,
            ILogger<WebhookBackgroundService> looger)
        {
            _webhookQueue = webhookQueue;
            _httpClientFactory = httpClientFactory;
            _logger = looger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await foreach(
                var job in _webhookQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await DeliverWebhookAsync(
                        job,
                        stoppingToken
                    );
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(
                        e,
                        "Unexpected error while processing webhook for order {OrderId}.",
                        job.OrderId
                    );
                }
            }
        }

        private async Task DeliverWebhookAsync(
            WebhookDeliveryJob job,
            CancellationToken stoppingToken)
        {
            var client = _httpClientFactory.CreateClient("WebhookClient");

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    var payload = new WebhookPayload(job.OrderId, OrderStatuses.Shipped);

                    using var response = await client.PostAsJsonAsync(job.WebhookUrl, payload, stoppingToken);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation(
                            "Webhook delivered successfully for order {OrderId} on attempt {Attempt}.",
                            job.OrderId,
                            attempt
                        );

                        return;
                    }

                    _logger.LogWarning(
                        "Webhook delivery failed for order {OrderId} on attemp {Attempt}. HTTP status: {StatusCode}.",
                        job.OrderId,
                        attempt,
                        (int)response.StatusCode
                    );
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    _logger.LogWarning(
                        e,
                        "Webhook delivery failed for order {OrderId} on attempt {Attempt}.",
                        job.OrderId,
                        attempt
                    );
                }

                if (attempt < MaxAttempts)
                {
                    var delay = TimeSpan.FromMicroseconds(500 * Math.Pow(2, attempt - 1));

                    await Task.Delay(delay, stoppingToken);
                }
            }

            _logger.LogError(
                "Webhook delivery permanently failed for order {OrderId} after {MaxAttempts}. Webhook URL: {WebhookUrl}.",
                job.OrderId,
                MaxAttempts,
                job.WebhookUrl
            );
        }

        private sealed record WebhookPayload(
            long OrderId,
            string Status
        );
    }
}
