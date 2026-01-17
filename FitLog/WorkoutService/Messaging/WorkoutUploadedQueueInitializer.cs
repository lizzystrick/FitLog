using RabbitMQ.Client;

namespace WorkoutService.Messaging;

public class WorkoutUploadedQueueInitializer : BackgroundService
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IModel? _channel;

    private const string QueueName = "workout.uploaded";

    public WorkoutUploadedQueueInitializer()
    {
        var host = Environment.GetEnvironmentVariable("RabbitMq__Host") ?? "rabbitmq";
        var username = Environment.GetEnvironmentVariable("RabbitMq__Username") ?? "fitlog";
        var password = Environment.GetEnvironmentVariable("RabbitMq__Password") ?? "fitlog_pw";
        var vhost = Environment.GetEnvironmentVariable("RabbitMq__VirtualHost") ?? "/";
        var port = int.TryParse(Environment.GetEnvironmentVariable("RabbitMq__Port"), out var p) ? p : 5672;
        var useTls = bool.TryParse(Environment.GetEnvironmentVariable("RabbitMq__UseTls"), out var tls) && tls;

        _factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            VirtualHost = vhost,
            DispatchConsumersAsync = true,
            Ssl = new SslOption
            {
                Enabled = useTls,
                ServerName = host
            }
        };
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = _factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        Console.WriteLine($"[WorkoutService] Queue ensured: '{QueueName}'");

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Delay(Timeout.Infinite, stoppingToken);

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}