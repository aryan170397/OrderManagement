namespace OrderManagement.Application.Interfaces;

public interface ICurrentUser
{
    bool IsAdmin { get; }

    bool IsCustomer { get; }

    int GetUserId();
}
