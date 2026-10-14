namespace OrderManagement.Application.Exceptions;

public sealed class ConflictException : AppException
{
    public ConflictException(string detail, string errorCode)
        : base("Conflict", detail, AppStatusCodes.Conflict, ProblemTypes.Conflict, errorCode)
    {
    }
}
