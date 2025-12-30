namespace WorkoutService.Messaging;

public class WorkoutUploadedEvent
{
    public Guid WorkoutId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
}