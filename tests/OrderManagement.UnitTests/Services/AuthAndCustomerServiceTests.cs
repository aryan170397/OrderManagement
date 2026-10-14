using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Services;
using OrderManagement.Domain.Enums;
using OrderManagement.UnitTests.Support;

namespace OrderManagement.UnitTests.Services;

public class AuthAndCustomerServiceTests
{
    [Fact]
    public async Task Registration_creates_an_active_customer_and_never_assigns_admin()
    {
        var fixture = new ServiceFixture();
        var service = CreateAuthService(fixture);

        var response = await service.RegisterAsync(new RegisterRequest
        {
            Name = "Ada Lovelace",
            Email = " Ada@Example.com ",
            Password = "Password123"
        }, CancellationToken.None);

        Assert.Equal(UserRole.Customer.ToString(), response.Customer.Role);
        Assert.Equal("ada@example.com", response.Customer.Email);
        Assert.True(response.Customer.IsActive);
        Assert.StartsWith("token-", response.AccessToken);
        Assert.Equal("hashed:Password123", fixture.Store.Customers.Single().PasswordHash);
        Assert.DoesNotContain("passwordHash", JsonSerializer.Serialize(response), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password123", JsonSerializer.Serialize(response), StringComparison.Ordinal);
    }

    [Fact]
    public void Registration_request_has_no_role_property()
    {
        Assert.DoesNotContain(
            typeof(RegisterRequest).GetProperties(),
            property => property.Name.Equals("Role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer("ada@example.com");
        var service = CreateAuthService(fixture);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.RegisterAsync(new RegisterRequest
        {
            Name = "Ada",
            Email = "ADA@example.com",
            Password = "Password123"
        }, CancellationToken.None));

        Assert.Equal("duplicate_email", exception.ErrorCode);
    }

    [Fact]
    public async Task Invalid_credentials_and_inactive_accounts_are_rejected()
    {
        var fixture = new ServiceFixture();
        var service = CreateAuthService(fixture);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => service.LoginAsync(new LoginRequest
        {
            Email = "missing@example.com",
            Password = "Password123"
        }, CancellationToken.None));

        fixture.AddCustomer(active: false);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => service.LoginAsync(new LoginRequest
        {
            Email = "ada@example.com",
            Password = "Password123"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Customer_profile_update_changes_only_the_authenticated_customer_name()
    {
        var fixture = new ServiceFixture();
        var customer = fixture.AddCustomer();
        var service = new CustomerService(fixture.Customers, fixture.CurrentUser, fixture.UnitOfWork, fixture.Clock);

        var updated = await service.UpdateCurrentAsync(new UpdateCustomerRequest { Name = "Ada Lovelace" }, CancellationToken.None);

        Assert.Equal(customer.Id, updated.Id);
        Assert.Equal("Ada Lovelace", updated.Name);
        Assert.Equal("ada@example.com", updated.Email);
        Assert.Equal(UserRole.Customer.ToString(), updated.Role);
    }

    [Fact]
    public async Task Non_admin_cannot_deactivate_a_customer()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer();
        var service = new CustomerService(fixture.Customers, fixture.CurrentUser, fixture.UnitOfWork, fixture.Clock);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.SetActiveAsync(1, false, CancellationToken.None));
    }

    private static AuthService CreateAuthService(ServiceFixture fixture) =>
        new(
            fixture.Customers,
            new FakePasswordHasher(),
            new FakeTokenService(),
            fixture.UnitOfWork,
            fixture.Clock,
            NullLogger<AuthService>.Instance);
}
