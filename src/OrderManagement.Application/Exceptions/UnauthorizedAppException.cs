namespace OrderManagement.Application.Exceptions;

public sealed class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string detail)
        : base("Unauthorized", detail, AppStatusCodes.Unauthorized, ProblemTypes.Unauthorized, "unauthorized")
    {
    }
}
