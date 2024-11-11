namespace Splitwise_Back.Services.UserBalance;

public interface IUserBalanceService
{
    Task<bool> ValidateUserBalances(string userId);
}