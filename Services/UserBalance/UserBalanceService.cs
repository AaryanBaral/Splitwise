using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Controllers;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Services.Group;
using Splitwise_Back.Services.User;

namespace Splitwise_Back.Services.UserBalance;

public class UserBalanceService:IUserBalanceService
{
    private readonly ILogger<ExpenseController> _logger;
    private readonly AppDbContext _context;
    public UserBalanceService(ILogger<ExpenseController> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<bool> ValidateUserBalances(string userId)
    {
        try
        {
            var userBalances = await _context.UserBalances
                .Where(e => (e.OwesToUserId == userId || e.OwedByUserId == userId) && e.Balance != 0)
                .AsNoTracking()
                .ToListAsync();
            return userBalances.Count != 0;
        }
        catch (Exception e)
        {
            throw new CustomException()
            {
                Errors = e.Message,
                StatusCode = 500,
            };
        }
    }
}