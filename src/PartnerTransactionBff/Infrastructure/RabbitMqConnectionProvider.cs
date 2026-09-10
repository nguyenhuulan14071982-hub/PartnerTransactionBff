using Microsoft.Extensions.Options;
using PartnerTransactionBff.Configuration;
using RabbitMQ.Client;

namespace PartnerTransactionBff.Infrastructure;

public interface IRabbitMqConnectionProvider
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnectionProvider> logger) : IRabbitMqConnectionProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = settings.Host,
                Port = settings.Port,
                UserName = settings.User,
                Password = settings.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            Exception? lastException = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync("partner-transaction-bff", cancellationToken);
                    logger.LogInformation("RabbitMQ connection established to {Host}:{Port}.", settings.Host, settings.Port);
                    return _connection;
                }
                catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException or
                                           TimeoutException)
                {
                    lastException = ex;
                    logger.LogWarning(ex, "RabbitMQ connection attempt {Attempt}/3 failed.", attempt);
                    if (attempt < 3)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                    }
                }
            }

            throw new InvalidOperationException("RabbitMQ is currently unavailable.", lastException);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
