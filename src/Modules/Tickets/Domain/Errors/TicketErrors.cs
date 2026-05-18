using Helpdesk.Shared.Errors;

namespace Helpdesk.Modules.Tickets.Domain.Errors;

public static class TicketErrors
{
    public static readonly Error CannotAssumeInProgress =
        new("ticket.cannot_assume_in_progress", "Ticket is already in progress.");

    public static readonly Error CannotAssumeFromFinalState =
        new("ticket.cannot_assume_final_state", "Ticket is in a final state and cannot be assumed.");

    public static readonly Error CannotResolveNotInProgress =
        new("ticket.cannot_resolve_not_in_progress", "Ticket must be In Progress to be resolved.");

    public static readonly Error CannotResolveFromFinalState =
        new("ticket.cannot_resolve_final_state", "Ticket is already in a final state.");

    public static readonly Error ResolutionRequiresAssignee =
        new("ticket.resolution_requires_assignee", "Cannot resolve a ticket without an assignee.");

    public static readonly Error ResolutionRequiresActor =
        new("ticket.resolution_requires_actor", "Only the current assignee can resolve this ticket.");

    public static readonly Error ResolutionDescriptionRequired =
        new("ticket.resolution_description_required", "A resolution description is required.");

    public static readonly Error CannotCancelFinalState =
        new("ticket.cannot_cancel_final_state", "Ticket is already in a final state and cannot be cancelled.");

    public static readonly Error ManagerCancellationRequiresReason =
        new("ticket.manager_cancellation_requires_reason", "A reason is required when a Manager cancels a ticket.");

    public static readonly Error TransferNotAllowedInCurrentState =
        new("ticket.transfer_not_allowed", "Transfer is only allowed when the ticket is In Progress.");

    public static readonly Error TransferRequiresAssignee =
        new("ticket.transfer_requires_actor", "Only the current assignee can transfer this ticket.");

    public static readonly Error PriorityChangeOnlyInProgress =
        new("ticket.priority_change_only_in_progress", "Priority can only be changed when the ticket is In Progress.");

    public static readonly Error PriorityChangerMustBeAssignee =
        new("ticket.priority_changer_must_be_assignee", "Only the current assignee can change the priority.");

    public static readonly Error MaxPriorityChangesReached =
        new("ticket.max_priority_changes_reached", "Priority has already been changed the maximum number of times (3).");
}
