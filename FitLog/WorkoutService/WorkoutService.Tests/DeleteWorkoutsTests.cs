using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using WorkoutService.Logic;
using WorkoutService.Models;
using WorkoutService.Messaging;

public class DeleteWorkoutsTests
{
    [Fact]
    public async Task CreateWorkoutAsync_WhenUserIsDeleted_ThrowsUnauthorizedAccessException()
    {
        using var db = TestDbContextFactory.Create();

        db.Users.Add(new Users { Id = "user-deleted", IsDeleted = true });
        await db.SaveChangesAsync();

        var publisher = new FakePublisher();
        var logic = new WorkoutServiceLogic(db, publisher);

        var request = new CreateWorkoutRequest
        {
            Date = DateTime.UtcNow.Date,
            DurationMinutes = 30
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            logic.CreateWorkoutAsync("user-deleted", request));
    }

}