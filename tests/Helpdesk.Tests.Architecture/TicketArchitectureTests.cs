using FluentAssertions;
using Helpdesk.Modules.Tickets;
using NetArchTest.Rules;

namespace Helpdesk.Tests.Architecture;

public sealed class TicketArchitectureTests
{
    [Fact]
    public void Tickets_Domain_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types
            .InAssembly(typeof(TicketsModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Tickets.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must have zero infrastructure dependencies. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Tickets_Domain_Should_Not_Reference_Infrastructure_Namespace()
    {
        var result = Types
            .InAssembly(typeof(TicketsModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Tickets.Domain")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.Modules.Tickets.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must not depend on Infrastructure. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Tickets_Application_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types
            .InAssembly(typeof(TicketsModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Tickets.Application")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer must not depend on EF Core. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Tickets_Application_Should_Not_Reference_Infrastructure_Namespace()
    {
        var result = Types
            .InAssembly(typeof(TicketsModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Tickets.Application")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.Modules.Tickets.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer must not depend on Infrastructure. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Tickets_Infrastructure_Should_Not_Reference_API()
    {
        var result = Types
            .InAssembly(typeof(TicketsModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Tickets.Infrastructure")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Infrastructure layer must not depend on API. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
