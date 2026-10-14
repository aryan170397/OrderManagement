namespace OrderManagement.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string Admin = "AdminOnly";

    public const string Customer = "CustomerOnly";
}
