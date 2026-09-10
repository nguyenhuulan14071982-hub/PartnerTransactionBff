using System.Text.Json;
using Microsoft.Extensions.Options;
using PartnerTransactionBff.Configuration;
using PartnerTransactionBff.Contracts;
using RabbitMQ.Client;

namespace PartnerTransactionBff.Infrastructure;

public sealed class RabbitMqTransactionQueue(
    IRabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTransactionQueue> logger) : ITransactionQueue, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(
        PartnerTransactionMessage message,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetOrCreateChannelAsync(cancellationToken);
            var queueName = options.Value.QueueName;
            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.MessageId,
                Type = "partner.transaction.v1"
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Transaction message {MessageId} published to {QueueName}.",
                message.MessageId,
                queueName);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
        await _channel.QueueDeclareAsync(
            options.Value.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _gate.Dispose();
    }
}
