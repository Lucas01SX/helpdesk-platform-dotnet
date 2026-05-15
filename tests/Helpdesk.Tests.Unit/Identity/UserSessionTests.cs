using FluentAssertions;
using Helpdesk.Modules.Identity.Domain.Entities;

namespace Helpdesk.Tests.Unit.Identity;

public sealed class UserSessionTests
{
    private static readonly DateTime Now = new(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan SevenDays = TimeSpan.FromDays(7);

    [Fact]
    public void Should_Be_Active_When_Not_Revoked_And_Not_Expired()
    {
        var session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", null, null, Now, SevenDays);

        session.IsActive(Now).Should().BeTrue();
    }

    [Fact]
    public void Should_Not_Be_Active_When_Revoked()
    {
        var session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", null, null, Now, SevenDays);
        session.Revoke(Now.AddHours(1));

        session.IsActive(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Should_Not_Be_Active_When_Expired()
    {
        var session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", null, null, Now, SevenDays);
        var afterExpiry = Now.Add(SevenDays).AddSeconds(1);

        session.IsActive(afterExpiry).Should().BeFalse();
    }

    [Fact]
    public void Should_Set_Expires_At_Based_On_Lifetime()
    {
        var session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", null, null, Now, SevenDays);

        session.ExpiresAt.Should().Be(Now.Add(SevenDays));
    }

    [Fact]
    public void Should_Set_Revoked_At_When_Revoked()
    {
        var session = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", null, null, Now, SevenDays);
        var revokedAt = Now.AddHours(3);

        session.Revoke(revokedAt);

        session.RevokedAt.Should().Be(revokedAt);
    }

    [Fact]
    public void Should_Store_User_Agent_And_Ip()
    {
        var session = UserSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), "hash",
            "Mozilla/5.0", "192.168.1.1",
            Now, SevenDays);

        session.UserAgent.Should().Be("Mozilla/5.0");
        session.IpAddress.Should().Be("192.168.1.1");
    }

    [Fact]
    public void Should_Assign_Unique_Id_On_Create()
    {
        var session1 = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), "h1", null, null, Now, SevenDays);
        var session2 = UserSession.Create(Guid.NewGuid(), Guid.NewGuid(), "h2", null, null, Now, SevenDays);

        session1.Id.Should().NotBe(session2.Id);
    }
}
