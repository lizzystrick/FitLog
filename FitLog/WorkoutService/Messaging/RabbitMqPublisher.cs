using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client;

namespace WorkoutService.Messaging;

public class RabbitMqPublisher : IEventPublisher
{
    private readonly ConnectionFactory _factory;

    public RabbitMqPublisher(string hostName)
    {
        _factory = new ConnectionFactory { HostName = hostName };
    }

    public void PublishWorkoutUploaded(WorkoutUploadedEvent evt)
    {
        using var connection = _factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: "workout.uploaded",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(evt);
        var body = Encoding.UTF8.GetBytes(json);

        var props = channel.CreateBasicProperties();
        props.Persistent = true;

        channel.BasicPublish(
            exchange: "",
            routingKey: "workout.uploaded",
            basicProperties: props,
            body: body);
    }

    public void PublishUserDeleted(UserDeletedEvent evt)
{
    using var connection = _factory.CreateConnection();
    using var channel = connection.CreateModel();

    const string exchange = "fitlog.events";
    const string routingKey = "user.deleted";

    channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Topic, durable: true);

    var json = JsonSerializer.Serialize(evt);
    var body = Encoding.UTF8.GetBytes(json);

    var props = channel.CreateBasicProperties();
    props.Persistent = true;

    channel.BasicPublish(
        exchange: "",
        routingKey: "user.deleted",
        basicProperties: props,
        body: body);
}
}