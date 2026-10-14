namespace OrderManagement.Application.Interfaces;

public enum InventoryReservationFailure
{
    None = 0,
    NotFound = 1,
    Inactive = 2,
    InsufficientStock = 3
}

public sealed class InventoryReservationResult
{
    public bool Succeeded { get; init; }

    public InventoryReservationFailure Failure { get; init; }

    public static InventoryReservationResult Success() =>
        new() { Succeeded = true, Failure = InventoryReservationFailure.None };

    public static InventoryReservationResult Fail(InventoryReservationFailure failure) =>
        new() { Succeeded = false, Failure = failure };
}
