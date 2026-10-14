using OrderManagement.Application.Interfaces;

namespace OrderManagement.Application.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
