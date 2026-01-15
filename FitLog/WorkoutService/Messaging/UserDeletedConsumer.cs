using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WorkoutService.Logic;

namespace WorkoutService.Messaging;

public class UserDeletedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IModel? _channel;

    private const string QueueName = "user.deleted";

    public UserDeletedConsumer(IServiceProvider serviceProvider, IConfiguration config)
    {
        _serviceProvider = serviceProvider;

        // Zelfde config style als je publisher:
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST")
           ?? config["RabbitMq:Host"]
           ?? "rabbitmq";
        _factory = new ConnectionFactory
        {
            HostName = host,
            DispatchConsumersAsync = true
        };
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
{
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            Console.WriteLine($"[WorkoutService] RabbitMQ not ready (attempt {attempt}/{maxAttempts}): {ex.Message}");
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    if (_channel is null)
        throw new InvalidOperationException("[WorkoutService] RabbitMQ channel could not be initialized.");

    _channel.QueueDeclare(
        queue: QueueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null);

    _channel.BasicQos(0, 1, false);

    Console.WriteLine("[WorkoutService] UserDeletedConsumer listening on 'user.deleted'...");

    await base.StartAsync(cancellationToken);
}

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel not initialized.");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (_, ea) =>
        {
    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"[WorkoutService] RAW message on user.deleted: {json}");

    try
    {
        var evt = JsonSerializer.Deserialize<UserDeletedEvent>(json);

        if (evt is null)
        {
            Console.WriteLine("[WorkoutService] Deserialization returned NULL -> ack");
            _channel!.BasicAck(ea.DeliveryTag, false);
            return;
        }

        Console.WriteLine($"[WorkoutService] Parsed event: userId='{evt.UserId}', eventId='{evt.EventId}'");

        if (string.IsNullOrWhiteSpace(evt.UserId))
        {
            Console.WriteLine("[WorkoutService] Missing UserId in event -> ack");
            _channel!.BasicAck(ea.DeliveryTag, false);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var logic = scope.ServiceProvider.GetRequiredService<WorkoutServiceLogic>();

        Console.WriteLine($"[WorkoutService] Calling DeleteWorkoutsAsync for userId='{evt.UserId}'...");
        await logic.DeleteWorkoutsAsync(evt.UserId);
        Console.WriteLine($"[WorkoutService] DeleteWorkoutsAsync finished for userId='{evt.UserId}'");

        _channel!.BasicAck(ea.DeliveryTag, false);
        Console.WriteLine("[WorkoutService] ACK sent");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WorkoutService] ERROR: {ex}");
        _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        Console.WriteLine("[WorkoutService] NACK sent (requeue=true)");
    }
        };

        _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

// BELANGRIJK: laat de BackgroundService draaien zolang de app draait
return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}