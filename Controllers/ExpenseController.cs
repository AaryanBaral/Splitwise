using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.Expense;

namespace Splitwise_Back.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class ExpenseController : ControllerBase
{
    private readonly ILogger<ExpenseController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<CustomUsers> _userManager;
    private readonly IExpenseService  _expenseService;

    public ExpenseController(ILogger<ExpenseController> logger, AppDbContext context,
        UserManager<CustomUsers> userManager, IExpenseService expenseService)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
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
}