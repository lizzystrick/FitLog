using Microsoft.AspNetCore.Mvc;
using WorkoutService.Logic;
using WorkoutService.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using WorkoutService.Messaging;
using WorkoutService.Observability;


namespace WorkoutService.Controllers;

[ApiController]
[Route("workouts")]
[Authorize]
public class WorkoutsController : ControllerBase
{
    private readonly WorkoutServiceLogic _logic;
    private readonly Messaging.RabbitMqPublisher _publisher;

    public WorkoutsController(WorkoutServiceLogic logic, Messaging.RabbitMqPublisher publisher)
    {
        _logic = logic;
        _publisher = publisher;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkoutRequest request)
    {
        try
        {
            var userId =
    User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? User.FindFirstValue("sub")
    ?? User.FindFirstValue("userId");

if (string.IsNullOrWhiteSpace(userId))
    return Unauthorized("Missing user id claim");
            var workoutId = await _logic.CreateWorkoutAsync(userId, request);
            return Created($"/workouts/{workoutId}", new WorkoutResponse { WorkoutId = workoutId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId =
    User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? User.FindFirstValue("sub")
    ?? User.FindFirstValue("userId");

if (string.IsNullOrWhiteSpace(userId))
    return Unauthorized("Missing user id claim");
        var workouts = await _logic.GetWorkoutsAsync(userId);
        return Ok(workouts);
    }

 /*   [HttpDelete("me")]
public async Task<IActionResult> DeleteMine()
{
    var userId =
        User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? User.FindFirstValue("userId");

    if (string.IsNullOrWhiteSpace(userId))
        return Unauthorized("Missing user id claim");

    await _logic.DeleteWorkoutsAsync(userId);
    return NoContent();
}*/

[HttpDelete("me")]
public async Task<IActionResult> DeleteMine()
{
    var userId =
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? User.FindFirstValue("userId");

    if (string.IsNullOrWhiteSpace(userId))
        return Unauthorized("Missing user id claim");

        var eventId = Guid.NewGuid();

    // 1) Verwijder workouts direct (GDPR: data weg)
    await _logic.DeleteWorkoutsAsync(userId);

    // 2) Publish event voor andere services (SummaryWorker)
    var evt = new Messaging.UserDeletedEvent(
        EventId: Guid.NewGuid(),
        UserId: userId,
        DeletedAtUtc: DateTime.UtcNow
    );

    _publisher.PublishUserDeleted(evt);
    Metrics.UsersDeleted.Inc();

    return NoContent(); // 204
}
}