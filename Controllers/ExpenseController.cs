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

namespace Splitwise_Back.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class ExpenseController : ControllerBase
{
    private readonly ILogger<ExpenseController> _logger;
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly UserManager<CustomUsers> _userManager;

    public ExpenseController(ILogger<ExpenseController> logger, AppDbContext context, UserManager<CustomUsers> userManager, IMapper mapper)
    {
        _logger = logger;
        _context = context;
        _mapper = mapper;
        _userManager = userManager;
    }
    // [HttpGet]
    // [Route("{id}")]
    // public async Task<IActionResult> GetExpenseById(string id)
    // {
    //     if (id is null)
    //     {
    //         return BadRequest("Please provide the expense id");
    //     }

    //     var expense = await _context.Expenses
    //         .Where(e => e.Id == id)
    //         .Include(e => e.Payer)
    //         .Include(e => e.Group)
    //         .Include(e => e.ExpenseShares)
    //         .ThenInclude(es => es.User)
    //         .Include(e => e.ExpenseShares)
    //         .ThenInclude(es => es.Expense)
    //         .Include(e => e.ExpenseShares)
    //         .ThenInclude(e => e.OwesUser)
    //         .FirstOrDefaultAsync();

    //     if (expense is null)
    //     {
    //         return NotFound("Expense not found");
    //     }
    //     if (expense.Group is null)
    //     {
    //         return BadRequest("The expense does not belong to any group.");
    //     }

    //     var ExpenseShareForExpense = expense.ExpenseShares.Select(es => new ExpenseShareForExpense()
    //     {
    //         AmountOwed = es.AmountOwed,
    //         ShareType = es.ShareType,
    //         User = new AbstractReadUserDto()
    //         {
    //             UserName = es.User?.UserName,
    //             Id = es.User?.Id
    //         },
    //         OwesUser = new AbstractReadUserDto()
    //         {
    //             UserName = es.OwesUser?.UserName,
    //             Id = es.OwesUserId
    //         }
    //     }).ToList();

    //     ReadExpenseDto readExpenseDto = new()
    //     {
    //         ExpenseId = expense.Id,
    //         GroupId = expense.GroupId,
    //         Payer = new AbstractReadUserDto()
    //         {
    //             Id = expense.Payer?.Id,
    //             UserName = expense.Payer?.UserName,
    //         },
    //         Amount = expense.Amount,
    //         Date = expense.Date,
    //         Description = expense.Description,
    //         ExpenseShares = ExpenseShareForExpense
    //     };

    //     return Ok(readExpenseDto);
    // }
    // [HttpGet]
    // public async Task<IActionResult> GetAllExpense([FromQuery] string groupId)
    // {
    //     if (groupId is null)
    //     {
    //         return BadRequest("Please provide groupId");
    //     }
    //     var expense = await _context.Expenses
    //     //start coding from here
    //     .FirstOrDefaultAsync(e => e.GroupId == groupId);
    //     return Ok("");
    // }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto createExpenseDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
            
        }
        Console.WriteLine(JsonConvert.SerializeObject(createExpenseDto));
        //calculating and validating the provided data
        var total = createExpenseDto.Amount;


        // Validate Group
        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == createExpenseDto.GroupId);
        if (group is null)
        {
            return BadRequest("Invalid group ID");
        }


        // Validate Share Participants
        var userIdsInGroup = group.GroupMembers.Select(gm => gm.UserId).ToHashSet();
        var invalidUsers = createExpenseDto.ExpenseSharedMembers.Where(es => !userIdsInGroup.Contains(es.UserId)).ToList();
        if (invalidUsers.Count != 0)
        {
            return BadRequest("One or more users in the expense shares are not members of the group");
        }
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {


            //Create a new expense
            var newExpense = new Expenses
            {
                GroupId = createExpenseDto.GroupId,
                Amount = createExpenseDto.Amount,
                Date = DateTime.UtcNow,
                Group = group,
                Description = createExpenseDto.Description
            };
            var expenseShares = new List<ExpenseShares>();
            _context.Expenses.Add(newExpense);
            await _context.SaveChangesAsync();


            // for equal splitting   
            if (createExpenseDto.ShareType.Equals("EQUAL", StringComparison.CurrentCultureIgnoreCase))
            {
                var sharedAmount = createExpenseDto.Amount / createExpenseDto.ExpenseSharedMembers.Count;
                
                // for equal and single payer splitting 
                if (createExpenseDto.PayerId is not null)
                {

                    var payer = await _userManager.FindByIdAsync(createExpenseDto.PayerId);
                    
                    // if a player is null return a bad request
                    if (payer is null)
                    {
                        throw new Exception("Please provide a valid payer");
                    }

                    // creating an expense payer 
                    var expensePayer = new ExpensePayers
                    {
                        PayerId = payer.Id,
                        ExpenseId = newExpense.Id,
                        Expense = newExpense,
                        Payer = payer,
                        AmountPaid = createExpenseDto.Amount
                    };

                    //saving the expense payer
                    _context.ExpensePayers.Add(expensePayer);
                    await _context.SaveChangesAsync();

                    foreach (var sharedMember in createExpenseDto.ExpenseSharedMembers)
                    {
                        var user = await _userManager.FindByIdAsync(sharedMember.UserId);
                        
                        //throw error if user is null
                        if (user is null)
                        {
                            throw new Exception("Please Provide valid user id");
                        }
                        var balanceEntry = await _context.UserBalances
                        .FirstOrDefaultAsync(
                            b => b.UserId == sharedMember.UserId &&
                            b.OwedToUserId == payer.Id &&
                            b.GroupId == createExpenseDto.GroupId
                        );
                        
                        //check if the User Balance exists or not
                        if (balanceEntry is null)
                        {
                            var newBalanceEntry = new UserBalances
                            {
                                GroupId = group.Id,
                                Group = group,
                                UserId = sharedMember.UserId,
                                OwedToUserId = payer.Id,
                                Balance = sharedAmount,
                                User = user,
                                OwedToUser = payer
                            };
                            _context.UserBalances.Add(newBalanceEntry);
                            await _context.SaveChangesAsync();

                        }
                        
                        //if exists do calculations
                        else
                        {
                            balanceEntry.Balance += sharedAmount;
                        }
                        
                        //creating expense Shares
                        expenseShares.Add(new ExpenseShares
                        {
                            ExpenseId = newExpense.Id,
                            UserId = sharedMember.UserId,
                            OwesUserId = payer.Id,
                            AmountOwed = sharedAmount,
                            ShareType = "EQUAL",
                            User = user,
                            Expense = newExpense,
                            OwesUser = payer
                        });
                    }
                }

                // for equal and multi-player splitting
                else if (createExpenseDto.PayerId is null && createExpenseDto.Payers is not null && createExpenseDto.Payers.Count != 0)
                {
                    var totalPaid = createExpenseDto.Payers.Sum(p => p.Share);
                    if (totalPaid != createExpenseDto.Amount)
                    {
                        throw new Exception("Total amount paid by all payers does not match the total expense amount.");
                    }
                    var payerIds = createExpenseDto.Payers.Select(p => p.UserId).ToList();
                    var payerInGroups = group.GroupMembers
                        .Where(gm => payerIds.Contains(gm.UserId))
                        .ToList();
                    if (payerIds.Count != payerInGroups.Count)
                    {
                        return BadRequest("One or more payers are not in the group");
                    }
                    foreach (var payers in createExpenseDto.Payers)
                    {
                        var payer = await _userManager.FindByIdAsync(payers.UserId);
                        if (payer is null)
                        {
                            throw new Exception("please provide valid ids of payers");
                        }
                        
                        //creating Expense Payers
                        var expensePayers = new ExpensePayers
                        {
                            PayerId = payer.Id,
                            ExpenseId = newExpense.Id,
                            Expense = newExpense,
                            Payer = payer,
                            AmountPaid = payers.Share
                        };
                        _context.ExpensePayers.Add(expensePayers);
                    }
                    await _context.SaveChangesAsync();
                    
                    // for each payer in payer expense each member owes to the payer
                    foreach (var payer in createExpenseDto.Payers)
                    {
                        var payerUser = await _userManager.FindByIdAsync(payer.UserId) ?? throw new Exception("Provide valid payer id");
                        decimal payerShare = payer.Share;
                        decimal proportionOfDebtCovered = payerShare / total;
                        foreach (var member in createExpenseDto.ExpenseSharedMembers)
                        {
                            if (payer.UserId == member.UserId)
                            {
                                continue;
                            }
                            var memberUser = await _userManager.FindByIdAsync(member.UserId) ?? throw new Exception("provide valid member ids");
                            var amountOwedFromPayer = sharedAmount * proportionOfDebtCovered;
                            //creating expense share
                            expenseShares.Add(new ExpenseShares
                            {
                                Expense = newExpense,
                                ExpenseId = newExpense.Id,
                                UserId = member.UserId,
                                User = memberUser,
                                OwesUser = payerUser,
                                OwesUserId = payer.UserId,
                                AmountOwed = amountOwedFromPayer,
                                ShareType = "EQUAL"
                            });
                            
                            //finding a user balance if exists
                            var userBalance = await _context.UserBalances.FirstOrDefaultAsync(
                                b => b.UserId == member.UserId &&
                                b.OwedToUserId == payer.UserId &&
                                b.GroupId == group.Id
                            );
                            
                            // if doesn't exist create a new one with the owed amount
                            if (userBalance is null)
                            {
                                var newUserBalance = new UserBalances
                                {
                                    GroupId = group.Id,
                                    Group = group,
                                    UserId = member.UserId,
                                    OwedToUserId = payer.UserId,
                                    Balance = amountOwedFromPayer,
                                    User = memberUser,
                                    OwedToUser = payerUser
                                };
                                await _context.UserBalances.AddAsync(newUserBalance);
                            }
                            
                            //else add the owed amount to the previous amount stored
                            else
                            {
                                userBalance.Balance += amountOwedFromPayer;
                            }
                        }
                    }
                    await _context.ExpenseShares.AddRangeAsync(expenseShares);
                    await _context.SaveChangesAsync();
                }

            }

            //for unequal splitting
            else if (createExpenseDto.ShareType.Equals("UNEQUAL", StringComparison.CurrentCultureIgnoreCase))
            {
                //for unequal and single payer splitting
                if (createExpenseDto.PayerId is not null)
                {
                    var payer = await _userManager.FindByIdAsync(createExpenseDto.PayerId);
                    if (payer is null)
                    {
                        return BadRequest("Please Provide a valid payer id");
                    }
                    foreach (var sharedMember in createExpenseDto.ExpenseSharedMembers)
                    {
                        var user = await _userManager.FindByIdAsync(sharedMember.UserId);
                        if (user is null)
                        {
                            throw new Exception("Please Provide valid user id");
                        }
                        var userBalance = await _context.UserBalances
                        .FirstOrDefaultAsync(
                            b => b.UserId == sharedMember.UserId &&
                            b.OwedToUserId == payer.Id &&
                            b.GroupId == createExpenseDto.GroupId
                        );
                        if (userBalance is null)
                        {
                            var newUserBalance = new UserBalances
                            {
                                GroupId = group.Id,
                                Group = group,
                                UserId = sharedMember.UserId,
                                OwedToUserId = payer.Id,
                                Balance = sharedMember.Share,
                                User = user,
                                OwedToUser = payer
                            };
                            _context.UserBalances.Add(newUserBalance);
                        }
                        else
                        {
                            userBalance.Balance += sharedMember.Share;
                        }
                        expenseShares.Add(new ExpenseShares
                        {
                            ExpenseId = newExpense.Id,
                            UserId = sharedMember.UserId,
                            OwesUserId = payer.Id,
                            AmountOwed = sharedMember.Share,
                            ShareType = createExpenseDto.ShareType,
                            User = user,
                            Expense = newExpense,
                            OwesUser = payer
                        });
                    }
                }
            
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
}