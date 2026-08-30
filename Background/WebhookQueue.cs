using System.Threading.Channels;

namespace ProductCatalog.Background
{
    public sealed record WebhookDeliveryJob(
        long OrderId,
        Uri WebhookUrl
    );

    public interface IWebhookQueue
    {
        ValueTask QueueAsync(
            WebhookDeliveryJob job,
            CancellationToken cancellationToken = default
        );

        IAsyncEnumerable<WebhookDeliveryJob> ReadAllAsync(
            CancellationToken cancellationToken = default
        );
    }

    public sealed class WebhookQueue : IWebhookQueue
    {
        private readonly Channel<WebhookDeliveryJob> _channel;

        public WebhookQueue()
        {
            _channel = Channel.CreateUnbounded<WebhookDeliveryJob>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                }
            );
        }

        public ValueTask QueueAsync(
            WebhookDeliveryJob job,
            CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(
                job,
                cancellationToken
            );
        }

        public IAsyncEnumerable<WebhookDeliveryJob> ReadAllAsync(
            CancellationToken cancellationToken = default)
        {
            return _channel.Reader.ReadAllAsync(
                cancellationToken
            );
        }
    }
}
