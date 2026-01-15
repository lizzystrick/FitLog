using Prometheus;

namespace WorkoutService.Observability;

public static class Metrics
{
    public static readonly Counter WorkoutsCreated =
        Prometheus.Metrics.CreateCounter("fitlog_workouts_created_total", "Total workouts created");

    public static readonly Counter UsersDeleted =
        Prometheus.Metrics.CreateCounter("fitlog_users_deleted_total", "Total users deleted");
}