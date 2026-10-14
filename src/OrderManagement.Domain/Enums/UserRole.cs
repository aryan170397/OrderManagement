namespace OrderManagement.Domain.Enums;

/// <summary>
/// Application role. Persisted as the enum name. Registration always assigns <see cref="Customer"/>.
/// </summary>
public enum UserRole
{
    Customer = 0,
    Admin = 1
}
