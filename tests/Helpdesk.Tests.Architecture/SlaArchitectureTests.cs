using FluentAssertions;
using Helpdesk.Modules.SLA;
using NetArchTest.Rules;

namespace Helpdesk.Tests.Architecture;

public sealed class SlaArchitectureTests
{
    [Fact]
    public void SLA_Domain_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types
            .InAssembly(typeof(SlaModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.SLA.Domain")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must have zero infrastructure dependencies. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void SLA_Domain_Should_Not_Reference_Infrastructure_Namespace()
    {
        var result = Types
            .InAssembly(typeof(SlaModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.SLA.Domain")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.Modules.SLA.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must not depend on Infrastructure. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void SLA_Application_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types
            .InAssembly(typeof(SlaModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.SLA.Application")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer must not depend on EF Core. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void SLA_Application_Should_Not_Reference_Infrastructure_Namespace()
    {
        var result = Types
            .InAssembly(typeof(SlaModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.SLA.Application")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.Modules.SLA.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer must not depend on Infrastructure. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void SLA_Infrastructure_Should_Not_Reference_API()
    {
        var result = Types
            .InAssembly(typeof(SlaModule).Assembly)
            .That()
            .ResideInNamespace("Helpdesk.Modules.SLA.Infrastructure")
            .ShouldNot()
            .HaveDependencyOn("Helpdesk.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Infrastructure layer must not depend on API. " +
                     $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
