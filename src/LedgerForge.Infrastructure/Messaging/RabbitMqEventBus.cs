using System.Text.Json;
using LedgerForge.Application.Abstractions;
using LedgerForge.Domain.Events;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace LedgerForge.Infrastructure.Messaging;

public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchange;
    private readonly ILogger<RabbitMqEventBus> _logger;

    public RabbitMqEventBus(string host, string exchange, ILogger<RabbitMqEventBus> logger)
    {
        _exchange = exchange;
        _logger = logger;
        _connection = new ConnectionFactory { HostName = host }.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true, autoDelete: false);
    }

    public Task PublishAsync(IReadOnlyList<EventEnvelope> events, CancellationToken cancellationToken)
    {
        foreach (var envelope in events)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, envelope.GetType());
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = envelope.EventId.ToString();
            properties.CorrelationId = envelope.CorrelationId;
            _channel.BasicPublish(_exchange, envelope.EventType, properties, payload);
            _logger.LogInformation("Domain event published to RabbitMQ. EventType={EventType} EventId={EventId}", envelope.EventType, envelope.EventId);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}