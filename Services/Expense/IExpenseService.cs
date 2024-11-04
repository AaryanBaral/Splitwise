using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Services.Expense;

public interface IExpenseService
{
    Task<ExpenseResults<string>> CreateExpenseAsync(CreateExpenseDto createExpenseDto);
    Task<ExpenseResults<ReadTestExpenseDto>> GetExpenseAsync(string expenseId);
}