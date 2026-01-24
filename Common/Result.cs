namespace UserService.Common;

/// <summary>
/// Generic result pattern for operations
/// Encapsulates success/failure state with optional value and errors
/// </summary>
/// <typeparam name="T">Type of the result value</typeparam>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }
    public List<string>? Errors { get; }

    private Result(bool isSuccess, T? value, string? error, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Errors = errors;
    }

    /// <summary>
    /// Creates a successful result with a value
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a successful result without a value
    /// </summary>
    public static Result<T> Success() => new(true, default, null);

    /// <summary>
    /// Creates a failure result with an error message
    /// </summary>
    public static Result<T> Failure(string error) => new(false, default, error, new List<string> { error });

    /// <summary>
    /// Creates a failure result with multiple errors
    /// </summary>
    public static Result<T> Failure(List<string> errors) => new(false, default, errors.FirstOrDefault(), errors);

    /// <summary>
    /// Creates a failure result with an exception
    /// </summary>
    public static Result<T> Failure(Exception exception) => new(false, default, exception.Message, new List<string> { exception.Message });
}

/// <summary>
/// Non-generic result for operations without return values
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public List<string>? Errors { get; }

    private Result(bool isSuccess, string? error, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Errors = errors;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error, new List<string> { error });
    public static Result Failure(List<string> errors) => new(false, errors.FirstOrDefault(), errors);
    public static Result Failure(Exception exception) => new(false, exception.Message, new List<string> { exception.Message });
}
