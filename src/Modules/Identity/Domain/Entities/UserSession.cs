namespace Helpdesk.Modules.Identity.Domain.Entities;

public sealed class UserSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserSession() { }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    public static UserSession Create(
        Guid userId,
        Guid familyId,
        string refreshTokenHash,
        string? userAgent,
        string? ipAddress,
        DateTime now,
        TimeSpan lifetime)
    {
        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            RefreshTokenHash = refreshTokenHash,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            ExpiresAt = now.Add(lifetime),
            CreatedAt = now
        };
    }

    public void Revoke(DateTime now)
    {
        RevokedAt = now;
    }
}
