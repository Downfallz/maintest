using System.Reflection;
using DownfallArena.Application;

namespace DownfallArena.Architecture.Tests;

/// <summary>
/// A use case is one class with one public method (.claude/rules/application.md): the feedback box's use cases, which
/// answer through <c>HandleAsync</c>, declare no other (#865).
/// </summary>
public sealed class UseCaseTests
{
    private const string FeedbackBox = "DownfallArena.Application.FeedbackBox";

    [Fact]
    public void A_feedback_use_case_declares_one_public_method()
    {
        var bags = typeof(ApplicationServiceCollectionExtensions).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.IsClass && type.Namespace == FeedbackBox && type.GetMethod("HandleAsync") is not null)
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Count(method => !method.IsSpecialName) != 1)
            .Select(type => type.Name)
            .ToList();

        bags.ShouldBeEmpty();
    }
}
