using OrderManagement.Application.DTOs;

namespace OrderManagement.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public interface ICustomerService
{
    Task<CustomerResponse> GetCurrentAsync(CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateCurrentAsync(UpdateCustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> SetActiveAsync(int customerId, bool isActive, CancellationToken cancellationToken);
}

public interface IProductService
{
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<PagedResponse<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<ProductResponse> GetAsync(int id, CancellationToken cancellationToken);

    Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> UpdatePriceAsync(int id, UpdateProductPriceRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> UpdateInventoryAsync(int id, UpdateInventoryRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
}

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    Task<PagedResponse<OrderResponse>> ListAsync(OrderQuery query, CancellationToken cancellationToken);

    Task<OrderResponse> GetAsync(int id, CancellationToken cancellationToken);

    Task<OrderResponse> ConfirmAsync(int id, CancellationToken cancellationToken);

    Task<OrderResponse> ShipAsync(int id, CancellationToken cancellationToken);

    Task<OrderResponse> CancelAsync(int id, CancellationToken cancellationToken);
}
