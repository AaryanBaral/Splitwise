using Microsoft.AspNetCore.Identity;
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
    private readonly IUserService _userService;
    private readonly UserManager<CustomUsers> _userManager;
    public GroupService(ILogger<ExpenseController> logger, AppDbContext context,
        UserManager<CustomUsers> userManager, IUserService userService)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _userService = userService;
        
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
}