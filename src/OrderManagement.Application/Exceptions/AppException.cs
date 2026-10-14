namespace OrderManagement.Application.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string title, string detail, int statusCode, string type, string errorCode)
        : base(detail)
    {
        Title = title;
        StatusCode = statusCode;
        Type = type;
        ErrorCode = errorCode;
    }

    public string Title { get; }

    public int StatusCode { get; }

    public string Type { get; }

    public string ErrorCode { get; }
}
