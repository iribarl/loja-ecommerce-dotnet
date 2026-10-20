namespace Loja.SharedKernel.Tests;

public class ResultTests
{
    private static readonly Error SomeError = Error.NotFound("Test.NotFound", "Not found.");

    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_KeepsTheError()
    {
        var result = Result.Failure(SomeError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SomeError);
    }

    [Fact]
    public void Failure_WithErrorNone_Throws()
    {
        Should.Throw<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void ValueOfSuccess_ReturnsTheValue()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void ValueOfFailure_Throws()
    {
        Result<int> result = SomeError;

        result.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => result.Value);
    }
}
