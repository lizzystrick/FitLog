using System.Collections.Generic;
using WorkoutService.Messaging;

public class FakePublisher : IEventPublisher
{
    public List<WorkoutUploadedEvent> WorkoutUploadedEvents { get; } = new();
    public List<UserDeletedEvent> UserDeletedEvents { get; } = new();

    public void PublishWorkoutUploaded(WorkoutUploadedEvent evt)
    {
        WorkoutUploadedEvents.Add(evt);
    }

    public void PublishUserDeleted(UserDeletedEvent evt)
    {
        UserDeletedEvents.Add(evt);
    }
}