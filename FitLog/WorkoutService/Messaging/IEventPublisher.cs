namespace WorkoutService.Messaging;

public interface IEventPublisher
{
    void PublishWorkoutUploaded(WorkoutUploadedEvent evt);
    void PublishUserDeleted(UserDeletedEvent evt);
}