namespace Core.DTOs.Common
{
    /// <summary>
    /// Represents the result of an operation, indicating success or failure and an optional error message.
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; protected set; }
        public string ErrorMessage { get; protected set; } = string.Empty;

        protected Result() { }

        public static Result Success() => new Result { IsSuccess = true };
        public static Result Failure(string errorMessage) => new Result { IsSuccess = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    /// Represents the result of an operation that returns a value on success.
    /// </summary>
    /// <typeparam name="T">The type of the returned data.</typeparam>
    public class Result<T> : Result
    {
        public T? Data { get; private set; }

        private Result() { }

        public static Result<T> Success(T data) => new Result<T> { IsSuccess = true, Data = data };
        public new static Result<T> Failure(string errorMessage) => new Result<T> { IsSuccess = false, ErrorMessage = errorMessage };
    }
}
