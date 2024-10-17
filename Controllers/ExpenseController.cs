using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class ExpenseController : ControllerBase
{
    private readonly ILogger<ExpenseController> _logger;
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly UserManager<CustomUser> _userManager;

    public ExpenseController(ILogger<ExpenseController> logger, AppDbContext context, UserManager<CustomUser> userManager, IMapper mapper)
    {
        _logger = logger;
        _context = context;
        _mapper = mapper;
        _userManager = userManager;
    }
    [HttpGet]
    [Route("{id}")]
    public async Task<IActionResult> GetExpenseById(string id)
    {
        if (id is null)
        {
            return BadRequest("Please provide the expense id");
        }

        var expense = await _context.Expenses
            .Where(e => e.Id == id)
            .Include(e => e.Group)
            .ThenInclude(g => g.GroupMembers)
            .ThenInclude(gm => gm.User)
            .Include(e => e.Payer)
            .Include(e => e.ExpenseShares)
            .ThenInclude(es => es.Expense)
            .FirstOrDefaultAsync();

        if (expense is null)
        {
            return NotFound("Expense not found");
        }
        if (expense.Group is null)
        {
            return BadRequest("The expense does not belong to any group.");
        }

        var GroupMembers = expense.Group?.GroupMembers.Select(gm => new GroupMemberDto
        {
            UserId = gm.UserId,
            UserName = gm.User?.UserName
        }).ToList();
        if (GroupMembers is null)
        {
            return BadRequest("the group has no group members");
        }

        var ExpenseShareForExpense = expense.ExpenseShares.Select(es => new ExpenseShareForExpense()
        {
            AmountOwed = es.AmountOwed,
            ShareType = es.ShareType,
            User = new AbstractReadUserDto()
            {
                UserName = es.User?.UserName,
                Id = es.User?.Id
            },
            OwesUser = new AbstractReadUserDto()
            {
                UserName = es.OwesUser?.UserName,
                Id = es.OwesUserId
            }
        }).ToList();

        ReadExpenseDto readExpenseDto = new()
        {
            GroupId = expense.Id,
            Payer = new AbstractReadUserDto()
            {
                Id = expense.Payer?.Id,
                UserName = expense.Payer?.UserName,
            },
            Amount = expense.Amount,
            Date = expense.Date,
            Description = expense.Description,
            ExpenseShares = ExpenseShareForExpense
        };

        return Ok(readExpenseDto);
    }

    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto createExpenseDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate Group
        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == createExpenseDto.GroupId);

        if (group is null)
        {
            return BadRequest("Invalid group ID");
        }

        // Validate Payer
        var payer = await _userManager.FindByIdAsync(createExpenseDto.PayerId);
        if (payer is null || !group.GroupMembers.Any(gm => gm.UserId == createExpenseDto.PayerId))
        {
            return BadRequest("Invalid payer or payer is not a member of the group");
        }

        // Validate Share Participants
        var userIdsInGroup = group.GroupMembers.Select(gm => gm.UserId).ToHashSet();
        var invalidUsers = createExpenseDto.ExpenseShares.Where(es => !userIdsInGroup.Contains(es.UserId)).ToList();
        if (invalidUsers.Count != 0)
        {
            return BadRequest("One or more users in the expense shares are not members of the group");
        }
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {

            //Create a new expense
            var newExpense = new Expense
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = createExpenseDto.GroupId,
                PayerId = createExpenseDto.PayerId,
                Amount = createExpenseDto.Amount,
                Date = DateTime.UtcNow,
                Description = createExpenseDto.Description
            };
            _context.Expenses.Add(newExpense);
            await _context.SaveChangesAsync();

            //Get User Balance for each share
            foreach (var share in createExpenseDto.ExpenseShares)
            {
                var BalanceEntry = await _context.UserBalances
                .FirstOrDefaultAsync(b => b.UserId == share.UserId && b.OwedToUserId == share.OwesUserId);
                // if no Balance entry then create a new one
                if (BalanceEntry is null)
                {
                    //creating a balance entry
                    var balanceEntry = new UserBalance
                    {
                        UserId = share.UserId,
                        OwedToUserId = share.OwesUserId,
                        Balance = share.AmountOwed
                    };
                     _context.UserBalances.Add(balanceEntry);
                }
                else{
                    BalanceEntry.Balance += share.AmountOwed;
                }
            }

            // Save user balances
            await _context.SaveChangesAsync(); 

            //create a new Expense Share
            var expenseShares = createExpenseDto.ExpenseShares.Select(es => new ExpenseShare
            {
                ExpenseId = newExpense.Id,
                UserId = es.UserId,
                OwesUserId = es.OwesUserId,
                AmountOwed = es.AmountOwed,
                ShareType = es.ShareType
            }).ToList();


            _context.ExpenseShares.AddRange(expenseShares);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Return success
            return Ok(new { Message = "Expense created successfully", ExpenseId = newExpense.Id });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { Message = "An error occurred while creating the expense", Error = ex.Message });
        }
    }
}