using System.Reflection;
using DownfallArena.Application;
using DownfallArena.Infrastructure;
using DownfallArena.SharedKernel.Primitives;
using NetArchTest.Rules;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// The dependency rule of the architecture, as code. See docs/architecture/overview.md and ADR 0007.
/// </summary>
public sealed class LayerDependencyTests
{
    private const string SharedKernelNamespace = "DownfallArena.SharedKernel";
    private const string SharedKernelAssemblyName = "DownfallArena.SharedKernel";
    private const string DomainAssemblyName = "DownfallArena.Domain";
    private const string ApplicationNamespace = "DownfallArena.Application";
    private const string InfrastructureNamespace = "DownfallArena.Infrastructure";
    private const string CliNamespace = "DownfallArena.Cli";

    private static readonly Assembly SharedKernelAssembly = typeof(Result).Assembly;
    private static readonly Assembly DomainAssembly = Assembly.Load(DomainAssemblyName);
    private static readonly Assembly ApplicationAssembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;

    [Fact]
    public void Shared_kernel_references_only_the_base_class_library()
    {
        ForeignReferences(SharedKernelAssembly).ShouldBeEmpty();
    }

    [Fact]
    public void Domain_references_only_the_base_class_library_and_the_shared_kernel()
    {
        ForeignReferences(DomainAssembly, SharedKernelAssemblyName).ShouldBeEmpty();
    }

    [Fact]
    public void Shared_kernel_types_live_in_its_namespace()
    {
        var result = Types.InAssembly(SharedKernelAssembly)
            .That()
            .ArePublic()
            .Should()
            .ResideInNamespace(SharedKernelNamespace)
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

    private static List<string> ForeignReferences(Assembly assembly, params string[] allowedAssemblies) =>
        assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !IsBaseClassLibrary(name) && !allowedAssemblies.Contains(name, StringComparer.Ordinal))
            .ToList();

    private static bool IsBaseClassLibrary(string assemblyName) =>
        assemblyName.StartsWith("System", StringComparison.Ordinal)
        || assemblyName is "netstandard" or "mscorlib";

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful
            ? "OK"
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
