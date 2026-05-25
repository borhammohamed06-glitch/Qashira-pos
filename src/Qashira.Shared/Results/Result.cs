namespace Qashira.Shared.Results;

public sealed record Result(bool Succeeded, string Message)
{
    public static Result Success(string message = "") => new(true, message);
    public static Result Failure(string message) => new(false, message);
}

public sealed record Result<T>(bool Succeeded, string Message, T? Value)
{
    public static Result<T> Success(T value, string message = "") => new(true, message, value);
    public static Result<T> Failure(string message) => new(false, message, default);
}
