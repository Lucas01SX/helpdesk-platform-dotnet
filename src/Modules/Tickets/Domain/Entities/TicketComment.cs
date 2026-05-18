using Helpdesk.Modules.Tickets.Domain.Enums;

namespace Helpdesk.Modules.Tickets.Domain.Entities;

public sealed class TicketComment
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public CommentVisibility Visibility { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TicketComment() { }

    public static TicketComment Create(
        Guid ticketId, Guid authorId, string content,
        CommentVisibility visibility, DateTime now)
    {
        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = authorId,
            Content = content,
            Visibility = visibility,
            CreatedAt = now
        };
    }
}
