using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Controllers;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.User;
using Splitwise_Back.Validation.Group;

namespace Splitwise_Back.Services.Expense;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly GroupValidation _groupValidation;
    private const string EqualShareType = "EQUAL";
    private const string UnequalShareType = "UNEQUAL";
    private const string PercentageShareType = "PERCENTAGE";
    private readonly List<string> _validShareTypes = [EqualShareType, UnequalShareType, PercentageShareType];


    public ExpenseService(
        AppDbContext context,
        IUserService userService,
        GroupValidation groupValidation)
    {
        _context = context;
        _userService = userService;
        _groupValidation = groupValidation;
    }

    public async Task<ResponseResults<string>> UpdateExpense(string expenseId, UpdateExpenseDto updateExpenseDto)
    {
        if (!_validShareTypes.Contains(updateExpenseDto.ShareType, StringComparer.InvariantCultureIgnoreCase))
        {
            throw new ArgumentException("Please provide valid share type");
        }

        var group = await _groupValidation.ValidateExpenseSharedMembersInGroup(
            updateExpenseDto.ExpenseSharedMembers, updateExpenseDto.GroupId);

        var expense = await _context.Expenses
                          .Include(e => e.ExpenseShares)
                          .Include(e => e.Payers)
                          .FirstOrDefaultAsync(x => x.Id == expenseId)
                      ?? throw new KeyNotFoundException("The expense of given expense id is not found");


        expense.Description = updateExpenseDto.Description;

        await _context.SaveChangesAsync();
        await UpdateExpenseShare(updateExpenseDto, expense, group);
        await _context.SaveChangesAsync();
        return new ResponseResults<string>()
        {
            Data = "Data Updated Successfully",
            Success = true,
            StatusCode = 200
        };
    }


    /// <summary>
    ///  For Getting Single Expenses, 
    /// Parameters => string expenseId
    /// </summary>
    public async Task<ResponseResults<ReadTestExpenseDto>> GetExpenseAsync(string expenseId)
    {
        // Fetch the expense and related data in a single query with no tracking
        var expense = await _context.Expenses
                          .Include(e => e.ExpenseShares)
                          .Include(e => e.Payers)
                          .Include(e => e.Group)
                          .AsNoTracking()
                          .FirstOrDefaultAsync(e => e.Id == expenseId)
                      ?? throw new KeyNotFoundException("The expense of given expense id is not found");

        // Fetch user balances once and apply AsNoTracking
        var userBalances = await _context.UserBalances
            .Where(e => e.GroupId == expense.GroupId)
            .AsNoTracking()
            .ToListAsync();

        // Prepare data transformations in sequential loops to avoid concurrency issues
        var expensePayers = new List<ReadExpensePayerDto>();
        foreach (var payer in expense.Payers)
        {
            var user = await _userService.GetUserIdOrThrowAsync(payer.PayerId);

            expensePayers.Add(new ReadExpensePayerDto
            {
                Id = payer.Id,
                UserName = user.UserName,
                AmountPaid = payer.AmountPaid
            });
        }

        var allUserBalances = new List<ReadUserBalanceDto>();
        foreach (var ub in userBalances)
        {
            var user = await _userService.GetUserIdOrThrowAsync(ub.OwedByUserId);
            var owedTo = await _userService.GetUserIdOrThrowAsync(ub.OwesToUserId);

            if (user.UserName == null || owedTo.UserName == null)
                throw new ArgumentNullException(user.UserName, owedTo.UserName);

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
            var user = await _userService.GetUserIdOrThrowAsync(es.OwedByUserId);
            var owesUser = await _userService.GetUserIdOrThrowAsync(es.OwesToUserId);

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


    /// <summary>
    ///  For Getting All Expenses, Parameters => string groupId
    /// </summary>
    public async Task<ResponseResults<List<ReadAllExpenseDto>>> GetAllExpenses(string groupId)
    {
        var group = await _context.Groups.FindAsync(groupId)
                    ?? throw new KeyNotFoundException("The expense of given expense id is not found");

        var allExpenses = await _context.Expenses.Where(e => e.GroupId == groupId).ToListAsync();
        if (allExpenses.Count == 0)
        {
            throw new KeyNotFoundException("No Expenses found for this group");
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


    public async Task<ResponseResults<string>> CreateExpenseAsync(CreateExpenseDto createExpenseDto)
    {
        if (!_validShareTypes.Contains(createExpenseDto.ShareType, StringComparer.InvariantCultureIgnoreCase))
        {
           throw new ArgumentException("Please provide a valid share type");
        }

        // Validate Group
        var group = await _groupValidation.ValidateExpenseSharedMembersInGroup(
            createExpenseDto.ExpenseSharedMembers, createExpenseDto.GroupId);

        var newExpense = new Expenses()
        {
            Amount = createExpenseDto.Amount,
            GroupId = createExpenseDto.GroupId,
            Group = group,
            Date = DateTime.UtcNow,
            Description = createExpenseDto.Description,
        };
        _context.Expenses.Add(newExpense);
        await _context.SaveChangesAsync();
        await CreateExpenseShares(createExpenseDto, group, newExpense);
        await _context.SaveChangesAsync();
        return new ResponseResults<string>()
        {
            Success = true,
            StatusCode = 200,
            Data = newExpense.Id
        };
    }

    private async Task CreateExpenseShares(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        ValidateExpenseData(createExpenseDto);
        var total = createExpenseDto.Payers.Sum(p => p.Share);
        List<ExpenseShares> expenseSharesList = [];
        List<ExpensePayers> expensePayersList = [];
        if (createExpenseDto.ShareType.Equals("percentage", StringComparison.OrdinalIgnoreCase))
        {
            ConvertPercentShareToAmount(createExpenseDto.ExpenseSharedMembers, total);
        }

        foreach (var payerDto in createExpenseDto.Payers)
        {
            var payerUser = await _userService.GetUserIdOrThrowAsync(payerDto.UserId);
            var expensePayer = CreateExpensePayerAsync(newExpense, payerUser, payerDto.Share);
            expensePayersList.Add(expensePayer);
            await AddExpenseSharesForPayer(createExpenseDto, payerDto, total, expenseSharesList, payerUser,
                newExpense, group);
        }

        await _context.ExpenseShares.AddRangeAsync(expenseSharesList);
        await _context.ExpensePayers.AddRangeAsync(expensePayersList);
        await _context.SaveChangesAsync();
    }

    private static void ValidateExpenseData(CreateExpenseDto createExpenseDto)
    {
        if (createExpenseDto.Payers.Count == 0)
            throw new ArgumentException("No payers for the given expense");

        if (createExpenseDto.Payers.Sum(p => p.Share) != createExpenseDto.Amount)
            throw new ArgumentException("Total amount paid by payers does not match the expense amount");

        if (createExpenseDto.ExpenseSharedMembers.Sum(e => e.Share) != 100)
            throw new ArgumentException("The percentage distribution doesn't sum up to 100");
    }

    private static void ValidateExpenseData(UpdateExpenseDto updateExpenseDto)
    {
        if (updateExpenseDto.Payers.Count == 0)
            throw new ArgumentException("You must provide at least one payer");

        if (updateExpenseDto.Payers.Sum(p => p.Share) != updateExpenseDto.Amount)
            throw new ArgumentException("Total amount paid by payers does not match the expense amount");

        if (updateExpenseDto.ExpenseSharedMembers.Sum(e => e.Share) != 100)
            throw new ArgumentException("The percentage distribution doesn't sum up to 100");
    }

    private static ExpensePayers CreateExpensePayerAsync(Expenses newExpense, CustomUsers payerUser, decimal amountPaid)
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

    private async Task UpdateExpenseShare(UpdateExpenseDto updateExpenseDto, Expenses existingExpense, Groups group)
    {
        ValidateExpenseData(updateExpenseDto);
        existingExpense.Amount = updateExpenseDto.Amount;
        await _context.SaveChangesAsync();
        var total = updateExpenseDto.Payers.Sum(p => p.Share);
        if (updateExpenseDto.ShareType.Equals(PercentageShareType, StringComparison.OrdinalIgnoreCase))
        {
            ConvertPercentShareToAmount(updateExpenseDto.ExpenseSharedMembers, total);
        }

        List<ExpenseShares> expenseSharesList = [];
        List<ExpensePayers> expensePayersList = [];

        // deducts the amount of every expense shares
        foreach (var member in existingExpense.ExpenseShares)
        {
            await UpdateUserBalanceOnExpenseDeletionOrModification(member.OwedByUserId, member.OwesToUserId,
                existingExpense.GroupId, member.AmountOwed);
        }

        await _context.SaveChangesAsync();
        var expenseSharedMembers = _context.ExpenseShares.Where(es => es.ExpenseId == existingExpense.Id)
            .AsNoTracking()
            .Select(es => es.OwedByUserId)
            .Distinct()
            .ToList();
        var newExpenseSharedMembers = updateExpenseDto.ExpenseSharedMembers.Select(e => e.UserId);
        var expenseSharedMembersToBeDeleted = expenseSharedMembers.Except(newExpenseSharedMembers).ToList();

        var newExpensePayersId = updateExpenseDto.Payers.Select(p => p.UserId);
        var dbExpensePayersId = await _context.ExpensePayers.Where(ep => ep.ExpenseId == existingExpense.Id)
            .Select(ep => ep.PayerId)
            .AsNoTracking()
            .ToListAsync();

        var payersToBeDeleted = dbExpensePayersId.Except(newExpensePayersId).ToList();

        if (expenseSharedMembersToBeDeleted.Count > 0)
        {
            foreach (var expenseSharedMember in expenseSharedMembersToBeDeleted)
            {
                await _context.ExpenseShares.Where(ep =>
                        ep.OwedByUserId == expenseSharedMember && ep.ExpenseId == existingExpense.Id)
                    .AsNoTracking()
                    .ExecuteDeleteAsync();
            }
        }

        // remove the expense Payer
        if (payersToBeDeleted.Count > 0)
        {
            foreach (var payer in payersToBeDeleted)
            {
                await _context.ExpensePayers.Where(ep => ep.PayerId == payer)
                    .AsNoTracking()
                    .ExecuteDeleteAsync();
            }
        }

        await _context.SaveChangesAsync();

        foreach (var payer in updateExpenseDto.Payers)
        {
            var userPayer = await _userService.GetUserIdOrThrowAsync(payer.UserId);
            var existingPayerOfExpense =
                await _context.ExpensePayers
                    .Where(p => p.PayerId == payer.UserId && p.ExpenseId == existingExpense.Id)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
            if (existingPayerOfExpense is null)
            {
                var newPayer = CreateExpensePayerAsync(existingExpense, userPayer, payer.Share);
                expensePayersList.Add(newPayer);
            }
            else
            {
                existingPayerOfExpense.AmountPaid = payer.Share;
            }

            await UpdateExpenseSharesForPayer(updateExpenseDto, payer, total, expenseSharesList, userPayer,
                existingExpense, group);
        }

        await _context.SaveChangesAsync();
        await _context.ExpenseShares.AddRangeAsync(expenseSharesList);
        await _context.ExpensePayers.AddRangeAsync(expensePayersList);
        await _context.SaveChangesAsync();
    }


    private async Task UpdateExpenseSharesForPayer(UpdateExpenseDto updateExpenseDto,
        ExpensePayer expensePayer,
        decimal total,
        List<ExpenseShares> expenseSharesList,
        CustomUsers payerUser,
        Expenses newExpense,
        Groups group)
    {
        var payerShare = expensePayer.Share;
        var proportionOfDebtCovered = payerShare / total;
        foreach (var member in updateExpenseDto.ExpenseSharedMembers)
        {
            if (expensePayer.UserId == member.UserId)
                continue;
            var memberUser = await _userService.GetUserIdOrThrowAsync(member.UserId);
            var amountOwed = proportionOfDebtCovered * member.Share;
            var existingExpenseShare = await _context.ExpenseShares
                .Where(es =>
                    es.OwesToUserId == expensePayer.UserId && es.OwedByUserId == memberUser.Id &&
                    es.ExpenseId == newExpense.Id)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (existingExpenseShare is null)
            {
                var expenseShare = new ExpenseShares
                {
                    Expense = newExpense,
                    ExpenseId = newExpense.Id,
                    OwedByUserId = member.UserId,
                    OwedByUser = memberUser,
                    OwesToUser = payerUser,
                    OwesToUserId = expensePayer.UserId,
                    AmountOwed = amountOwed,
                    ShareType = updateExpenseDto.ShareType.ToUpper()
                };
                expenseSharesList.Add(expenseShare);
            }
            else
            {
                existingExpenseShare.AmountOwed = amountOwed;
            }

            await UpdateUserBalanceAsync(memberUser, payerUser, group, amountOwed);
        }
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

            var memberUser = await _userService.GetUserIdOrThrowAsync(member.UserId);
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
            var newUserBalance = new UserBalances()
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

    private async Task UpdateUserBalanceOnExpenseDeletionOrModification(string owedByUserId, string owesToUserId,
        string groupId,
        decimal amountOwed)
    {
        var userBalance = await _context.UserBalances
            .FirstOrDefaultAsync(b => b.OwedByUserId == owedByUserId &&
                                      b.OwesToUserId == owesToUserId &&
                                      b.GroupId == groupId);
        if (userBalance is not null)
        {
            userBalance.Balance -= amountOwed;
        }
    }

    private static void ConvertPercentShareToAmount(List<ExpenseSharedMembers> expenseSharedMembers, decimal total)
    {
        foreach (var member in expenseSharedMembers)
        {
            member.Share = member.Share * total / 100;
        }
    }

    public async Task<ResponseResults<string>> DeleteExpense(string expenseId)
    {
        var expense = await _context.Expenses
                          .Include(e => e.ExpenseShares)
                          .Where(ex => ex.Id == expenseId)
                          .FirstOrDefaultAsync() ??
                      throw new KeyNotFoundException("Expense not found");


        foreach (var expenseShare in expense.ExpenseShares)
        {
            await UpdateUserBalanceOnExpenseDeletionOrModification(expenseShare.OwedByUserId,
                expenseShare.OwesToUserId,
                expense.GroupId, expenseShare.AmountOwed);
        }

        await _context.ExpenseShares.Where(exp => exp.ExpenseId == expenseId).ExecuteDeleteAsync();
        await _context.ExpensePayers.Where(ep => ep.ExpenseId == expenseId).ExecuteDeleteAsync();
        await _context.Expenses.Where(exp => exp.Id == expenseId).ExecuteDeleteAsync();
        await _context.SaveChangesAsync();
        return new ResponseResults<string>()
        {
            Data = "Expense deleted successfully",
            Success = true,
            StatusCode = 200
        };
    }


    public async Task DeleteAllExpenses(string groupId)
    {
        var expenseIds = await _context.Expenses.Where(e => e.GroupId == groupId).Select(e => e.Id).ToListAsync();
        foreach (var expenseId in expenseIds)
        {
            await DeleteExpense(expenseId);
        }

        await _context.SaveChangesAsync();
    }


    public async Task<List<ReadExpenseShareDto>> GetAllExpenseShares(string expenseId)
    {
        var expenseShares = await _context.ExpenseShares
            .Where(e => e.ExpenseId == expenseId)
            .Include(e => e.OwesToUser)
            .Include(e => e.OwedByUser)
            .ToListAsync();
        var expenseShareDto = expenseShares.Select(e => new ReadExpenseShareDto()
        {
            AmountOwed = e.AmountOwed,
            ShareType = e.ShareType.ToUpper(),
            User = new AbstractReadUserDto()
            {
                Id = e.OwesToUserId,
                UserName = e.OwesToUser.UserName
            },
            OwesUser = new AbstractReadUserDto()
            {
                Id = e.OwedByUserId,
                UserName = e.OwedByUser.UserName
            },
        });
        return expenseShareDto.ToList();
    }

    public void ThrowResults()
    {
        throw new KeyNotFoundException("The given key is not found");
    }
}