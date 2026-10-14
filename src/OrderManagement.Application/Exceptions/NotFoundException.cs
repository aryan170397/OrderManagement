namespace OrderManagement.Application.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string resourceName, int id)
        : base(
            "Resource not found",
            $"{resourceName} {id} was not found.",
            AppStatusCodes.NotFound,
            ProblemTypes.NotFound,
            "not_found")
    {
    }
}
