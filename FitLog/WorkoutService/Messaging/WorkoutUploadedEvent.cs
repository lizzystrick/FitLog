namespace WorkoutService.Messaging;

public class WorkoutUploadedEvent
{
    public Guid EventId { get; set; }  
    public Guid WorkoutId { get; set; }
    public string UserId { get; set; } = "";
    public int DurationMinutes { get; set; } 
    public DateTime OccurredAtUtc { get; set; }
}