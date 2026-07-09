namespace Luff.Server.Features;

public sealed class NotificationDispatcher : BackgroundService, INotificationDispatcher
{
    private readonly Channel<NotificationDelivery> _queue =
        Channel.CreateUnbounded<NotificationDelivery>(new UnboundedChannelOptions { SingleReader = true });

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(IHttpClientFactory httpClientFactory, ILogger<NotificationDispatcher> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Enqueue(NotificationDelivery delivery)
    {
        _queue.Writer.TryWrite(delivery);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var delivery in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("notifications");
                using var content = new StringContent(delivery.Body, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(delivery.Url, content, stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Notification to {Endpoint} returned {Status}",
                        Redact(delivery.Url), (int)response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A down endpoint must never take anything else with it.
                _logger.LogWarning(exception, "Notification delivery to {Endpoint} failed", Redact(delivery.Url));
            }
        }
    }

    // Webhook URLs embed a secret token, so only the scheme + host is safe to log.
    private static string Redact(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? $"{uri.Scheme}://{uri.Host}" : "(invalid url)";
    }
}
