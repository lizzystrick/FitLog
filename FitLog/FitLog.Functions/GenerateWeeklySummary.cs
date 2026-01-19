using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;

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
        var mongoUri = Environment.GetEnvironmentVariable("MONGODB_URI");
        var pg = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(mongoUri) || string.IsNullOrWhiteSpace(pg))
        {
            var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
            await bad.WriteStringAsync("Missing env vars");
            return bad;
        }

        var now = DateTime.UtcNow;
var day = (int)now.DayOfWeek;   // Sunday=0
day = day == 0 ? 7 : day;       // Sunday -> 7
var weekStart = now.Date.AddDays(1 - day); // Monday 00:00 UTC
var weekEnd = weekStart.AddDays(7);

        int totalMinutes = 0;
        int workoutCount = 0;
        string userId = "unknown";

        // 2️⃣ SUPERSIMPE Postgres query
        await using var conn = new NpgsqlConnection(pg);
        await conn.OpenAsync();

        var sql = @"
SELECT COUNT(*)::int, COALESCE(SUM(""DurationMinutes""),0)::int
FROM ""Workouts""
WHERE ""Date"" >= @start AND ""Date"" < @end;
";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("start", weekStart);
        cmd.Parameters.AddWithValue("end", weekEnd);

        await using var reader = await cmd.ExecuteReaderAsync();
if (await reader.ReadAsync())
{
    workoutCount = reader.GetInt32(0);
    totalMinutes = reader.GetInt32(1);
    userId = "ALL_USERS";
}

        // 3️⃣ Schrijf naar Mongo
        var mongo = new MongoClient(mongoUri);
        var col = mongo
            .GetDatabase("fitlog_summary")
            .GetCollection<BsonDocument>("weekly_summaries");

        var doc = new BsonDocument
        {
            { "UserId", userId },
            { "WeekStartUtc", weekStart },
            { "WorkoutCount", workoutCount },
            { "TotalMinutes", totalMinutes },
            { "UpdatedAtUtc", DateTime.UtcNow }
        };

        await col.InsertOneAsync(doc);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("Summary generated");
        return ok;
    }
}
