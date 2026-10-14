using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Mapping;

namespace OrderManagement.Application.Services;

public sealed class CustomerService(
    ICustomerRepository customers,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock) : ICustomerService
{
    public async Task<CustomerResponse> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var customer = await GetRequiredCustomerAsync(currentUser.GetUserId(), cancellationToken);
        return ResponseMapper.ToResponse(customer);
    }

    public async Task<CustomerResponse> UpdateCurrentAsync(UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        var updated = await customers.TryUpdateNameAsync(userId, request.Name.Trim(), clock.UtcNow, cancellationToken);
        if (!updated)
        {
            throw new NotFoundException("Customer", userId);
        }

        var customer = await GetRequiredCustomerAsync(userId, cancellationToken);
        return ResponseMapper.ToResponse(customer);
    }

    public async Task<CustomerResponse> SetActiveAsync(int customerId, bool isActive, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var updated = await customers.TrySetActiveAsync(customerId, isActive, clock.UtcNow, cancellationToken);
        if (!updated)
        {
            throw new NotFoundException("Customer", customerId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var customer = await GetRequiredCustomerAsync(customerId, cancellationToken);
        return ResponseMapper.ToResponse(customer);
    }

    private async Task<Domain.Entities.Customer> GetRequiredCustomerAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException("Customer", id);
        }

        return customer;
    }

    private void EnsureAdmin()
    {
        if (!currentUser.IsAdmin)
        {
            throw new ForbiddenException("Only administrators can manage customers.");
        }
    }
}
