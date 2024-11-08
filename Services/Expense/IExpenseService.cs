using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Services.Expense;

public interface IExpenseService
{
    // Task<ResponseResults<string>> CreateExpenseAsync(CreateExpenseDto createExpenseDto);
    Task<ResponseResults<ReadTestExpenseDto>> GetExpenseAsync(string expenseId);
    Task<ResponseResults<List<ReadAllExpenseDto>>> GetAllExpenses(string groupId);
    Task<ResponseResults<string>> UpdateExpense(string expenseId, UpdateExpenseDto updateExpenseDto);
    Task<ResponseResults<string>> CreateExpenseAsync(CreateExpenseDto createExpenseDto);
    Task<ResponseResults<string>> DeleteExpense(string expenseId);
}