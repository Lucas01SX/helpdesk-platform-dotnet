namespace Helpdesk.Modules.Identity.Domain.Entities;

public sealed class EmailVerificationToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool Used { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private EmailVerificationToken() { }

    public bool IsValid(DateTime now) => !Used && ExpiresAt > now;

    public static EmailVerificationToken Create(Guid userId, string tokenHash, DateTime now)
    {
        return new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = now.AddHours(24),
            Used = false,
            CreatedAt = now
        };
    }

    public void MarkAsUsed()
    {
        Used = true;
    }
}
