using Helpdesk.Shared.Errors;

namespace Helpdesk.Shared.Results;

public readonly struct Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(T value) { Value = value; IsSuccess = true; }
    private Result(Error error) { Error = error; IsSuccess = false; }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Error error) => Fail(error);
}

public readonly struct Result
{
    public Error? Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(bool success, Error? error) { IsSuccess = success; Error = error; }

    public static Result Ok() => new(true, null);
    public static Result Fail(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Fail(error);
}
