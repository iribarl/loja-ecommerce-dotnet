namespace Loja.SharedKernel;

/// <summary>
/// Kind of expected failure. The HTTP layer maps each one to a status code.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
}

/// <summary>
/// An expected failure of a use case, e.g. "product not found".
/// </summary>
/// <param name="Code">Stable, machine-readable identifier, e.g. "Catalog.ProductNotFound".</param>
/// <param name="Message">Human-readable description.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}
