using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OrderManagement.Infrastructure.Persistence;

/// <summary>
/// MySQL DATETIME has no offset. Connector/NET converts <see cref="DateTimeKind.Utc"/> to the machine's local time,
/// so values are stored as an unspecified UTC wall clock and read back as UTC.
/// </summary>
public static class DateTimeStorage
{
    public static readonly ValueConverter<DateTime, DateTime> Converter = new(
        value => ToUnspecifiedUtc(value),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTime ToUnspecifiedUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
    }
}
