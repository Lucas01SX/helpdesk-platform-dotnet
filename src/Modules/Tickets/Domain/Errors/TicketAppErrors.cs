using Helpdesk.Shared.Errors;

namespace Helpdesk.Modules.Tickets.Domain.Errors;

public static class TicketAppErrors
{
    public static readonly Error TicketNotFound =
        new("ticket.not_found", "Ticket not found.");

    public static readonly Error Forbidden =
        new("ticket.forbidden", "You do not have permission to perform this action on this ticket.");
}
