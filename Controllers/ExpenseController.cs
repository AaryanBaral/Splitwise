using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.Expense;

namespace Splitwise_Back.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService  _expenseService;

    public ExpenseController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    [Route("test/{id}")]
    public async Task<IActionResult> GetExpense(string id)
    {
        var expenseResult = await _expenseService.GetExpenseAsync(id);
        return StatusCode(expenseResult.StatusCode,
            new { Id = expenseResult.Data, Success = expenseResult.Success, Errors = expenseResult.Errors });
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
        await _expenseService.UpdateExpense(id, updateExpenseDto);
        return Ok("Check Console");
    }
}