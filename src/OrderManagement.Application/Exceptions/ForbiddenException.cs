namespace OrderManagement.Application.Exceptions;

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string detail)
        : base("Forbidden", detail, AppStatusCodes.Forbidden, ProblemTypes.Forbidden, "forbidden")
    {
    }
}
