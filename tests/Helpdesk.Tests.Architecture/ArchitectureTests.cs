using FluentAssertions;
using Helpdesk.Modules.Identity;
using NetArchTest.Rules;

namespace Helpdesk.Tests.Architecture;

public sealed class ArchitectureTests
{
    private static readonly string IdentityAssembly =
        typeof(IdentityModule).Assembly.GetName().Name!;

    [Fact]
    public void Identity_Domain_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types
            .InAssembly(typeof(IdentityModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Identity.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must have zero infrastructure dependencies. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Identity_Domain_Should_Not_Reference_Infrastructure_Namespace()
    {
        var result = Types
            .InAssembly(typeof(IdentityModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Identity.Domain")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.Modules.Identity.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must not depend on Infrastructure. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Identity_Application_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types
            .InAssembly(typeof(IdentityModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Identity.Application")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer must not depend on EF Core. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Identity_Application_Should_Not_Reference_Infrastructure_Namespace()
    {
        var result = Types
            .InAssembly(typeof(IdentityModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Identity.Application")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.Modules.Identity.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer must not depend on Infrastructure. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Identity_Infrastructure_Should_Not_Reference_API()
    {
        var result = Types
            .InAssembly(typeof(IdentityModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.Identity.Infrastructure")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Infrastructure layer must not depend on API. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
