using Microsoft.EntityFrameworkCore;
using WorkoutService.Data;
using WorkoutService.Messaging;
using WorkoutService.Models;

namespace WorkoutService.Logic;

public class WorkoutServiceLogic
{
    private readonly WorkoutDbContext _db;
    private readonly RabbitMqPublisher _publisher;

    public WorkoutServiceLogic(WorkoutDbContext db, RabbitMqPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Guid> CreateWorkoutAsync(string userId, CreateWorkoutRequest request)
    {
        if (request.DurationMinutes <= 0)
            throw new ArgumentException("DurationMinutes must be greater than 0.");

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = request.Date,
            DurationMinutes = request.DurationMinutes
        };

        _db.Workouts.Add(workout);
        await _db.SaveChangesAsync();

        _publisher.PublishWorkoutUploaded(new WorkoutUploadedEvent
        {
            WorkoutId = workout.Id,
            UserId = userId,
            OccurredAtUtc = DateTime.UtcNow
        });

        return workout.Id;
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