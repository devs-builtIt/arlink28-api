namespace Arlink28.Api.Helpers;

public class Result
{
    internal Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; init; }
    public string[] Errors { get; init; }

    public static Result Success() => new(true, []);
    public static Result Failure(IEnumerable<string> errors) => new(false, errors);
    public static Result Failure(string error) => new(false, [error]);
}

public class Result<T>
{
    internal Result(bool succeeded, T value, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Value = value;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; init; }
    public T Value { get; init; }
    public string[] Errors { get; init; }

    public static Result<T> Success(T value) => new(true, value, []);
    public static Result<T> Failure(IEnumerable<string> errors) => new(false, default!, errors);
    public static Result<T> Failure(string error) => new(false, default!, [error]);
}
