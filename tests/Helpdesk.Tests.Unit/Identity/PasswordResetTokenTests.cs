using FluentAssertions;
using Helpdesk.Modules.Identity.Domain.Entities;

namespace Helpdesk.Tests.Unit.Identity;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTime Now = new(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_Be_Valid_When_Not_Used_And_Not_Expired()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        token.IsValid(Now).Should().BeTrue();
    }

    [Fact]
    public void Should_Expire_After_One_Hour()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);
        var afterExpiry = Now.AddHours(1).AddSeconds(1);

        token.IsValid(afterExpiry).Should().BeFalse();
    }

    [Fact]
    public void Should_Be_Invalid_When_Used()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);
        token.MarkAsUsed();

        token.IsValid(Now).Should().BeFalse();
    }

    [Fact]
    public void Should_Set_Expiry_At_One_Hour_From_Creation()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        token.ExpiresAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Should_Not_Be_Used_On_Creation()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        token.Used.Should().BeFalse();
    }

    [Fact]
    public void Should_Mark_As_Used()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash", Now);

        token.MarkAsUsed();

        token.Used.Should().BeTrue();
    }
}
