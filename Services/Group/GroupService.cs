using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Controllers;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.User;

namespace Splitwise_Back.Services.Group;

public class GroupService:IGroupService

{

    private readonly ILogger<ExpenseController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<CustomUsers> _userManager;
    public GroupService(ILogger<ExpenseController> logger, AppDbContext context,
        UserManager<CustomUsers> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }
    public async Task<ResponseResults<string>> CreateGroupAsync(CreateGroupDto createGroupDto)
    {

        if (createGroupDto.UserIds is null || createGroupDto.UserIds.Count < 2)
        {
            return new ResponseResults<string>()
            {
                Success = false,
                StatusCode = 400,
                Errors = "The group must contain more than one member"
            };
        }

        var creator = await _userManager.FindByIdAsync(createGroupDto.CreatedByUserId);
        if (creator == null)
        {
            return new ResponseResults<string>()
            {
                Success = false,
                StatusCode = 400,
                Errors = "The creator of the group doesn't exist"
            };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
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

                var user = await _userManager.FindByIdAsync(userId);
                if (user is null)
                {
                    throw new Exception("User Id must be valid");
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

            await transaction.CommitAsync();
            return new ResponseResults<string>()
            {
                Success = true,
                StatusCode = 200,
                Data = newGroup.Id
            };
        }
        catch (Exception ex)
        {
            // Rollback the transaction in case of any error
            await transaction.RollbackAsync();
            _logger.LogError(ex, "An error occurred while adding members to the group: {Message}", ex.Message);
            return new ResponseResults<string>()
            {
                Success = false,
                StatusCode = 400,
                Errors = $"An error occurred while creating the group, {ex.Message}"
            };
        }
    }

    public async Task<Groups> ValidateGroup(string groupId)
    {
        return await _context.Groups.FindAsync(groupId) ?? throw new CustomException()
        {
            StatusCode = 400,
            Errors = "Group not found"
        };
    }
    
    public async Task<Groups> ValidateGroupAndMembers(CreateExpenseDto createExpenseDto)
    {
        // Validate Group
        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == createExpenseDto.GroupId);
        if (group is null)
        {
            throw new CustomException()
            {
                Errors = "Group does not exist",
                StatusCode = 400
            };
        }


        // Validate Share Participants
        var userIdsInGroup = group.GroupMembers.Select(gm => gm.UserId).ToHashSet();
        var invalidUsers = createExpenseDto.ExpenseSharedMembers.Where(es => !userIdsInGroup.Contains(es.UserId))
            .ToList();
        if (invalidUsers.Count != 0)
        {
            throw new CustomException()
            {
                Errors = "Members provided does not exist in group",
                StatusCode = 400
            };
        }

        return group;
    }    public async Task<Groups> ValidateGroupAndMembers(UpdateExpenseDto createExpenseDto)
    {
        // Validate Group
        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == createExpenseDto.GroupId);
        if (group is null)
        {
            throw new CustomException()
            {
                Errors = "Group does not exist",
                StatusCode = 400
            };
        }


        // Validate Share Participants
        var userIdsInGroup = group.GroupMembers.Select(gm => gm.UserId).ToHashSet();
        var invalidUsers = createExpenseDto.ExpenseSharedMembers.Where(es => !userIdsInGroup.Contains(es.UserId))
            .ToList();
        if (invalidUsers.Count != 0)
        {
            throw new CustomException()
            {
                Errors = "Members provided does not exist in group",
                StatusCode = 400
            };
        }

        return group;
    }
    
}