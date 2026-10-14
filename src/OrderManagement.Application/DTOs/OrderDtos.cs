namespace OrderManagement.Application.DTOs;

public sealed class CreateOrderRequest
{
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

public sealed class CreateOrderItemRequest
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
}

public sealed class OrderResponse
{
    public int Id { get; init; }

    public int CustomerId { get; init; }

    public decimal TotalAmount { get; init; }

    public required string Status { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public required IReadOnlyList<OrderItemResponse> Items { get; init; }
}

public sealed class OrderItemResponse
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public required string ProductName { get; init; }

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}

public sealed class OrderQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Status { get; set; }

    public DateTime? CreatedFromUtc { get; set; }

    public DateTime? CreatedToUtc { get; set; }
}

public sealed class PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }
}
