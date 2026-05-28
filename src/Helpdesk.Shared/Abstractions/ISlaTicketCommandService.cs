namespace Helpdesk.Shared.Abstractions;

public interface ISlaTicketCommandService
{
    Task MarkSlaBreachedAsync(Guid ticketId, DateTime now, CancellationToken ct = default);
    Task MarkSlaScoreAppliedAsync(Guid ticketId, CancellationToken ct = default);
    Task IncrementUnassignedPenaltyAsync(Guid ticketId, int count, CancellationToken ct = default);
    Task AutoAssignAsync(Guid ticketId, Guid managerId, string criteria, DateTime now, CancellationToken ct = default);
    Task AutoCancelAsync(Guid ticketId, string reason, DateTime now, CancellationToken ct = default);
}
