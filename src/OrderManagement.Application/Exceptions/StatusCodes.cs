namespace OrderManagement.Application.Exceptions;

public static class AppStatusCodes
{
    public const int BadRequest = 400;
    public const int Unauthorized = 401;
    public const int Forbidden = 403;
    public const int NotFound = 404;
    public const int Conflict = 409;
}

public static class ProblemTypes
{
    public const string Base = "https://ordermanagement.local/problems";
    public const string Validation = Base + "/validation";
    public const string Unauthorized = Base + "/unauthorized";
    public const string Forbidden = Base + "/forbidden";
    public const string NotFound = Base + "/not-found";
    public const string Conflict = Base + "/conflict";
    public const string Unexpected = Base + "/unexpected";
}
