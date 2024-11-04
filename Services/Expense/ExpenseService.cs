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
    private readonly string equalShareType = "EQUAL";
    private readonly string unequalShareType = "UNEQUAL";
    private readonly string percentageShareType = "PERCENTAGE";


    public ExpenseService(ILogger<ExpenseController> logger, AppDbContext context,
        UserManager<CustomUsers> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<ExpenseResults<string>> CreateExpenseAsync(CreateExpenseDto createExpenseDto)
    {
        //calculating and validating the provided data
        var total = createExpenseDto.Amount;

        // Validate Group
        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == createExpenseDto.GroupId);
        if (group is null)
        {
            return new ExpenseResults<string>()
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
            return new ExpenseResults<string>()
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
                    return new ExpenseResults<string>()
                    {
                        Success = true,
                        Data = newExpense.Id,
                    };
                }

                await EqualAndMultiPayer(createExpenseDto, group, newExpense);
                await transaction.CommitAsync();
                return new ExpenseResults<string>()
                {
                    Success = true,
                    Data = newExpense.Id,
                };
            }

            if (createExpenseDto.ShareType.Equals("unequal", StringComparison.OrdinalIgnoreCase))
            {
                if (createExpenseDto.PayerId != null)
                {
                    await UnequalAndSinglePayer(createExpenseDto, group, newExpense);
                    await transaction.CommitAsync();
                    return new ExpenseResults<string>()
                    {
                        Success = true,
                        Data = newExpense.Id,
                    };
                }

                await UnequalAndMultiPayer(createExpenseDto, group, newExpense);
                await transaction.CommitAsync();
                return new ExpenseResults<string>()
                {
                    Success = true,
                    Data = newExpense.Id,
                };
            }

            if (createExpenseDto.ShareType.Equals("percentage", StringComparison.OrdinalIgnoreCase))
            {
                if (createExpenseDto.PayerId != null)
                {
                    await PercentageAndSinglePayer(createExpenseDto, group, newExpense);
                    await transaction.CommitAsync();
                    return new ExpenseResults<string>()
                    {
                        Success = true,
                        Data = newExpense.Id,
                    };
                }

                await PercentageAndMultiPayer(createExpenseDto, group, newExpense);
                await transaction.CommitAsync();
                return new ExpenseResults<string>()
                {
                    Success = true,
                    Data = newExpense.Id,
                };
            }

            return new ExpenseResults<string>()
            {
                Success = false,
                Errors = "Share type is not valid",
                StatusCode = 400
            };
        }
        catch (CustomException ex)
        {
            await transaction.RollbackAsync();
            return new ExpenseResults<string>()
            {
                Errors = ex.Errors,
                StatusCode = ex.StatusCode,
                Success = false
            };
        }
    }

    public async Task<ExpenseResults<ReadTestExpenseDto>> GetExpenseAsync(string expenseId)
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
            return new ExpenseResults<ReadTestExpenseDto>()
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
                return new ExpenseResults<ReadTestExpenseDto>()
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
            var user = await _userManager.FindByIdAsync(ub.UserId);
            var owedTo = await _userManager.FindByIdAsync(ub.OwedToUserId);

            if (user == null || owedTo == null || user.UserName == null || owedTo.UserName == null)
                return new ExpenseResults<ReadTestExpenseDto>()
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
            var user = await _userManager.FindByIdAsync(es.UserId);
            var owesUser = await _userManager.FindByIdAsync(es.OwesUserId);

            if (user == null || owesUser == null)
                return new ExpenseResults<ReadTestExpenseDto>()
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
        return new ExpenseResults<ReadTestExpenseDto>()
        {
            Success = true,
            Data = readExpenseDto,
            StatusCode = 200
        };
    }

    public async Task<ExpenseResults<List<ReadAllExpenseDto>>> GetAllExpenses(string groupId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group is null)
        {
            return new ExpenseResults<List<ReadAllExpenseDto>>()
            {
                Errors = "Group does not exist",
                StatusCode = 400,
                Success = false
            };
        }

        var allExpenses = await _context.Expenses.Where(e => e.GroupId == groupId).ToListAsync();
        if (allExpenses.Count == 0)
        {
            return new ExpenseResults<List<ReadAllExpenseDto>>()
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

        return new ExpenseResults<List<ReadAllExpenseDto>>()
        {
            Success = true,
            Data = readAllExpense,
            StatusCode = 200
        };
    }

    private async Task EqualAndMultiPayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
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
                    UserId = member.UserId,
                    User = memberUser,
                    OwesUser = payerUser,
                    OwesUserId = payer.UserId,
                    AmountOwed = amountOwedFromPayer,
                    ShareType = equalShareType
                });

                //finding a user balance if exists
                var userBalance = await _context.UserBalances.FirstOrDefaultAsync(
                    b => b.UserId == member.UserId &&
                         b.OwedToUserId == payer.UserId &&
                         b.GroupId == group.Id
                );

                // if it doesn't exist create a new one with the owed amount
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
        await _context.ExpensePayers.AddRangeAsync(expensePayersList);

        await _context.SaveChangesAsync();
    }

    private async Task EqualAndSinglePayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
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
                    b => b.UserId == member.UserId &&
                         b.OwedToUserId == payer.Id &&
                         b.GroupId == createExpenseDto.GroupId);

            //check if the User Balance exists or not
            if (userBalance is null)
            {
                var newBalanceEntry = new UserBalances
                {
                    GroupId = group.Id,
                    Group = group,
                    UserId = member.UserId,
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
                userBalance.Balance += sharedAmount;
            }

            expenseShares.Add(new ExpenseShares()
            {
                Expense = newExpense,
                ExpenseId = newExpense.Id,
                UserId = member.UserId,
                User = user,
                OwesUser = user,
                OwesUserId = payer.Id,
                AmountOwed = sharedAmount,
                ShareType = equalShareType
            });
        }

        await _context.ExpenseShares.AddRangeAsync(expenseShares);
        await _context.SaveChangesAsync();
    }

    private async Task UnequalAndSinglePayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
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
                ub.UserId == member.UserId &&
                ub.OwedToUserId == payer.Id &&
                ub.GroupId == createExpenseDto.GroupId);

            //check if the User Balance exists or not
            if (userBalance is null)
            {
                var newBalanceEntry = new UserBalances
                {
                    GroupId = group.Id,
                    Group = group,
                    UserId = member.UserId,
                    OwedToUserId = payer.Id,
                    Balance = member.Share,
                    User = user,
                    OwedToUser = payer
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
                OwesUser = payer,
                OwesUserId = payer.Id,
                User = user,
                UserId = user.Id,
                AmountOwed = member.Share,
                ShareType = unequalShareType
            });
        }

        await _context.ExpenseShares.AddRangeAsync(expenseShares);
        await _context.SaveChangesAsync();
    }

    private async Task UnequalAndMultiPayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
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
                    UserId = member.UserId,
                    User = memberUser,
                    OwesUser = payerUser,
                    OwesUserId = payer.UserId,
                    AmountOwed = amountOwedFromPayer,
                    ShareType = unequalShareType
                });

                //finding a user balance if exists
                var userBalance = await _context.UserBalances.FirstOrDefaultAsync(
                    b => b.UserId == member.UserId &&
                         b.OwedToUserId == payer.UserId &&
                         b.GroupId == group.Id
                );

                // if it doesn't exist create a new one with the owed amount
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

        await _context.ExpensePayers.AddRangeAsync(expensePayersList);
        await _context.ExpenseShares.AddRangeAsync(expenseShares);
        await _context.SaveChangesAsync();
    }

    private async Task PercentageAndSinglePayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
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
        List<ExpenseShares> expenseSharesList = [];
        foreach (var member in createExpenseDto.ExpenseSharedMembers)
        {
            var user = await _userManager.FindByIdAsync(member.UserId) ?? throw new CustomException()
            {
                Errors = "User does not exist",
                StatusCode = 400
            };
            var amountOfPercentage = member.Share * total / (decimal)100;
            var userBalance = await _context.UserBalances.FirstOrDefaultAsync(ub => ub.UserId == member.UserId &&
                ub.OwedToUserId == payer.Id &&
                ub.GroupId == group.Id
            );
            if (userBalance is null)
            {
                var newUserBalance = new UserBalances()
                {
                    GroupId = group.Id,
                    Group = group,
                    OwedToUser = payer,
                    OwedToUserId = payer.Id,
                    UserId = member.UserId,
                    User = user,
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
                ShareType = percentageShareType,
                UserId = member.UserId,
                User = user,
                OwesUser = payer,
                OwesUserId = payer.Id,
                AmountOwed = amountOfPercentage
            });
        }

        await _context.ExpenseShares.AddRangeAsync(expenseSharesList);
        await _context.SaveChangesAsync();
    }

    private async Task PercentageAndMultiPayer(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
        {
            if (createExpenseDto.Payers is null || createExpenseDto.Payers.Count == 0)
            {
                throw new CustomException()
                {
                    Errors = "You must provide at least one payer",
                    StatusCode = 400
                };
            }

            var total = createExpenseDto.Payers.Sum(p => p.Share);
            if (total != createExpenseDto.Amount)
            {
                throw new CustomException()
                {
                    Errors = "Total amount payed by payers does not match the total amount of expense",
                    StatusCode = 400
                };
            }

            var totalPercentage = createExpenseDto.ExpenseSharedMembers.Sum(e => e.Share);
            if (totalPercentage != 100)
            {
                throw new CustomException()
                {
                    Errors = "The percentage distribution doesn't sum up to 100",
                    StatusCode = 400
                };
            }

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
                        UserId = member.UserId,
                        User = user,
                        OwesUser = payerUser,
                        OwesUserId = payer.UserId,
                        AmountOwed = amountOwedFromPayer,
                        ShareType = percentageShareType
                    });

                    //finding a user balance if exists
                    var userBalance = await _context.UserBalances.FirstOrDefaultAsync(
                        b => b.UserId == member.UserId &&
                             b.OwedToUserId == payer.UserId &&
                             b.GroupId == group.Id
                    );

                    // if it doesn't exist create a new one with the owed amount
                    if (userBalance is null)
                    {
                        var newUserBalance = new UserBalances
                        {
                            GroupId = group.Id,
                            Group = group,
                            UserId = member.UserId,
                            OwedToUserId = payer.UserId,
                            Balance = amountOwedFromPayer,
                            User = user,
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
}