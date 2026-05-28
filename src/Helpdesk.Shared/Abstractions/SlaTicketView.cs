namespace Helpdesk.Shared.Abstractions;

public sealed record SlaTicketView(
    Guid Id,
    string Status,
    DateTime SlaDueAt,
    DateTime UpdatedAt,
    DateTime? SlaBreachedAt,
    Guid? AssigneeId,
    DateTime? AutoAssignedAt,
    bool SlaScoreApplied,
    bool SlaExcluded,
    int SlaUnassignedPenaltyCount
);
