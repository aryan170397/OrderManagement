namespace OrderManagement.Domain.Entities;

public sealed class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int AvailableQuantity { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Inventory reservations and administrative updates increment it.
    /// </summary>
    public long Version { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
