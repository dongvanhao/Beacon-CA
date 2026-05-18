namespace Beacon.Application.Features.Posts.Dtos;

public record DailySafetyRecordInPostResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateOnly Date { get; init; }
    public string Status { get; init; } = default!;
    public DateTime DeadlineAtUtc { get; init; }
    public DateTime? CheckedInAtUtc { get; init; }
    public DateTime? MarkedMissedAtUtc { get; init; }
    public DateTime? ReminderSentAtUtc { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public DateTime? LastEvaluatedAtUtc { get; init; }
}
