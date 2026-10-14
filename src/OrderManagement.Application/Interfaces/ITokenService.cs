using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Interfaces;

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(Customer customer);
}
