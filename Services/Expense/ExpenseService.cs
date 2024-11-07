using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Controllers;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.Group;
using Splitwise_Back.Services.User;

namespace Splitwise_Back.Services.Expense;

public class ExpenseService : IExpenseService
{
    private readonly ILogger<ExpenseController> _logger;
    private readonly AppDbContext _context;
    private readonly IGroupService _groupService;
    private readonly IUserService _userService;
    private const string EqualShareType = "EQUAL";
    private const string UnequalShareType = "UNEQUAL";
    private const string PercentageShareType = "PERCENTAGE";
    private readonly List<string> _validShareTypes = [EqualShareType, UnequalShareType, PercentageShareType];


    public ExpenseService(ILogger<ExpenseController> logger, AppDbContext context,
        IGroupService groupService, IUserService userService)
    {
        _logger = logger;
        _context = context;
        _groupService = groupService;
        _userService = userService;
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


    /// <summary>
    ///  For Getting Single Expenses, 
    /// Parameters => string expenseId
    /// </summary>
    public async Task<ResponseResults<ReadTestExpenseDto>> GetExpenseAsync(string expenseId)
    {
        try
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
                var user = await _userService.GetUserIdOrThrowAsync(payer.PayerId);

                expensePayers.Add(new AbstractReadUserDto
                {
                    Id = payer.Id,
                    UserName = user.UserName
                });
            }

            var allUserBalances = new List<ReadUserBalanceDto>();
            foreach (var ub in userBalances)
            {
                var user = await _userService.GetUserIdOrThrowAsync(ub.OwedByUserId);
                var owedTo = await _userService.GetUserIdOrThrowAsync(ub.OwesToUserId);

                if (user.UserName == null || owedTo.UserName == null)
                    throw new CustomException()
                    {
                        StatusCode = 400,
                        Errors = "User not Valid"
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
        catch (CustomException ex)
        {
            return new ResponseResults<ReadTestExpenseDto>()
            {
                Success = false,
                Errors = ex.Message,
                StatusCode = 400
            };
        }
    }

    /// <summary>
    ///  For Getting All Expenses, Parameters => string groupId
    /// </summary>
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


    public async Task<ResponseResults<string>> CreateExpenseAsync(CreateExpenseDto createExpenseDto)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (!_validShareTypes.Contains(createExpenseDto.ShareType, StringComparer.InvariantCultureIgnoreCase))
            {
                return new ResponseResults<string>()
                {
                    Success = false,
                    StatusCode = 400,
                    Errors = "Please Enter a valid share type"
                };
            }

            // Validate Group
            var group = await _groupService.ValidateGroupAndMembers(createExpenseDto);

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
            await transaction.CommitAsync();
            return new ResponseResults<string>()
            {
                Success = true,
                StatusCode = 200,
                Data = newExpense.Id
            };
        }
        catch (CustomException ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(ex, ex.Message);
            return new ResponseResults<string>()
            {
                Errors = $"An error occured while processing your request, {ex.Message}",
                StatusCode = ex.StatusCode,
                Success = false
            };
        }
    }

    public async Task CreateExpenseShares(CreateExpenseDto createExpenseDto, Groups group, Expenses newExpense)
    {
        try
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


    private static void ValidateExpenseData(CreateExpenseDto createExpenseDto)
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

    private static void ConvertPercentShareToAmount(List<ExpenseSharedMembers> expenseSharedMembers, decimal total)
    {
        foreach (var member in expenseSharedMembers)
        {
            member.Share = member.Share * total / 100;
        }
    }
}