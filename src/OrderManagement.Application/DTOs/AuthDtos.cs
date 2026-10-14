namespace OrderManagement.Application.DTOs;

public sealed class RegisterRequest
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required string TokenType { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public required CustomerResponse Customer { get; init; }
}

public sealed class CustomerResponse
{
    public int Id { get; init; }

    public required string Name { get; init; }

    public required string Email { get; init; }

    public required string Role { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class UpdateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
}
