namespace WorkoutService.Models;

public class Users
{
    public string Id { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}