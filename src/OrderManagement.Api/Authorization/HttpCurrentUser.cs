using System.Globalization;
using System.Security.Claims;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Api.Authorization;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAdmin => Principal?.IsInRole(nameof(UserRole.Admin)) == true;

    public bool IsCustomer => Principal?.IsInRole(nameof(UserRole.Customer)) == true;

    public int GetUserId()
    {
        var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            throw new UnauthorizedAppException("Authentication is required.");
        }

        return id;
    }

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
