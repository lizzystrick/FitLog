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


}