using Helpdesk.Modules.Identity.Domain.Enums;

namespace Helpdesk.Modules.Identity.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool EmailVerified { get; private set; }
    public DateTime? EmailVerifiedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private User() { }

    public static User Create(string email, string name, string passwordHash, UserRole role, DateTime now)
    {
        var emailVerified = role != UserRole.Customer;
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant().Trim(),
            Name = name.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            EmailVerified = emailVerified,
            EmailVerifiedAt = emailVerified ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void VerifyEmail(DateTime now)
    {
        if (EmailVerified) return;
        EmailVerified = true;
        EmailVerifiedAt = now;
        UpdatedAt = now;
    }

    public void UpdatePassword(string newPasswordHash, DateTime now)
    {
        PasswordHash = newPasswordHash;
        UpdatedAt = now;
    }
}
