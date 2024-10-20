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
            .Include(e => e.Payer)
            .Include(e => e.Group)
            .Include(e => e.ExpenseShares)
            .ThenInclude(es => es.User)
            .Include(e => e.ExpenseShares)
            .ThenInclude(es => es.Expense)
            .Include(e => e.ExpenseShares)
            .ThenInclude(e => e.OwesUser)
            .FirstOrDefaultAsync();

        if (expense is null)
        {
            return NotFound("Expense not found");
        }
        if (expense.Group is null)
        {
            return BadRequest("The expense does not belong to any group.");
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
            ExpenseId = expense.Id,
            GroupId = expense.GroupId,
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
    [HttpGet]
    public async Task<IActionResult> GetAllExpense([FromQuery] string groupId)
    {
        if (groupId is null)
        {
            return BadRequest("Please provide groupId");
        }
        var expense = await _context.Expenses
        //start coding from here
        .FirstOrDefaultAsync(e => e.GroupId == groupId);
        return Ok("");
    }

    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto createExpenseDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        decimal total = 0;
        foreach (var expenseShare in createExpenseDto.ExpenseShares)
        {
            total += expenseShare.AmountOwed;
        }
        if (total != createExpenseDto.Amount)
        {
            return BadRequest("The expense shared amount is not valid");
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
                Group = group,
                Payer = payer,
                Description = createExpenseDto.Description
            };
            var expenseShares = new List<ExpenseShare>();
            _context.Expenses.Add(newExpense);
            await _context.SaveChangesAsync();

            //Get User Balance for each share
            foreach (var share in createExpenseDto.ExpenseShares)
            {
                var user = await _userManager.FindByIdAsync(share.UserId);
                var owesUser = await _userManager.FindByIdAsync(share.OwesUserId);
                if (user is null || owesUser is null)
                {
                    throw new Exception("Please Provide");
                }
                var BalanceEntry = await _context.UserBalances
                .FirstOrDefaultAsync(b => b.UserId == share.UserId && b.OwedToUserId == share.OwesUserId);
                // if no Balance entry then create a new one
                if (BalanceEntry is null)
                {
                    //creating a balance entry
                    var balanceEntry = new UserBalance
                    {
                        ExpenseId = newExpense.Id,
                        Expense = newExpense,
                        UserId = share.UserId,
                        OwedToUserId = share.OwesUserId,
                        Balance = share.AmountOwed,
                        User = user,
                        OwedToUser = owesUser
                    };
                    _context.UserBalances.Add(balanceEntry);
                }
                else
                {
                    BalanceEntry.Balance += share.AmountOwed;
                }
                //create expense 
                expenseShares.Add(new ExpenseShare
                {
                    ExpenseId = newExpense.Id,
                    UserId = share.UserId,
                    OwesUserId = share.OwesUserId,
                    AmountOwed = share.AmountOwed,
                    ShareType = share.ShareType,
                    User = user,
                    Expense = newExpense,
                    OwesUser = owesUser
                });
            }

            // Save user balances and expense share
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

    [HttpGet]
    [Route("settle/{id}")]
    public async Task<IActionResult> GetExpenseSettlement(string id)
    {
        var expenseShares = await _context.ExpenseShares
        .Where(es => es.ExpenseId == id)
        .ToListAsync();

        var userBalances = await _context.UserBalances
        .Where(ub => ub.ExpenseId == id)
        .Include(ub => ub.OwedToUser)
        .Include(ub => ub.User)
        .ToListAsync();

        Dictionary<string, decimal> dbUserBalanceSheet = [];
        Dictionary<string, decimal> userBalanceSheet = [];

        // who owes how much and who is owed how much using expense shares
        foreach (var expenseShare in expenseShares)
        {
            if (!userBalanceSheet.ContainsKey(expenseShare.UserId))
            {
                userBalanceSheet[expenseShare.UserId] = 0;
            }
            if (!userBalanceSheet.ContainsKey(expenseShare.OwesUserId))
            {
                userBalanceSheet[expenseShare.UserId] = 0;
            }
            userBalanceSheet[expenseShare.UserId] -= expenseShare.AmountOwed;
            userBalanceSheet[expenseShare.OwesUserId] += expenseShare.AmountOwed;
        }

        foreach (var userBalance in userBalances)
        {
            if (!userBalanceSheet.ContainsKey(userBalance.UserId))
            {
                userBalanceSheet[userBalance.UserId] = 0;
            }
            if (!userBalanceSheet.ContainsKey(userBalance.OwedToUserId))
            {
                userBalanceSheet[userBalance.UserId] = 0;
            }
            userBalanceSheet[userBalance.UserId] -= userBalance.Balance;
            userBalanceSheet[userBalance.OwedToUserId] += userBalance.Balance;
        }
        if (userBalanceSheet.Count != dbUserBalanceSheet.Count) return StatusCode(500,"The error ocoured beacuse the datat is not accurate ");
        foreach (var dbKey in dbUserBalanceSheet){
            if (userBalanceSheet.TryGetValue(dbKey.Key, out decimal value)) return StatusCode(500, "The error ocoured beacuse the datat is not accurate ");
            if(!dbKey.Value.Equals(value)) return StatusCode(500,"The error ocoured beacuse the datat is not accurate ");
        }







        return Ok("");
    }
}