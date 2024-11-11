using Splitwise_Back.Models;

namespace Splitwise_Back.Services.UserBalance;

public interface IUserBalanceService
{
    Task<bool> ValidateUserBalances(string userId);
    Task DeleteUserBalanceByGroup(string groupId);
}