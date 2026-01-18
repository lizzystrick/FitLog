using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FitLog.Functions;

public class GenerateWeeklySummary
{
    private readonly ILogger _logger;

    public GenerateWeeklySummary(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GenerateWeeklySummary>();
    }

    [Function("GenerateWeeklySummary")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "weekly-summary")] HttpRequestData req)
    {
        
            var userId = "user-e";

    var mongoUri = Environment.GetEnvironmentVariable("MONGODB_URI");
    var dbName = Environment.GetEnvironmentVariable("MONGODB_DB") ?? "fitlog_summary";
    var collectionName = Environment.GetEnvironmentVariable("MONGODB_COLLECTION") ?? "weekly_summaries";

    _logger.LogInformation("Has MONGODB_URI: {HasUri}", !string.IsNullOrWhiteSpace(mongoUri));
    _logger.LogInformation("DB={Db} Collection={Col}", dbName, collectionName);

    if (string.IsNullOrWhiteSpace(mongoUri))
    {
        var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
        await bad.WriteStringAsync("MONGODB_URI ontbreekt. Check local.settings.json -> Values -> MONGODB_URI");
        return bad;
    }

    var client = new MongoClient(mongoUri);
    var db = client.GetDatabase(dbName);
    var col = db.GetCollection<BsonDocument>(collectionName);

    var doc = new BsonDocument
    {
        { "UserId", userId },
        { "WeekStartUtc", DateTime.UtcNow.Date },
        { "TotalMinutes", 120 },
        { "WorkoutCount", 3 },
        { "UpdatedAtUtc", DateTime.UtcNow }
    };

    await col.InsertOneAsync(doc);

    _logger.LogInformation("Inserted summary for {UserId} into Atlas", userId);

    var ok = req.CreateResponse(HttpStatusCode.OK);
    await ok.WriteStringAsync($"Inserted summary for {userId} into {dbName}.{collectionName}");
    return ok;
    }
}
