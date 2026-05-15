using FluentAssertions;
using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Enums;

namespace Helpdesk.Tests.Unit.Identity;

public sealed class UserTests
{
    private static readonly DateTime Now = new(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_Create_User_With_Normalized_Email()
    {
        var user = User.Create("  ALICE@Example.COM  ", "Alice", "hash", UserRole.Customer, Now);

        user.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public void Should_Create_Customer_With_Email_Not_Verified()
    {
        var user = User.Create("alice@example.com", "Alice", "hash", UserRole.Customer, Now);

        user.EmailVerified.Should().BeFalse();
        user.EmailVerifiedAt.Should().BeNull();
    }

    [Fact]
    public void Should_Create_SupportAgent_With_Email_Pre_Verified()
    {
        var user = User.Create("agent@example.com", "Agent Bob", "hash", UserRole.SupportAgent, Now);

        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(Now);
    }

    [Fact]
    public void Should_Create_Manager_With_Email_Pre_Verified()
    {
        var user = User.Create("manager@example.com", "Manager Carol", "hash", UserRole.Manager, Now);

        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(Now);
    }

    [Fact]
    public void Should_Verify_Email_And_Set_Timestamp()
    {
        var user = User.Create("alice@example.com", "Alice", "hash", UserRole.Customer, Now);
        var verifiedAt = Now.AddHours(1);

        user.VerifyEmail(verifiedAt);

        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(verifiedAt);
        user.UpdatedAt.Should().Be(verifiedAt);
    }

    [Fact]
    public void Should_Be_Idempotent_When_Verifying_Already_Verified_Email()
    {
        var user = User.Create("alice@example.com", "Alice", "hash", UserRole.Customer, Now);
        user.VerifyEmail(Now.AddHours(1));
        var firstVerifiedAt = user.EmailVerifiedAt;

        // Second call should not change the verified-at timestamp
        user.VerifyEmail(Now.AddHours(2));

        user.EmailVerifiedAt.Should().Be(firstVerifiedAt);
    }

    [Fact]
    public void Should_Update_Password_And_Timestamp()
    {
        var user = User.Create("alice@example.com", "Alice", "original-hash", UserRole.Customer, Now);
        var updatedAt = Now.AddDays(1);

        user.UpdatePassword("new-hash", updatedAt);

        user.PasswordHash.Should().Be("new-hash");
        user.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Should_Assign_Unique_Id_On_Create()
    {
        var user1 = User.Create("a@example.com", "A", "hash", UserRole.Customer, Now);
        var user2 = User.Create("b@example.com", "B", "hash", UserRole.Customer, Now);

        user1.Id.Should().NotBe(user2.Id);
        user1.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Should_Set_Correct_Role()
    {
        var customer = User.Create("c@example.com", "C", "hash", UserRole.Customer, Now);
        var agent = User.Create("a@example.com", "A", "hash", UserRole.SupportAgent, Now);

        customer.Role.Should().Be(UserRole.Customer);
        agent.Role.Should().Be(UserRole.SupportAgent);
    }
}
