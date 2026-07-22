namespace VibeCore.Models;

public sealed class ScheduledTaskRun
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string HandlerKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long? DurationMilliseconds { get; set; }
    public string? ErrorSummary { get; set; }
}
