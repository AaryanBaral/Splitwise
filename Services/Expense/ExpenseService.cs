using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Controllers;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Services.Expense;

public class ExpenseService : IExpenseService
{
    private readonly ILogger<ExpenseController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<CustomUsers> _userManager;
    private const string EqualShareType = "EQUAL";
    private const string UnequalShareType = "UNEQUAL";
    private const string PercentageShareType = "PERCENTAGE";


    public ExpenseService(ILogger<ExpenseController> logger, AppDbContext context,
        UserManager<CustomUsers> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<ResponseResults<string>> UpdateExpense(string expenseId, UpdateExpenseDto updateExpenseDto)
    {
        var expense = await _context.Expenses
            .Include(e => e.ExpenseShares)
            .Include(e => e.Payers)
            .FirstOrDefaultAsync(x => x.Id == expenseId);
        if (expense is null)
            return new ResponseResults<string>()
            {
                Success = false,
                StatusCode = 400,
                Errors = "Expense not found."
            };
        if (expense.Description != updateExpenseDto.Description)
        {
            expense.Description = updateExpenseDto.Description;
        }

        expense.Amount = updateExpenseDto.Amount;
        var expenseSharedMembers = expense.ExpenseShares.Select(es => es.OwedByUserId).Distinct().ToList();
        var payers = expense.Payers.Select(e => e.PayerId).ToList();

        foreach (var member in expenseSharedMembers)
        {
            Console.WriteLine(member);
        }

        return new ResponseResults<string>()
        {
            Success = true,
            StatusCode = 200
        };
    }

    public async Task<ResponseResults<string>> CreateExpenseAsync(CreateExpenseDto createExpenseDto)
    {
        //calculating and validating the provided data
        var total = createExpenseDto.Amount;

        // Validate Group
        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == createExpenseDto.GroupId);
        if (group is null)
        {
            return new ResponseResults<string>()
            {
                Errors = "Group does not exist",
                Success = false,
                StatusCode = 400
            };
        }


        // Validate Share Participants
        var userIdsInGroup = group.GroupMembers.Select(gm => gm.UserId).ToHashSet();
        var invalidUsers = createExpenseDto.ExpenseSharedMembers.Where(es => !userIdsInGroup.Contains(es.UserId))
            .ToList();
        if (invalidUsers.Count != 0)
        {
            return new ResponseResults<string>()
            {
                Errors = "Members provided does not exist in group",
                Success = false,
                StatusCode = 400
            };
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

            if (createExpenseDto.ShareType.Equals("equal", StringComparison.OrdinalIgnoreCase))
            {
                if (createExpenseDto.PayerId != null)
                {
                    await EqualAndSinglePayer(createExpenseDto, group, newExpense);
                    await transaction.CommitAsync();
                    return new ResponseResults<string>()
                    {
                        Success = true,
                        Data = newExpense.Id,
                        StatusCode = 200
                    };
                }

                await EqualAndMultiPayer(createExpenseDto, group, newExpense);
                await transaction.CommitAsync();
                return new ResponseResults<string>()
                {
                    Success = true,
                    Data = newExpense.Id,
                    StatusCode = 200
                };
            }

            if (createExpenseDto.ShareType.Equals("unequal", StringComparison.OrdinalIgnoreCase))
            {
                if (createExpenseDto.PayerId != null)
                {
                    await UnequalAndSinglePayer(createExpenseDto, group, newExpense);
                    await transaction.CommitAsync();
                    return new ResponseResults<string>()
                    {
                        Success = true,
                        Data = newExpense.Id,
                        StatusCode = 200
                    };
                }

                await UnequalAndMultiPayer(createExpenseDto, group, newExpense);
                await transaction.CommitAsync();
                return new ResponseResults<string>()
                {
                    Success = true,
                    Data = newExpense.Id,
                    StatusCode = 200
                };
            }

            if (createExpenseDto.ShareType.Equals("percentage", StringComparison.OrdinalIgnoreCase))
            {
                if (createExpenseDto.PayerId != null)
                {
                    await PercentageAndSinglePayer(createExpenseDto, group, newExpense);
                    await transaction.CommitAsync();
                    return new ResponseResults<string>()
                    {
                        Success = true,
                        Data = newExpense.Id,
                        StatusCode = 200
                    };
                }

                await PercentageAndMultiPayer(createExpenseDto, group, newExpense);
                await transaction.CommitAsync();
                return new ResponseResults<string>()
                {
                    Success = true,
                    Data = newExpense.Id,
                    StatusCode = 200
                };
            }

            return new ResponseResults<string>()
            {
                Success = false,
                Errors = "Share type is not valid",
                StatusCode = 400
            };
        }
        catch (CustomException ex)
        {
            await transaction.RollbackAsync();
            return new ResponseResults<string>()
            {
                Errors = ex.Errors,
                StatusCode = ex.StatusCode,
                Success = false
            };
        }
    }

    public async Task<ResponseResults<ReadTestExpenseDto>> GetExpenseAsync(string expenseId)
    {
        // Fetch the expense and related data in a single query with no tracking
        var expense = await _context.Expenses
            .Include(e => e.ExpenseShares)
            .Include(e => e.Payers)
            .Include(e => e.Group)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
        {
            return new ResponseResults<ReadTestExpenseDto>()
            {
                Success = false,
                Errors = "Expense does not exist",
                StatusCode = 400
            };
        }

        // Fetch user balances once and apply AsNoTracking
        var userBalances = await _context.UserBalances
            .Where(e => e.GroupId == expense.GroupId)
            .AsNoTracking()
            .ToListAsync();

        // Prepare data transformations in sequential loops to avoid concurrency issues
        var expensePayers = new List<AbstractReadUserDto>();
        foreach (var payer in expense.Payers)
        {
            var user = await _userManager.FindByIdAsync(payer.PayerId);
            if (user == null)
                return new ResponseResults<ReadTestExpenseDto>()
                {
                    Success = false,
                    Errors = "members in the expense does not exist",
                    StatusCode = 400
                };
            expensePayers.Add(new AbstractReadUserDto
            {
                Id = payer.Id,
                UserName = user.UserName
            });
        }

        var allUserBalances = new List<ReadUserBalanceDto>();
        foreach (var ub in userBalances)
        {
            var user = await _userManager.FindByIdAsync(ub.OwedByUserId);
            var owedTo = await _userManager.FindByIdAsync(ub.OwesToUserId);

            if (user == null || owedTo == null || user.UserName == null || owedTo.UserName == null)
                return new ResponseResults<ReadTestExpenseDto>()
                {
                    Success = false,
                    Errors = "User in the expense does not exist",
                    StatusCode = 400
                };

            allUserBalances.Add(new ReadUserBalanceDto
            {
                UserId = user.Id,
                OwedToUserId = owedTo.Id,
                UserName = user.UserName,
                OwedToUserName = owedTo.UserName,
                Amount = ub.Balance
            });
        }

        var expenseShares = new List<ReadExpenseShareDto>();
        foreach (var es in expense.ExpenseShares)
        {
            var user = await _userManager.FindByIdAsync(es.OwedByUserId);
            var owesUser = await _userManager.FindByIdAsync(es.OwesToUserId);

            if (user == null || owesUser == null)
                return new ResponseResults<ReadTestExpenseDto>()
                {
                    Success = false,
                    Errors = "Expense does not exist",
                    StatusCode = 400
                };

            expenseShares.Add(new ReadExpenseShareDto
            {
                User = new AbstractReadUserDto
                {
                    Id = user.Id,
                    UserName = user.UserName
                },
                OwesUser = new AbstractReadUserDto
                {
                    Id = owesUser.Id,
                    UserName = owesUser.UserName
                },
                AmountOwed = es.AmountOwed,
                ShareType = es.ShareType
            });
        }

        // Populate the DTO
        var readExpenseDto = new ReadTestExpenseDto
        {
            ExpenseShares = expenseShares,
            Payers = expensePayers,
            UserBalance = allUserBalances,
            ExpenseId = expense.Id,
            GroupId = expense.GroupId,
            Amount = expense.Amount,
            Date = expense.Date,
            Description = expense.Description
        };
        return new ResponseResults<ReadTestExpenseDto>()
        {
            Success = true,
            Data = readExpenseDto,
            StatusCode = 200
        };
    }

    public async Task<ResponseResults<List<ReadAllExpenseDto>>> GetAllExpenses(string groupId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group is null)
        {
            return new ResponseResults<List<ReadAllExpenseDto>>()
            {
                Errors = "Group does not exist",
                StatusCode = 400,
                Success = false
            };
        }

        var allExpenses = await _context.Expenses.Where(e => e.GroupId == groupId).ToListAsync();
        if (allExpenses.Count == 0)
        {
            return new ResponseResults<List<ReadAllExpenseDto>>()
            {
                Errors = "No Expenses Found",
                StatusCode = 400,
                Success = false
            };
        }

        var readAllExpense = allExpenses.Select(e => new ReadAllExpenseDto()
        {
            Date = e.Date,
            Amount = e.Amount,
            GroupId = e.GroupId,
            ExpenseId = e.Id,
            Description = e.Description
        }).ToList();

        return new ResponseResults<List<ReadAllExpenseDto>>()
        {
            Success = true,
            Data = readAllExpense,
            StatusCode = 200
        };
    }

    private async Task EqualAndMultiPayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            if (createExpenseDto.Payers is null)
                throw new CustomException()
                {
                    Errors = "You must provide at least one payer",
                    StatusCode = 400
                };
            var sharedAmount = createExpenseDto.Amount / createExpenseDto.ExpenseSharedMembers.Count;
            var totalPaid = createExpenseDto.Payers.Sum(p => p.Share);
            if (totalPaid != createExpenseDto.Amount)
            {
                throw new CustomException()
                {
                    Errors = "The total amount doesnt match the total amount paid by the users",
                    StatusCode = 400
                };
            }

            var payerIds = createExpenseDto.Payers.Select(p => p.UserId).ToList();
            var payerInGroups = group.GroupMembers
                .Where(gm => payerIds.Contains(gm.UserId))
                .ToList();
            if (payerIds.Count != payerInGroups.Count)
            {
                throw new CustomException()
                {
                    Errors = "One or more payers are not in the group",
                    StatusCode = 400
                };
            }

            List<ExpensePayers> expensePayersList = [];
            List<ExpenseShares> expenseShares = [];

            // for each payer in payer expense each member owes to the payer
            foreach (var payer in createExpenseDto.Payers)
            {
                var payerUser = await _userManager.FindByIdAsync(payer.UserId);
                if (payerUser is null)
                    throw new CustomException()
                    {
                        Errors = "Payer user does not exist",
                        StatusCode = 400
                    };
                expensePayersList.Add(new ExpensePayers
                {
                    PayerId = payerUser.Id,
                    ExpenseId = newExpense.Id,
                    Expense = newExpense,
                    Payer = payerUser,
                    AmountPaid = payer.Share
                });
                var payerShare = payer.Share;
                var proportionOfDebtCovered = payerShare / totalPaid;
                foreach (var member in createExpenseDto.ExpenseSharedMembers)
                {
                    if (payer.UserId == member.UserId)
                    {
                        continue;
                    }

                    var memberUser = await _userManager.FindByIdAsync(member.UserId) ?? throw new CustomException()
                    {
                        Errors = "user does not exist",
                        StatusCode = 400
                    };
                    var amountOwedFromPayer = sharedAmount * proportionOfDebtCovered;

                    //creating expense share
                    expenseShares.Add(new ExpenseShares
                    {
                        Expense = newExpense,
                        ExpenseId = newExpense.Id,
                        OwedByUserId = member.UserId,
                        OwedByUser = memberUser,
                        OwesToUser = payerUser,
                        OwesToUserId = payer.UserId,
                        AmountOwed = amountOwedFromPayer,
                        ShareType = EqualShareType
                    });

                    //finding a user balance if exists
                    var userBalance = await _context.UserBalances.FirstOrDefaultAsync(
                        b => b.OwedByUserId == member.UserId &&
                             b.OwesToUserId == payer.UserId &&
                             b.GroupId == group.Id
                    );

                    // if it doesn't exist create a new one with the owed amount
                    if (userBalance is null)
                    {
                        var newUserBalance = new UserBalances
                        {
                            GroupId = group.Id,
                            Group = group,
                            OwedByUserId = member.UserId,
                            OwesToUserId = payer.UserId,
                            Balance = amountOwedFromPayer,
                            OwedByUser = memberUser,
                            OwesToUser = payerUser
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
            await _context.ExpensePayers.AddRangeAsync(expensePayersList);

            await _context.SaveChangesAsync();
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Errors}",
                StatusCode = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = 500
            };
        }
    }

    private async Task EqualAndSinglePayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            if (createExpenseDto.PayerId is null)
                throw new CustomException()
                {
                    Errors = "You must provide at least one payer",
                    StatusCode = 400
                };
            var sharedAmount = createExpenseDto.Amount / createExpenseDto.ExpenseSharedMembers.Count;
            var payer = await _userManager.FindByIdAsync(createExpenseDto.PayerId) ?? throw new CustomException()
            {
                Errors = "Payer does not exist",
                StatusCode = 400
            };
            await _context.ExpensePayers.AddAsync(new ExpensePayers()
            {
                Expense = newExpense,
                ExpenseId = newExpense.Id,
                Payer = payer,
                PayerId = payer.Id,
                AmountPaid = createExpenseDto.Amount
            });
            List<ExpenseShares> expenseShares = [];
            foreach (var member in createExpenseDto.ExpenseSharedMembers)
            {
                var user = await _userManager.FindByIdAsync(member.UserId) ?? throw new CustomException()
                {
                    Errors = "User does not exist",
                    StatusCode = 400
                };
                var userBalance = await _context.UserBalances
                    .FirstOrDefaultAsync(
                        b => b.OwedByUserId == member.UserId &&
                             b.OwesToUserId == payer.Id &&
                             b.GroupId == createExpenseDto.GroupId);

                //check if the User Balance exists or not
                if (userBalance is null)
                {
                    var newBalanceEntry = new UserBalances
                    {
                        GroupId = group.Id,
                        Group = group,
                        OwedByUserId = member.UserId,
                        OwesToUserId = payer.Id,
                        Balance = sharedAmount,
                        OwedByUser = user,
                        OwesToUser = payer
                    };
                    _context.UserBalances.Add(newBalanceEntry);
                    await _context.SaveChangesAsync();
                }

                //if exists do calculations
                else
                {
                    userBalance.Balance += sharedAmount;
                }

                expenseShares.Add(new ExpenseShares()
                {
                    Expense = newExpense,
                    ExpenseId = newExpense.Id,
                    OwedByUserId = member.UserId,
                    OwedByUser = user,
                    OwesToUser = user,
                    OwesToUserId = payer.Id,
                    AmountOwed = sharedAmount,
                    ShareType = EqualShareType
                });
            }

            await _context.ExpenseShares.AddRangeAsync(expenseShares);
            await _context.SaveChangesAsync();
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Errors}",
                StatusCode = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = 500
            };
        }
    }

    private async Task UnequalAndSinglePayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            if (createExpenseDto.ExpenseSharedMembers.Sum(m => m.Share) != createExpenseDto.Amount)
            {
                throw new CustomException()
                {
                    Errors = "Share split doesnt match the total amount",
                    StatusCode = 400
                };
            }

            if (createExpenseDto.PayerId is null)
                throw new CustomException()
                {
                    Errors = "You must provide at least one payer",
                    StatusCode = 400
                };
            var payer = await _userManager.FindByIdAsync(createExpenseDto.PayerId) ?? throw new CustomException()
            {
                Errors = "User does not exist",
                StatusCode = 400
            };
            var expensePayer = new ExpensePayers()
            {
                Payer = payer,
                PayerId = payer.Id,
                Expense = newExpense,
                ExpenseId = newExpense.Id,
                AmountPaid = createExpenseDto.Amount
            };
            _context.ExpensePayers.Add(expensePayer);
            await _context.SaveChangesAsync();

            List<ExpenseShares> expenseShares = [];
            foreach (var member in createExpenseDto.ExpenseSharedMembers)
            {
                var user = await _userManager.FindByIdAsync(member.UserId) ?? throw new CustomException()
                {
                    Errors = "User does not exist",
                    StatusCode = 400
                };
                var userBalance = await _context.UserBalances.FirstOrDefaultAsync(ub =>
                    ub.OwedByUserId == member.UserId &&
                    ub.OwesToUserId == payer.Id &&
                    ub.GroupId == createExpenseDto.GroupId);

                //check if the User Balance exists or not
                if (userBalance is null)
                {
                    var newBalanceEntry = new UserBalances
                    {
                        GroupId = group.Id,
                        Group = group,
                        OwedByUserId = member.UserId,
                        OwesToUserId = payer.Id,
                        Balance = member.Share,
                        OwedByUser = user,
                        OwesToUser = payer
                    };
                    _context.UserBalances.Add(newBalanceEntry);
                    await _context.SaveChangesAsync();
                }

                //if exists do calculations
                else
                {
                    userBalance.Balance += member.Share;
                }

                expenseShares.Add(new ExpenseShares()
                {
                    Expense = newExpense,
                    ExpenseId = newExpense.Id,
                    OwesToUser = payer,
                    OwesToUserId = payer.Id,
                    OwedByUser = user,
                    OwedByUserId = user.Id,
                    AmountOwed = member.Share,
                    ShareType = UnequalShareType
                });
            }

            await _context.ExpenseShares.AddRangeAsync(expenseShares);
            await _context.SaveChangesAsync();
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Errors}",
                StatusCode = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = 500
            };
        }
    }

    private async Task UnequalAndMultiPayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            if (createExpenseDto.Payers is null || createExpenseDto.Payers.Count == 0)
                throw new CustomException()
                {
                    Errors = "You must provide at least one payer",
                    StatusCode = 400,
                };
            var memberTotal = createExpenseDto.ExpenseSharedMembers.Sum(m => m.Share);
            if (memberTotal != createExpenseDto.Amount)
                throw new CustomException()
                {
                    Errors = "Share split doesnt match the total amount",
                    StatusCode = 400
                };
            var total = createExpenseDto.Payers.Sum(e => e.Share);
            if (total != createExpenseDto.Amount)
                throw new CustomException()
                {
                    Errors = "The payer payed amount doesn't match the total amount",
                    StatusCode = 400
                };
            var payerIds = createExpenseDto.Payers.Select(p => p.UserId).ToList();
            var payerInGroups = group.GroupMembers
                .Where(gm => payerIds.Contains(gm.UserId))
                .ToList();
            if (payerIds.Count != payerInGroups.Count)
            {
                throw new CustomException()
                {
                    Errors = "One or more payers are not in the group",
                    StatusCode = 400
                };
            }

            List<ExpensePayers> expensePayersList = [];
            List<ExpenseShares> expenseShares = [];
            foreach (var payer in createExpenseDto.Payers)
            {
                var payerUser = await _userManager.FindByIdAsync(payer.UserId) ?? throw new CustomException()
                {
                    Errors = "User does not exist",
                    StatusCode = 400
                };
                expensePayersList.Add(new ExpensePayers
                {
                    PayerId = payerUser.Id,
                    ExpenseId = newExpense.Id,
                    Expense = newExpense,
                    Payer = payerUser,
                    AmountPaid = payer.Share
                });
                var payerShare = payer.Share;
                var proportionOfDebtCovered = payerShare / total;
                foreach (var member in createExpenseDto.ExpenseSharedMembers)
                {
                    if (payer.UserId == member.UserId)
                    {
                        continue;
                    }

                    var memberUser = await _userManager.FindByIdAsync(member.UserId) ?? throw new CustomException()
                    {
                        Errors = "user does not exist",
                        StatusCode = 400
                    };
                    var amountOwedFromPayer = member.Share * proportionOfDebtCovered;

                    //creating expense share
                    expenseShares.Add(new ExpenseShares
                    {
                        Expense = newExpense,
                        ExpenseId = newExpense.Id,
                        OwedByUserId = member.UserId,
                        OwedByUser = memberUser,
                        OwesToUser = payerUser,
                        OwesToUserId = payer.UserId,
                        AmountOwed = amountOwedFromPayer,
                        ShareType = UnequalShareType
                    });

                    //finding a user balance if exists
                    var userBalance = await _context.UserBalances.FirstOrDefaultAsync(
                        b => b.OwedByUserId == member.UserId &&
                             b.OwesToUserId == payer.UserId &&
                             b.GroupId == group.Id
                    );

                    // if it doesn't exist create a new one with the owed amount
                    if (userBalance is null)
                    {
                        var newUserBalance = new UserBalances
                        {
                            GroupId = group.Id,
                            Group = group,
                            OwedByUserId = member.UserId,
                            OwesToUserId = payer.UserId,
                            Balance = amountOwedFromPayer,
                            OwedByUser = memberUser,
                            OwesToUser = payerUser
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

            await _context.ExpensePayers.AddRangeAsync(expensePayersList);
            await _context.ExpenseShares.AddRangeAsync(expenseShares);
            await _context.SaveChangesAsync();
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Errors}",
                StatusCode = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = 500
            };
        }
    }

    private async Task PercentageAndSinglePayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            var total = createExpenseDto.Amount;
            var totalPercentage = createExpenseDto.ExpenseSharedMembers.Sum(e => e.Share);
            if (totalPercentage != 100)
            {
                throw new CustomException()
                {
                    Errors = "The percentage distribution doesn't sum up to 100",
                    StatusCode = 400
                };
            }

            if (createExpenseDto.PayerId is null)
            {
                throw new CustomException()
                {
                    Errors = "You must provide at least one payer",
                    StatusCode = 400,
                };
            }

            var payer = await _userManager.FindByIdAsync(createExpenseDto.PayerId) ?? throw new CustomException()
            {
                Errors = "User does not exist",
                StatusCode = 400
            };
            var expensePayer = new ExpensePayers()
            {
                PayerId = payer.Id,
                Payer = payer,
                AmountPaid = createExpenseDto.Amount,
                Expense = newExpense,
                ExpenseId = newExpense.Id,
            };
            _context.ExpensePayers.Add(expensePayer);
            await _context.SaveChangesAsync();
            List<ExpenseShares> expenseSharesList = [];
            foreach (var member in createExpenseDto.ExpenseSharedMembers)
            {
                var user = await _userManager.FindByIdAsync(member.UserId) ?? throw new CustomException()
                {
                    Errors = "User does not exist",
                    StatusCode = 400
                };
                var amountOfPercentage = member.Share * total / (decimal)100;
                var userBalance = await _context.UserBalances.FirstOrDefaultAsync(ub =>
                    ub.OwedByUserId == member.UserId &&
                    ub.OwesToUserId == payer.Id &&
                    ub.GroupId == group.Id
                );
                if (userBalance is null)
                {
                    var newUserBalance = new UserBalances()
                    {
                        GroupId = group.Id,
                        Group = group,
                        OwesToUser = payer,
                        OwesToUserId = payer.Id,
                        OwedByUserId = member.UserId,
                        OwedByUser = user,
                        Balance = amountOfPercentage
                    };
                    _context.UserBalances.Add(newUserBalance);
                }
                else
                {
                    userBalance.Balance += amountOfPercentage;
                }

                expenseSharesList.Add(new ExpenseShares()
                {
                    Expense = newExpense,
                    ExpenseId = newExpense.Id,
                    ShareType = PercentageShareType,
                    OwedByUserId = member.UserId,
                    OwedByUser = user,
                    OwesToUser = payer,
                    OwesToUserId = payer.Id,
                    AmountOwed = amountOfPercentage
                });
            }

            await _context.ExpenseShares.AddRangeAsync(expenseSharesList);
            await _context.SaveChangesAsync();
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Errors}",
                StatusCode = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = 500
            };
        }
    }

    private async Task PercentageAndMultiPayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            ValidateExpenseData(createExpenseDto);
            var total = createExpenseDto.Payers.Sum(p => p.Share);
            List<ExpenseShares> expenseSharesList = [];
            List<ExpensePayers> expensePayersList = [];

            foreach (var payer in createExpenseDto.Payers)
            {
                var payerUser = await _userManager.FindByIdAsync(payer.UserId) ?? throw new CustomException()
                {
                    Errors = "User does not exist",
                    StatusCode = 400
                };
                expensePayersList.Add(new ExpensePayers
                {
                    PayerId = payerUser.Id,
                    ExpenseId = newExpense.Id,
                    Expense = newExpense,
                    Payer = payerUser,
                    AmountPaid = payer.Share
                });
                var payerShare = payer.Share;
                var proportionOfDebtCovered = payerShare / total;
                foreach (var member in createExpenseDto.ExpenseSharedMembers)
                {
                    if (payer.UserId == member.UserId)
                    {
                        continue;
                    }

                    var user = await _userManager.FindByIdAsync(member.UserId) ?? throw new CustomException()
                    {
                        Errors = "User does not exist",
                        StatusCode = 400
                    };
                    var percentageOfShare = member.Share * total / (decimal)100;
                    var amountOwedFromPayer = percentageOfShare * proportionOfDebtCovered;
                    //creating expense share
                    expenseSharesList.Add(new ExpenseShares
                    {
                        Expense = newExpense,
                        ExpenseId = newExpense.Id,
                        OwedByUserId = member.UserId,
                        OwedByUser = user,
                        OwesToUser = payerUser,
                        OwesToUserId = payer.UserId,
                        AmountOwed = amountOwedFromPayer,
                        ShareType = PercentageShareType
                    });

                    //finding a user balance if exists
                    var userBalance = await _context.UserBalances.FirstOrDefaultAsync(
                        b => b.OwedByUserId == member.UserId &&
                             b.OwesToUserId == payer.UserId &&
                             b.GroupId == group.Id
                    );

                    // if it doesn't exist create a new one with the owed amount
                    if (userBalance is null)
                    {
                        var newUserBalance = new UserBalances
                        {
                            GroupId = group.Id,
                            Group = group,
                            OwedByUserId = member.UserId,
                            OwesToUserId = payer.UserId,
                            Balance = amountOwedFromPayer,
                            OwedByUser = user,
                            OwesToUser = payerUser
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

            await _context.ExpenseShares.AddRangeAsync(expenseSharesList);
            await _context.ExpensePayers.AddRangeAsync(expensePayersList);
            await _context.SaveChangesAsync();
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Errors}",
                StatusCode = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = 500
            };
        }
    }


    public async Task PercentageShare(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            ValidateExpenseData(createExpenseDto);
            var total = createExpenseDto.Payers.Sum(p => p.Share);
            List<ExpenseShares> expenseSharesList = [];
            List<ExpensePayers> expensePayersList = [];
            foreach (var payerDto in createExpenseDto.Payers)
            {
                var payerUser = await GetUserOrThrowAsync(payerDto.UserId);
                var expensePayer = CreateExpensePayer(newExpense, payerUser, payerDto.Share);
                expensePayersList.Add(expensePayer);
                await AddExpenseSharesForPayer(createExpenseDto, payerDto, total, expenseSharesList, payerUser, newExpense, group);
            }
            await _context.ExpenseShares.AddRangeAsync(expenseSharesList);
            await _context.ExpensePayers.AddRangeAsync(expensePayersList);
            await _context.SaveChangesAsync();
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Errors}",
                StatusCode = 500
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new CustomException()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = 500
            };
        }
    }


    private void ValidateExpenseData(CreateExpenseDto createExpenseDto)
    {
        if (!createExpenseDto.Payers.Any())
            throw new CustomException { Errors = "You must provide at least one payer", StatusCode = 400 };

        if (createExpenseDto.Payers.Sum(p => p.Share) != createExpenseDto.Amount)
            throw new CustomException
                { Errors = "Total amount paid by payers does not match the expense amount", StatusCode = 400 };

        if (createExpenseDto.ExpenseSharedMembers.Sum(e => e.Share) != 100)
            throw new CustomException
                { Errors = "The percentage distribution doesn't sum up to 100", StatusCode = 400 };
    }

    private ExpensePayers CreateExpensePayer(Expenses newExpense, CustomUsers payerUser, decimal amountPaid)
    {
        return new ExpensePayers
        {
            PayerId = payerUser.Id,
            ExpenseId = newExpense.Id,
            Expense = newExpense,
            Payer = payerUser,
            AmountPaid = amountPaid
        };
    }

    private async Task<CustomUsers> GetUserOrThrowAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId) ?? throw new CustomException
        {
            Errors = "User does not exist",
            StatusCode = 400
        };
    }

    // Adds ExpenseShares for each shared member related to a specific payer
    private async Task AddExpenseSharesForPayer(
        CreateExpenseDto createExpenseDto,
        ExpensePayer payerDto,
        decimal total,
        List<ExpenseShares> expenseSharesList,
        CustomUsers payerUser,
        Expenses newExpense,
        Groups group)
    {
        var payerShare = payerDto.Share;
        var proportionOfDebtCovered = payerShare / total;

        foreach (var member in createExpenseDto.ExpenseSharedMembers)
        {
            if (payerDto.UserId == member.UserId)
                continue;

            var memberUser = await GetUserOrThrowAsync(member.UserId);
            var amountOwed = proportionOfDebtCovered * member.Share;

            var expenseShare = new ExpenseShares
            {
                Expense = newExpense,
                ExpenseId = newExpense.Id,
                OwedByUserId = member.UserId,
                OwedByUser = memberUser,
                OwesToUser = payerUser,
                OwesToUserId = payerDto.UserId,
                AmountOwed = amountOwed,
                ShareType = createExpenseDto.ShareType.ToUpper()
            };
            expenseSharesList.Add(expenseShare);

            await UpdateUserBalanceAsync(memberUser, payerUser, group, amountOwed);
        }
    }
    

// Updates or creates a UserBalance record
    private async Task UpdateUserBalanceAsync(CustomUsers owedByUser, CustomUsers owesToUser, Groups group,
        decimal amountOwed)
    {
        var userBalance = await _context.UserBalances
            .FirstOrDefaultAsync(b => b.OwedByUserId == owedByUser.Id &&
                                      b.OwesToUserId == owesToUser.Id &&
                                      b.GroupId == group.Id);

        if (userBalance == null)
        {
            var newUserBalance = new UserBalances
            {
                GroupId = group.Id,
                Group = group,
                OwedByUserId = owedByUser.Id,
                OwesToUserId = owesToUser.Id,
                Balance = amountOwed,
                OwedByUser = owedByUser,
                OwesToUser = owesToUser
            };
            await _context.UserBalances.AddAsync(newUserBalance);
        }
        else
        {
            userBalance.Balance += amountOwed;
        }
    }

    private void ConvertPercentShareToAmount(List<ExpenseSharedMembers> expenseSharedMembers, decimal total)
    {
        foreach (var member in expenseSharedMembers)
        {
            member.Share = member.Share *total/100;
        }
    }
}