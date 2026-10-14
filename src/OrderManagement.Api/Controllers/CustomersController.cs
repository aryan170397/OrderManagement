using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Authorization;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Interfaces;

namespace OrderManagement.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerResponse>> GetCurrent(CancellationToken cancellationToken)
    {
        return Ok(await customers.GetCurrentAsync(cancellationToken));
    }

    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerResponse>> UpdateCurrent(UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        return Ok(await customers.UpdateCurrentAsync(request, cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPatch("{id:int}/activate")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Activate(int id, CancellationToken cancellationToken)
    {
        return Ok(await customers.SetActiveAsync(id, isActive: true, cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpPatch("{id:int}/deactivate")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Deactivate(int id, CancellationToken cancellationToken)
    {
        return Ok(await customers.SetActiveAsync(id, isActive: false, cancellationToken));
    }
}
