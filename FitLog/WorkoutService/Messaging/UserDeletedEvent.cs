namespace WorkoutService.Messaging;

public record UserDeletedEvent(
    Guid EventId,
    string UserId,
    DateTime DeletedAtUtc
);