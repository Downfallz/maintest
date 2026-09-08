using System.Reflection;
using DownfallArena.Application;
using DownfallArena.Domain.Common;
using DownfallArena.Infrastructure;
using NetArchTest.Rules;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// The dependency rule of the architecture, as code. See docs/architecture/overview.md.
/// </summary>
public sealed class LayerDependencyTests
{
    private const string DomainNamespace = "DownfallArena.Domain";
    private const string ApplicationNamespace = "DownfallArena.Application";
    private const string InfrastructureNamespace = "DownfallArena.Infrastructure";
    private const string CliNamespace = "DownfallArena.Cli";

    private static readonly Assembly DomainAssembly = typeof(Result).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_outside_itself()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, CliNamespace, "Microsoft.Extensions")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_hosts()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, CliNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_hosts()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(CliNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Domain_types_live_in_the_domain_namespace()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ArePublic()
            .Should()
            .ResideInNamespace(DomainNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful
            ? "OK"
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
