using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

const string WorkoutUploadedQueue = "workout.uploaded";
const string UserDeletedQueue = "user.deleted";

var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
var mongoConn = Environment.GetEnvironmentVariable("MONGO_CONN") ?? "mongodb://mongodb:27017";
var mongoDbName = Environment.GetEnvironmentVariable("MONGO_DB") ?? "fitlog_summary";
BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));
// Mongo
var mongoClient = new MongoClient(mongoConn);
var db = mongoClient.GetDatabase(mongoDbName);
var weeklySummaries = db.GetCollection<WeeklySummary>("weekly_summaries");
var processedEvents = db.GetCollection<ProcessedEvent>("processed_events");

// Indexes (idempotency + unique weekly doc)
await EnsureIndexesAsync(processedEvents, weeklySummaries);
Console.WriteLine("[SummaryWorker] Mongo indexes ensured.");

// RabbitMQ
var factory = new ConnectionFactory
{
    HostName = rabbitHost,
    DispatchConsumersAsync = true
};

// Retry connect (zodat container niet meteen crasht)
IConnection? connection = null;
for (var attempt = 1; attempt <= 20; attempt++)
{
    try
    {
        connection = factory.CreateConnection();
        Console.WriteLine("[SummaryWorker] Connected to RabbitMQ.");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SummaryWorker] RabbitMQ not ready (attempt {attempt}/20): {ex.Message}");
        await Task.Delay(2000);
    }
}
if (connection == null) throw new Exception("Could not connect to RabbitMQ after retries.");

using (connection)
using (var channel = connection.CreateModel())
{
    channel.QueueDeclare(queue: WorkoutUploadedQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
    channel.QueueDeclare(queue: UserDeletedQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);

    Console.WriteLine("[SummaryWorker] Queues declared.");
    Console.WriteLine($"[SummaryWorker] Listening on '{WorkoutUploadedQueue}' and '{UserDeletedQueue}'...");

    var workoutConsumer = new AsyncEventingBasicConsumer(channel);
    workoutConsumer.Received += async (_, ea) =>
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<WorkoutUploadedEvent>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (evt == null || string.IsNullOrWhiteSpace(evt.UserId))
            {
                Console.WriteLine("[SummaryWorker] Invalid WorkoutUploadedEvent payload.");
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            await HandleWorkoutUploadedAsync(evt, processedEvents, weeklySummaries);
            channel.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SummaryWorker] ERROR workout.uploaded: {ex.Message}");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    };

    channel.BasicConsume(queue: WorkoutUploadedQueue, autoAck: false, consumer: workoutConsumer);

    var userDeletedConsumer = new AsyncEventingBasicConsumer(channel);
    userDeletedConsumer.Received += async (_, ea) =>
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<UserDeletedEvent>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (evt == null || string.IsNullOrWhiteSpace(evt.UserId))
            {
                Console.WriteLine("[SummaryWorker] Invalid UserDeletedEvent payload.");
                channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            await HandleUserDeletedAsync(evt, weeklySummaries);
            channel.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SummaryWorker] ERROR user.deleted: {ex.Message}");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    };

    channel.BasicConsume(queue: UserDeletedQueue, autoAck: false, consumer: userDeletedConsumer);

    await Task.Delay(Timeout.Infinite);
}

// ---------------- helpers ----------------

static async Task EnsureIndexesAsync(
    IMongoCollection<ProcessedEvent> processedEvents,
    IMongoCollection<WeeklySummary> weeklySummaries)
{
    var processedIndex = new CreateIndexModel<ProcessedEvent>(
        Builders<ProcessedEvent>.IndexKeys.Ascending(x => x.EventId),
        new CreateIndexOptions { Unique = true }
    );
    await processedEvents.Indexes.CreateOneAsync(processedIndex);

    var weeklyIndex = new CreateIndexModel<WeeklySummary>(
        Builders<WeeklySummary>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.WeekStartUtc),
        new CreateIndexOptions { Unique = true }
    );
    await weeklySummaries.Indexes.CreateOneAsync(weeklyIndex);
}

static DateTime GetWeekStartUtc(DateTime occurredAtUtc)
{
    var d = occurredAtUtc.Date;
    var dow = (int)d.DayOfWeek; // Sunday=0
    var daysSinceMonday = dow == 0 ? 6 : dow - 1;
    var monday = d.AddDays(-daysSinceMonday);
    return DateTime.SpecifyKind(monday, DateTimeKind.Utc);
}

static async Task HandleWorkoutUploadedAsync(
    WorkoutUploadedEvent evt,
    IMongoCollection<ProcessedEvent> processedEvents,
    IMongoCollection<WeeklySummary> weeklySummaries)
{
    // idempotency
    try
    {
        await processedEvents.InsertOneAsync(new ProcessedEvent
        {
            EventId = evt.EventId,
            ProcessedAtUtc = DateTime.UtcNow
        });
    }
    catch (MongoWriteException mwx) when (mwx.WriteError.Category == ServerErrorCategory.DuplicateKey)
    {
        Console.WriteLine($"[SummaryWorker] Duplicate event skipped: {evt.EventId}");
        return;
    }

    var weekStart = GetWeekStartUtc(evt.OccurredAtUtc);

    var filter = Builders<WeeklySummary>.Filter.Where(x =>
        x.UserId == evt.UserId && x.WeekStartUtc == weekStart);

    var update = Builders<WeeklySummary>.Update
        .Inc(x => x.WorkoutCount, 1)
        .Inc(x => x.TotalMinutes, evt.DurationMinutes)
        .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
        .SetOnInsert(x => x.UserId, evt.UserId)
        .SetOnInsert(x => x.WeekStartUtc, weekStart);

    await weeklySummaries.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });

    Console.WriteLine($"[SummaryWorker] Updated summary user={evt.UserId} week={weekStart:yyyy-MM-dd} +{evt.DurationMinutes}min");
}

static async Task HandleUserDeletedAsync(
    UserDeletedEvent evt,
    IMongoCollection<WeeklySummary> weeklySummaries)
{
    var delFilter = Builders<WeeklySummary>.Filter.Eq(x => x.UserId, evt.UserId);
    var result = await weeklySummaries.DeleteManyAsync(delFilter);
    Console.WriteLine($"[SummaryWorker] Deleted {result.DeletedCount} summary docs for user {evt.UserId}");
}

// ---------------- models ----------------

public class WorkoutUploadedEvent
{
    public Guid EventId { get; set; }
    public Guid WorkoutId { get; set; }
    public string UserId { get; set; } = "";
    public int DurationMinutes { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public class UserDeletedEvent
{
    public Guid EventId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime DeletedAtUtc { get; set; }
}

public class ProcessedEvent
{
    public ObjectId Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}

public class WeeklySummary
{
    public ObjectId Id { get; set; }
    public string UserId { get; set; } = "";
    public DateTime WeekStartUtc { get; set; }
    public int WorkoutCount { get; set; }
    public int TotalMinutes { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}