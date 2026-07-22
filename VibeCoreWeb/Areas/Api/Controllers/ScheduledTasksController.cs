using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VibeCore.Scheduling;
using VibeCore.Security;

namespace VibeCore.Areas.Api.Controllers;

[ApiController]
[Route("api/scheduled-tasks")]
[Authorize(Policy = AppPolicies.Reader)]
public sealed class ScheduledTasksController(ScheduledTaskService scheduledTasks) : ControllerBase
{
    [HttpGet("handlers")]
    public ActionResult<IReadOnlyCollection<ScheduledTaskHandlerDto>> GetHandlers() =>
        Ok(scheduledTasks.GetHandlers());

    [HttpGet("schedules")]
    public async Task<ActionResult<IReadOnlyCollection<ScheduledTaskDto>>> GetSchedules(
        CancellationToken ct) => Ok(await scheduledTasks.GetSchedulesAsync(ct));

    [HttpGet("schedules/{id:guid}")]
    public async Task<ActionResult<ScheduledTaskDto>> GetSchedule(Guid id, CancellationToken ct)
    {
        var schedule = await scheduledTasks.GetScheduleAsync(id, ct);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [HttpPost("schedules")]
    [Authorize(Policy = AppPolicies.Operator)]
    public async Task<ActionResult<ScheduledTaskDto>> Create(
        SaveScheduledTaskRequest request,
        CancellationToken ct)
    {
        try
        {
            var schedule = await scheduledTasks.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, schedule);
        }
        catch (ScheduledTaskValidationException ex)
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["schedule"] = [ex.Message] }));
        }
    }

    [HttpPut("schedules/{id:guid}")]
    [Authorize(Policy = AppPolicies.Operator)]
    public async Task<ActionResult<ScheduledTaskDto>> Update(
        Guid id,
        SaveScheduledTaskRequest request,
        CancellationToken ct)
    {
        try
        {
            var schedule = await scheduledTasks.UpdateAsync(id, request, ct);
            return schedule is null ? NotFound() : Ok(schedule);
        }
        catch (ScheduledTaskValidationException ex)
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["schedule"] = [ex.Message] }));
        }
    }

    [HttpPost("schedules/{id:guid}/pause")]
    [Authorize(Policy = AppPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct) =>
        await scheduledTasks.PauseAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("schedules/{id:guid}/resume")]
    [Authorize(Policy = AppPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct) =>
        await scheduledTasks.ResumeAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("schedules/{id:guid}/run")]
    [Authorize(Policy = AppPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken ct) =>
        await scheduledTasks.RunNowAsync(id, ct) ? Accepted() : NotFound();

    [HttpDelete("schedules/{id:guid}")]
    [Authorize(Policy = AppPolicies.Operator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await scheduledTasks.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("schedules/{id:guid}/runs")]
    public async Task<ActionResult<IReadOnlyCollection<ScheduledTaskRunDto>>> GetRuns(
        Guid id,
        CancellationToken ct) => Ok(await scheduledTasks.GetRunsAsync(id, ct));
}
