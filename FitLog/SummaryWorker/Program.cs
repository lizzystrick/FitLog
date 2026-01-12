using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

const string WorkoutUploadedQueue = "workout.uploaded";
const string UserDeletedQueue = "user.deleted";

var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

var factory = new ConnectionFactory
{
    HostName = host,
    DispatchConsumersAsync = true
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(
    queue: WorkoutUploadedQueue,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);

channel.QueueDeclare(
    queue: UserDeletedQueue,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);


Console.WriteLine("[SummaryWorker] Declared queue: workout.uploaded");
Console.WriteLine("[SummaryWorker] Declared queue: user.deleted");
var workoutConsumer = new AsyncEventingBasicConsumer(channel);

workoutConsumer.Received += async (_, ea) =>
{
    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"[SummaryWorker] Received: {json}");

    // later: hier bouw je de echte summary-logica
    await Task.Delay(50);

    channel.BasicAck(ea.DeliveryTag, multiple: false);
};

channel.BasicConsume(queue: "workout.uploaded", autoAck: false, consumer: workoutConsumer);
Console.WriteLine($"[SummaryWorker] Listening on '{WorkoutUploadedQueue}'...");

var userDeletedConsumer = new AsyncEventingBasicConsumer(channel);

userDeletedConsumer.Received += async (_, ea) =>
{
    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"[SummaryWorker] user.deleted received: {json}");

    // NU: no-op, later delete summaries
    await Task.Delay(50);

    channel.BasicAck(ea.DeliveryTag, multiple: false);
};

channel.BasicConsume(queue: UserDeletedQueue, autoAck: false, consumer: userDeletedConsumer);
Console.WriteLine($"[SummaryWorker] Listening on '{UserDeletedQueue}'...");

await Task.Delay(Timeout.Infinite);