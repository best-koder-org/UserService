namespace UserService.Common;

/// <summary>
/// Standardized API response wrapper for all endpoints
/// Provides consistent structure for success and error responses
/// </summary>
/// <typeparam name="T">Type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The actual data payload (null if error)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Human-readable message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// List of validation or business rule errors
    /// </summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Unique error code for client-side handling
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Timestamp of the response
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static ApiResponse<T> SuccessResult(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Creates a successful response without data
    /// </summary>
    public static ApiResponse<T> SuccessResult(string message)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failure response with error message
    /// </summary>
    public static ApiResponse<T> FailureResult(string message, string? errorCode = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = new List<string> { message }
        };
    }

    /// <summary>
    /// Creates a failure response with multiple errors
    /// </summary>
    public static ApiResponse<T> FailureResult(List<string> errors, string? message = null, string? errorCode = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message ?? "Validation failed",
            Errors = errors,
            ErrorCode = errorCode
        };
    }

    /// <summary>
    /// Creates a validation failure response
    /// </summary>
    public static ApiResponse<T> ValidationFailure(List<string> errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = "Validation failed",
            Errors = errors,
            ErrorCode = "VALIDATION_ERROR"
        };
    }
}
