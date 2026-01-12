using Microsoft.EntityFrameworkCore;
using WorkoutService.Data;

public static class TestDbContextFactory
{
    public static WorkoutDbContext Create()
    {
        var options = new DbContextOptionsBuilder<WorkoutDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new WorkoutDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }
}