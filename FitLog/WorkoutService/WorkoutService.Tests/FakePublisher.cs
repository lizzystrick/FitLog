using WorkoutService.Messaging;

public class FakePublisher : IEventPublisher
{
    public void PublishWorkoutUploaded(WorkoutUploadedEvent evt)
    {
        // no-op
    }

    public void PublishUserDeleted(UserDeletedEvent evt)
    {
        // no-op
    }
}