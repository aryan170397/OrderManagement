namespace OrderManagement.Application.DTOs;

public sealed class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int AvailableQuantity { get; set; }
}

public sealed class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long Version { get; set; }
}

public sealed class UpdateProductPriceRequest
{
    public decimal Price { get; set; }

    public long Version { get; set; }
}

/// <summary>
/// Sets the product's available quantity to an absolute value. This is not a relative adjustment.
/// <see cref="Version"/> must match the version last read by the caller.
/// </summary>
public sealed class UpdateInventoryRequest
{
    public int AvailableQuantity { get; set; }

    public long Version { get; set; }
}

public sealed class ProductResponse
{
    public int Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public decimal Price { get; init; }

    public int AvailableQuantity { get; init; }

    public bool IsActive { get; init; }

    public long Version { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class ProductQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public string SortBy { get; set; } = "name";

    public string SortDirection { get; set; } = "asc";
}
