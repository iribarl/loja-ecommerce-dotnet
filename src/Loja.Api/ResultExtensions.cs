using Loja.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Loja.Api;

public static class ResultExtensions
{
    /// <summary>
    /// Translates a failed <see cref="Result"/> into a ProblemDetails response.
    /// </summary>
    public static ProblemHttpResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be converted to a problem.");
        }

        var error = result.Error;

        return TypedResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: StatusCodeFor(error.Type),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}
