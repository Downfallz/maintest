using DownfallArena.Domain.Common;

namespace DownfallArena.Domain.Tests.Common;

public sealed class ResultTests
{
    private static readonly DomainError SomeError = new("Test.Failed", "Something went wrong.");

    [Fact]
    public void Success_carries_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(DomainError.None);
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        var result = Result.Failure(SomeError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SomeError);
    }

    [Fact]
    public void Successful_result_exposes_its_value()
    {
        var result = Result.Success(42);

        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Successful_result_refuses_a_null_value()
    {
        Should.Throw<ArgumentNullException>(() => Result.Success<string?>(null));
    }

    [Fact]
    public void Failed_result_refuses_to_expose_a_value()
    {
        var result = Result.Failure<int>(SomeError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SomeError);
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void A_success_cannot_carry_an_error()
    {
        Should.Throw<ArgumentException>(() => new ProbeResult(isSuccess: true, SomeError));
    }

    [Fact]
    public void A_failure_must_carry_an_error()
    {
        Should.Throw<ArgumentException>(() => new ProbeResult(isSuccess: false, DomainError.None));
    }

    private sealed class ProbeResult(bool isSuccess, DomainError error) : Result(isSuccess, error)
    {
    }
}
