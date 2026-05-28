namespace Helpdesk.Shared.Abstractions;

public interface ISlaTicketQueryService
{
    Task<IReadOnlyList<SlaTicketView>> GetForFinalScoringAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SlaTicketView>> GetBreachedActiveAsync(DateTime now, CancellationToken ct = default);
    Task<IReadOnlyList<SlaTicketView>> GetUnassignedBreachedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SlaTicketView>> GetCandidatesForAutoAssignAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SlaTicketView>> GetTimedOutAsync(DateTime cutoff, CancellationToken ct = default);
}
