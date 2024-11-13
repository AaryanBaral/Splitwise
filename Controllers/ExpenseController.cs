using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitwise_Back.Data;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.Expense;

namespace Splitwise_Back.Controllers;

// [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly AppDbContext _context;


    public ExpenseController(IExpenseService expenseService, AppDbContext context)
    {
        _expenseService = expenseService;
        _context = context;
    }

    [HttpGet]
    [Route("test/{id}")]
    public async Task<IActionResult> GetExpense(string id)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var expenseResult = await _expenseService.GetExpenseAsync(id);
            await transaction.CommitAsync();
            return StatusCode(expenseResult.StatusCode,
                new { Id = expenseResult.Data, Success = expenseResult.Success, Errors = expenseResult.Errors });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto createExpenseDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var expenseResult = await _expenseService.CreateExpenseAsync(createExpenseDto);
        return StatusCode(expenseResult.StatusCode,
            new { Data = expenseResult.Data, Success = expenseResult.Success, Errors = expenseResult.Errors });
    }

    [HttpGet]
    [Route("all/{groupId}")]
    public async Task<IActionResult> GetAllExpenses(string groupId)
    {
        var expenseResult = await _expenseService.GetAllExpenses(groupId);
        return StatusCode(expenseResult.StatusCode,
            new { Data = expenseResult.Data, Success = expenseResult.Success, Errors = expenseResult.Errors });
    }

    [HttpPut]
    [Route("update/{id}")]
    public async Task<IActionResult> UpdateExpense(string id, [FromBody] UpdateExpenseDto updateExpenseDto)
    {
        var expenseResult = await _expenseService.UpdateExpense(id, updateExpenseDto);
        return StatusCode(expenseResult.StatusCode,
            new { Data = expenseResult.Data, Success = expenseResult.Success, Errors = expenseResult.Errors });
    }

    [HttpDelete]
    [Route("delete/{id}")]
    public async Task<IActionResult> DeleteExpense(string id)
    {
        var result = await _expenseService.DeleteExpense(id);
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }

    [HttpGet]
    [Route("expenseshares/{id}")]
    public async Task<IActionResult> GetAllExpenseShares(string id)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            return Ok(await _expenseService.GetAllExpenseShares(id));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}