using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkoutService.Data;

public class WorkoutDbContextFactory : IDesignTimeDbContextFactory<WorkoutDbContext>
{
    public WorkoutDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkoutDbContext>();

        // Voor migrations gebruiken we Development connectionstring (simpel)
        var connectionString =
            "Host=localhost;Port=5432;Database=fitlog;Username=fitlog;Password=fitlog_pw";

        optionsBuilder.UseNpgsql(connectionString);

        return new WorkoutDbContext(optionsBuilder.Options);
    }
}