using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

var factory = new ConnectionFactory
{
    HostName = host,
    DispatchConsumersAsync = true
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(
    queue: "workout.uploaded",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);

Console.WriteLine($"[SummaryWorker] Waiting for messages on 'workout.uploaded' (host={host})");

var consumer = new AsyncEventingBasicConsumer(channel);

consumer.Received += async (_, ea) =>
{
    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"[SummaryWorker] Received: {json}");

    // later: hier bouw je de echte summary-logica
    await Task.Delay(50);

    channel.BasicAck(ea.DeliveryTag, multiple: false);
};

channel.BasicConsume(queue: "workout.uploaded", autoAck: false, consumer: consumer);

await Task.Delay(Timeout.Infinite);
