using Microsoft.EntityFrameworkCore;
using WorkoutService.Data;
using WorkoutService.Messaging;
using WorkoutService.Models;
using WorkoutService.Observability;
namespace WorkoutService.Logic;

public class WorkoutServiceLogic
{
    private readonly WorkoutDbContext _db;
    private readonly IEventPublisher _publisher;

    public WorkoutServiceLogic(WorkoutDbContext db, IEventPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Guid> CreateWorkoutAsync(string userId, CreateWorkoutRequest request)
    {
        if (request.DurationMinutes <= 0)
            throw new ArgumentException("DurationMinutes must be greater than 0.");

var user = await _db.Users.FindAsync(userId);

if (user == null)
{
    _db.Users.Add(new Users { Id = userId, IsDeleted = false });
    await _db.SaveChangesAsync();
}
else if (user.IsDeleted)
{
    throw new UnauthorizedAccessException("User is deleted");
}

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = request.Date,
            DurationMinutes = request.DurationMinutes
        };

        _db.Workouts.Add(workout);
        await _db.SaveChangesAsync();
        Metrics.WorkoutsCreated.Inc();

        _publisher.PublishWorkoutUploaded(new WorkoutUploadedEvent
        {
            WorkoutId = workout.Id,
            UserId = userId,
            DurationMinutes = workout.DurationMinutes,
            OccurredAtUtc = DateTime.UtcNow
        });

        return workout.Id;
    }

    public async Task EnsureUserActiveAsync(string userId)
{
    var user = await _db.Users.FindAsync(userId);
    if (user != null && user.IsDeleted)
        throw new UnauthorizedAccessException("User is deleted");
}

    public async Task<List<Workout>> GetWorkoutsAsync(string userId)
    {
        return await _db.Workouts
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.Date)
            .ToListAsync();
    }

    public async Task DeleteWorkoutsAsync(string userId)
{
    await _db.Workouts
        .Where(w => w.UserId == userId)
        .ExecuteDeleteAsync();
}

}