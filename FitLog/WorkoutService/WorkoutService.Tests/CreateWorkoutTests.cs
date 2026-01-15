using System.Linq;
using System.Threading.Tasks;
using Xunit;
using WorkoutService.Logic;
using WorkoutService.Models;
using System;

public class CreateWorkoutTests
{
    [Fact]
    public async Task CreateWorkoutAsync_CreatesUserIfMissing_AndSavesWorkout()
    {
        using var db = TestDbContextFactory.Create();

        var publisher = new FakePublisher();
        var logic = new WorkoutServiceLogic(db, publisher);

        var request = new CreateWorkoutRequest
        {
            Date = DateTime.UtcNow.Date,
            DurationMinutes = 45
        };

        var id = await logic.CreateWorkoutAsync("user-a", request);

        Assert.Single(db.Workouts);
        Assert.Equal("user-a", db.Workouts.Single().UserId);
    }

    [Fact]
public async Task CreateWorkoutAsync_Throws_WhenDurationIsZero()
{
    using var db = TestDbContextFactory.Create();

    var publisher = new FakePublisher();
    var logic = new WorkoutServiceLogic(db, publisher);

    var request = new CreateWorkoutRequest
    {
        Date = DateTime.UtcNow.Date,
        DurationMinutes = 0
    };

    await Assert.ThrowsAsync<ArgumentException>(() =>
        logic.CreateWorkoutAsync("user-a", request));
}

[Fact]
public async Task CreateWorkoutAsync_PublishesWorkoutUploadedEvent()
{
    using var db = TestDbContextFactory.Create();

    var publisher = new FakePublisher();
    var logic = new WorkoutServiceLogic(db, publisher);

    var request = new CreateWorkoutRequest
    {
        Date = DateTime.UtcNow.Date,
        DurationMinutes = 45
    };

    var workoutId = await logic.CreateWorkoutAsync("user-a", request);

    Assert.Single(publisher.WorkoutUploadedEvents);

    var evt = publisher.WorkoutUploadedEvents.Single();
    Assert.Equal("user-a", evt.UserId);
    Assert.Equal(workoutId, evt.WorkoutId);
    Assert.Equal(45, evt.DurationMinutes);
}
[Fact]
public async Task CreateWorkoutAsync_ThrowsUnauthorized_WhenUserIsDeleted()
{
    using var db = TestDbContextFactory.Create();

    // Arrange: voeg deleted user toe
    db.Users.Add(new Users { Id = "user-a", IsDeleted = true });
    await db.SaveChangesAsync();

    var publisher = new FakePublisher();
    var logic = new WorkoutServiceLogic(db, publisher);

    var request = new CreateWorkoutRequest
    {
        Date = DateTime.UtcNow.Date,
        DurationMinutes = 30
    };

    await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        logic.CreateWorkoutAsync("user-a", request));
}


}