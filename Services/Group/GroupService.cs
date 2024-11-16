using MediatR;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Data;
using Splitwise_Back.Events.GroupEvents;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dto;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.User;
using Splitwise_Back.Services.UserBalance;

namespace Splitwise_Back.Services.Group;

public class GroupService : IGroupService

{
    private readonly IMediator _mediator;
    private readonly AppDbContext _context;
    private readonly IUserService _userService;
    private readonly IUserBalanceService _userBalanceService;

    public GroupService(
        IMediator mediator,
        AppDbContext context,
        IUserService userService,
        IUserBalanceService userBalanceService
    )
    
    {
        _context = context;
        _mediator = mediator;
        _userService = userService;
        _userBalanceService = userBalanceService;
    }

    public async Task<ResponseResults<string>> CreateGroupAsync(CreateGroupDto createGroupDto)
    {
        if (createGroupDto.UserIds is null || createGroupDto.UserIds.Count < 2)
        {
            throw new ArgumentException("The group must contain more than one member");
        }

        var creator = await _userService.GetUserIdOrThrowAsync(createGroupDto.CreatedByUserId);

        Groups newGroup = new()
        {
            GroupName = createGroupDto.GroupName,
            Description = createGroupDto.Description,
            CreatedByUserId = createGroupDto.CreatedByUserId,
            CreatedByUser = creator,
            DateCreated = DateTime.Now
        };
        _context.Groups.Add(newGroup);
        await _context.SaveChangesAsync();
        var groupMembers = new List<GroupMembers>
        {
            new()
            {
                GroupId = newGroup.Id,
                UserId = createGroupDto.CreatedByUserId,
                IsAdmin = true,
                Group = newGroup,
                User = creator,
                JoinDate = DateTime.Now
            }
        };
        foreach (var userId in createGroupDto.UserIds)
        {
            if (userId == createGroupDto.CreatedByUserId)
            {
                continue;
            }

            var user = await _userService.GetUserIdOrThrowAsync(userId);
            if (user is null)
            {
                throw new KeyNotFoundException("The user could not be found");
            }

            groupMembers.Add(new GroupMembers
            {
                GroupId = newGroup.Id,
                UserId = user.Id,
                IsAdmin = false,
                User = user,
                Group = newGroup,
                JoinDate = DateTime.Now
            });
        }

        await _context.GroupMembers.AddRangeAsync(groupMembers);
        await _context.SaveChangesAsync();

        return new ResponseResults<string>()
        {
            Success = true,
            StatusCode = 200,
            Data = newGroup.Id
        };
    }

    public async Task<ResponseResults<List<ReadGroupDto>>> GetAllGroups()
    {
        var allGroups = await _context.Groups
            .Include(g => g.CreatedByUser)
            .Include(g => g.GroupMembers)
            .ToListAsync();
        if (allGroups.Count == 0)
        {
            throw new KeyNotFoundException("No groups found");
        }

        var readGroupDto = allGroups.Select(g => new ReadGroupDto
        {
            Id = g.Id,
            GroupName = g.GroupName,
            Description = g.Description,
            DateCreated = g.DateCreated,
            CreatedByUserId = g.CreatedByUserId,
            GroupMembers = g.GroupMembers.Select(group => group.UserId).ToList(),
            CreatedByUser = new AbstractReadUserDto()
            {
                UserName = g.CreatedByUser.UserName,
                Id = g.CreatedByUser.Id
            }
        }).ToList();
        return new ResponseResults<List<ReadGroupDto>>()
        {
            Data = readGroupDto,
            StatusCode = 200,
            Success = true
        };
    }

    public async Task<ResponseResults<List<ReadGroupDto>>> GetGroupByCreator(string id)
    {
        var groups = await _context.Groups.Where(g => g.CreatedByUserId == id)
            .Include(g => g.GroupMembers)
            .Include(g => g.CreatedByUser)
            .AsNoTracking()
            .ToArrayAsync();
        if (groups.Length < 1)
        {
            throw new KeyNotFoundException("No groups found");
        }

        var readGroupDto = groups.Select(g => new ReadGroupDto
        {
            Id = g.Id,
            GroupName = g.GroupName,
            Description = g.Description,
            DateCreated = g.DateCreated,
            CreatedByUserId = g.CreatedByUserId,
            GroupMembers = g.GroupMembers.Select(group => group.UserId).ToList(),
            CreatedByUser = new AbstractReadUserDto()
            {
                UserName = g.CreatedByUser.UserName,
                Id = g.CreatedByUser.Id
            }
        }).ToList();
        return new ResponseResults<List<ReadGroupDto>>()
        {
            Data = readGroupDto,
            StatusCode = 200,
            Success = true
        };
    }

    public async Task<ResponseResults<string>> UpdateGroup(UpdateGroupDto updateGroup, string id, string currentUserId)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group is null)
        {
            throw new KeyNotFoundException("No group found");
        }

        var groupAdminId = await _context.GroupMembers
            .Where(g => g.GroupId == group.Id && g.IsAdmin)
            .Select(g => g.UserId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (groupAdminId == null)
        {
            throw new ArgumentException("Group doesn't have any admin");
        }


        if (groupAdminId != currentUserId)
        {
            throw new UnauthorizedAccessException("Only Group Admin can apply changes to the group");
        }

        group.Description = updateGroup.Description;
        group.GroupName = updateGroup.GroupName;
        _context.Groups.Update(group);
        await _context.SaveChangesAsync();
        return new ResponseResults<string>()
        {
            Data = "Group Updated Successfully",
            StatusCode = 200,
            Success = true
        };
    }

    public async Task<ResponseResults<string>> AddMembersToGroup(AddToGroupDto addToGroup, string id)
    {
        if (addToGroup.UserIds.Count == 0)
        {
            throw new ArgumentException("please select members to be added");
        }

        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
        {
            throw new KeyNotFoundException("No group found");
        }

        if ((group.GroupMembers.Count + addToGroup.UserIds.Count) > 50)
        {
            throw new ArgumentException("Group cannot have more than 50 members");
        }

        var usersToAdd = addToGroup.UserIds
            .Select(userId => _userService.GetUserIdOrThrowAsync(userId))
            .ToList();
        var users = await Task.WhenAll(usersToAdd);
        var groupMembers = new List<GroupMembers>();
        foreach (var user in users)
        {
            if (user is null)
            {
                throw new ArgumentException("provide a list of valid user Ids");
            }

            groupMembers.Add(new GroupMembers
            {
                GroupId = group.Id,
                UserId = user.Id,
                IsAdmin = false,
                Group = group,
                User = user,
                JoinDate = DateTime.Now
            });
            if (groupMembers.Count == 0)
            {
                throw new KeyNotFoundException("No Users Found");
            }

            await _context.GroupMembers.AddRangeAsync(groupMembers);
            await _context.SaveChangesAsync();
        }

        return new ResponseResults<string>()
        {
            Data = "Members successfully removed from the group",
            StatusCode = 200,
            Success = true
        };
    }

    public async Task<ResponseResults<string>> RemoveMembersFromGroup(RemoveFromGroupDto removeFromGroupDto, string id)
    {
        if (removeFromGroupDto.UserIds.Count == 0)
            throw new ArgumentException("please select members to be deleted");


        var group = await _context.Groups
                        .Include(g => g.GroupMembers)
                        .FirstOrDefaultAsync(g => g.Id == id)
                    ?? throw new KeyNotFoundException("No group found");


        var membersToRemove = group
            .GroupMembers
            .Where(gm => removeFromGroupDto.UserIds.Contains(gm.UserId))
            .ToList();

        if (membersToRemove.Count <= 0)
            throw new ArgumentException("No users provided exist in the group");


        if ((group.GroupMembers.Count - membersToRemove.Count) < 2)
            throw new ArgumentException(
                "Removing members will make a group of single user either delete or reduce the list of members");


        _context.GroupMembers.RemoveRange(membersToRemove);
        await _context.SaveChangesAsync();
        return new ResponseResults<string>()
        {
            Data = "Members successfully removed from the group",
            StatusCode = 200,
            Success = true
        };
    }

    public async Task<ResponseResults<List<TransactionResults>>> GetSettlementOfGroupByGreedy(string id)
    {
        var expenseShares = await _context.ExpenseShares
            .Where(es => es.Expense.GroupId == id)
            .Include(es => es.Expense)
            .ToListAsync();

        var userBalances = await _context.UserBalances
            .Where(ub => ub.GroupId == id)
            .ToListAsync();

        Dictionary<string, decimal> dbUserBalanceSheet = [];
        Dictionary<string, decimal> userBalanceSheet = [];

        // who owes how much and who is owed how much using expense shares
        foreach (var expenseShare in expenseShares)
        {
            if (!userBalanceSheet.ContainsKey(expenseShare.OwedByUserId))
            {
                userBalanceSheet[expenseShare.OwedByUserId] = 0;
            }

            if (!userBalanceSheet.ContainsKey(expenseShare.OwesToUserId))
            {
                userBalanceSheet[expenseShare.OwesToUserId] = 0;
            }

            userBalanceSheet[expenseShare.OwedByUserId] += expenseShare.AmountOwed;
            userBalanceSheet[expenseShare.OwesToUserId] -= expenseShare.AmountOwed;
        }

        foreach (var userBalance in userBalances)
        {
            if (!dbUserBalanceSheet.ContainsKey(userBalance.OwedByUserId))
            {
                dbUserBalanceSheet[userBalance.OwedByUserId] = 0;
            }

            if (!dbUserBalanceSheet.ContainsKey(userBalance.OwesToUserId))
            {
                dbUserBalanceSheet[userBalance.OwesToUserId] = 0;
            }

            dbUserBalanceSheet[userBalance.OwedByUserId] += userBalance.Balance;
            dbUserBalanceSheet[userBalance.OwesToUserId] -= userBalance.Balance;
        }

        if (userBalanceSheet.Count != dbUserBalanceSheet.Count)
            throw new InvalidDataException("The data is inaccurate so the process could not be completed");

        foreach (var dbKey in dbUserBalanceSheet)
        {
            if (!userBalanceSheet.TryGetValue(dbKey.Key, out var value))
                throw new InvalidDataException("The data is inaccurate so the process could not be completed");

            if (dbKey.Value != value)
                throw new InvalidDataException("The data is inaccurate so the process could not be completed");
        }

        var transactions = new List<Transaction>();
        var payers = dbUserBalanceSheet.Where(b => b.Value < 0).Select(b => new { Id = b.Key, Amount = -b.Value })
            .ToList();
        var receivers = dbUserBalanceSheet.Where(b => b.Value > 0).Select(b => new { Id = b.Key, Amount = b.Value })
            .ToList();


        int i = 0, j = 0;
        // Settle debts with a two-pointer approach and log transactions
        while (i < payers.Count && j < receivers.Count)
        {
            var settlementAmount = Math.Min(payers[i].Amount, receivers[j].Amount);

            // Record the transaction
            transactions.Add(new Transaction
            {
                PayerId = payers[i].Id,
                ReceiverId = receivers[j].Id,
                Amount = settlementAmount
            });

            // Adjust balances after transaction
            payers[i] = new { Id = payers[i].Id, Amount = payers[i].Amount - settlementAmount };
            receivers[j] = new { Id = receivers[j].Id, Amount = receivers[j].Amount - settlementAmount };

            // Move pointers if a debt is settled
            if (payers[i].Amount == 0) i++;
            if (receivers[j].Amount == 0) j++;
        }

        List<TransactionResults> transactionResultsList = [];
        foreach (var transaction in transactions)
        {
            var payer = await _userService.GetUserIdOrThrowAsync(transaction.PayerId);
            var reviver = await _userService.GetUserIdOrThrowAsync(transaction.ReceiverId);
            transactionResultsList.Add(new TransactionResults()
            {
                Payer = new AbstractReadUserDto()
                {
                    Id = payer.Id,
                    UserName = payer.UserName
                },
                Receiver = new AbstractReadUserDto()
                {
                    Id = reviver.Id,
                    UserName = reviver.UserName
                },
                Amount = transaction.Amount
            });
        }

        return new ResponseResults<List<TransactionResults>>()
        {
            Data = transactionResultsList,
            Success = true,
            StatusCode = 200,
        };
    }

    public async Task<ResponseResults<List<TransactionResults>>> SettleGroup(string id)
    {
        var group = await _context.Groups.FindAsync(id) ?? throw new KeyNotFoundException("Group Not Found");

        var transactionsResult = await GetSettlementOfGroupByGreedy(id);
        var userBalance = await _context.UserBalances.Where(ub => ub.GroupId == id)
            .ToListAsync();
        if (transactionsResult.Data is null)
        {
            return transactionsResult;
        }

        var transactions = transactionsResult.Data;
        List<Settlement> settlements = [];
        foreach (var transact in transactions)
        {
            if (transact.Receiver.Id is null || transact.Payer.Id is null)
                throw new KeyNotFoundException("Users not found ");

            var receiver = await _userService.GetUserIdOrThrowAsync(transact.Receiver.Id);
            var payer = await _userService.GetUserIdOrThrowAsync(transact.Payer.Id);
            settlements.Add(new Settlement()
            {
                GroupId = id,
                ReceiverId = transact.Receiver.Id,
                Receiver = receiver,
                PayerId = payer.Id,
                Payer = payer,
                Amount = transact.Amount,
                Group = group,
                SettlementDate = DateTime.UtcNow,
            });
        }

        await _context.Settlements.AddRangeAsync(settlements);
        await _context.SaveChangesAsync();
        foreach (var balance in userBalance)
        {
            balance.Balance = 0;
        }

        await _context.SaveChangesAsync();
        return new ResponseResults<List<TransactionResults>>()
        {
            Data = transactionsResult.Data,
            StatusCode = 200,
            Success = true,
        };
    }

    public void IsGroupSettled(string id)
    {
        var unsettled = _context.UserBalances.Any(ub => ub.GroupId == id && ub.Balance == 0);
        if (!unsettled) throw new ArgumentException("The group is not settled");
    }

    public async Task<ResponseResults<string>> DeleteGroup(string id, string currentUser)
    {
        var group = await _context.Groups.FindAsync(id)
                    ?? throw new KeyNotFoundException("Group Not Found");

        var groupAdminId = await _context.GroupMembers
                               .Where(g => g.GroupId == group.Id && g.IsAdmin == true)
                               .Select(user => user.UserId)
                               .AsNoTracking()
                               .FirstOrDefaultAsync()
                           ?? throw new KeyNotFoundException("User of the group Not Found");


        if (groupAdminId != currentUser)
            throw new UnauthorizedAccessException("Only the admin can Delete the group");

        IsGroupSettled(id);
        var groupDeleteEvent = new GroupDeleteEvent(id);
        await _mediator.Publish(groupDeleteEvent);
        await _userBalanceService.DeleteUserBalanceByGroup(id);
        
 

        await _context.Groups
            .Where(g => g.Id == group.Id)
            .ExecuteDeleteAsync();
        await _context.SaveChangesAsync();
        return new ResponseResults<string>()
        {
            Data = "Group Deleted Successfully",
            Success = true,
            StatusCode = 200
        };
    }

    public async Task<List<string>> GetGroupsWhereUserExists(string userId)
    {
        var groups = await _context.Groups
            .Where(g => g.GroupMembers.Any(gm => gm.UserId == userId && !gm.IsAdmin))
            .Select(g=>g.Id)
            .ToListAsync();
        return groups;
    }
}