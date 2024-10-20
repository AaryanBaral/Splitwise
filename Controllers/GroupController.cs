using System.Security.Claims;
using AutoMapper;
using CloudinaryDotNet;
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
public class GroupController : Controller
{
    private readonly ILogger<GroupController> _logger;
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly UserManager<CustomUsers> _userManager;

    public GroupController(ILogger<GroupController> logger, AppDbContext context, UserManager<CustomUsers> userManager, IMapper mapper)
    {
        _logger = logger;
        _context = context;
        _mapper = mapper;
        _userManager = userManager;
    }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto groupDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        if (groupDto.UserIds is null || groupDto.UserIds.Count < 2)
        {
            return BadRequest("group must contain atleast 2 members");
        }
        var creator = await _userManager.FindByIdAsync(groupDto.CreatedByUserId);
        if (creator == null)
        {
            return NotFound("Creator user not found");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            Groups newGroup = new()
            {
                GroupName = groupDto.GroupName,
                Description = groupDto.Description,
                CreatedByUserId = groupDto.CreatedByUserId,
                CreatedByUser = creator,
                DateCreated = DateTime.Now
            };
            _context.Groups.Add(newGroup);
            await _context.SaveChangesAsync();
            var groupMembers = new List<GroupMembers>
            {
                new() {
                    GroupId = newGroup.Id,
                    UserId = groupDto.CreatedByUserId,
                    IsAdmin = true,
                    Group = newGroup,
                    User = creator,
                    JoinDate = DateTime.Now
                }
            };
            foreach (var userId in groupDto.UserIds)
            {
                if (userId == groupDto.CreatedByUserId)
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
            return Ok(new
            {
                GroupId = newGroup.Id,
                Message = "Group created successfully",
                MembersAdded = groupMembers.Count
            });
        }
        catch (Exception ex)
        {
            // Rollback the transaction in case of any error
            await transaction.RollbackAsync();
            _logger.LogError(ex, "An error occurred while adding members to the group: {Message}", ex.Message);
            return StatusCode(500, new { Message = "An error occurred while creating the group.", Error = ex.Message });
        }
    }


    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var AllGroups = await _context.Groups
        .Include(g => g.CreatedByUser)
        .ToListAsync();
        if (AllGroups is null || AllGroups.Count == 0)
        {
            return NotFound("No Group Exists");
        }
        var readGroupDto = AllGroups.Select(g => new ReadGroupDto
        {
            Id = g.Id,
            GroupName = g.GroupName,
            Description = g.Description,
            DateCreated = g.DateCreated,
            CreatedByUserId = g.CreatedByUserId,
            CreatedByUser = new AbstractReadUserDto()
            {
                UserName = g.CreatedByUser.UserName,
                Id = g.CreatedByUser.Id
            }
        }).ToList();
        return Ok(readGroupDto);
    }

    [HttpGet]
    [Route("creator/{id}")]
    public async Task<IActionResult> GetGroupByCreator(string id)
    {
        var groups = await _context.Groups.Where(g => g.CreatedByUserId == id)
        .AsNoTracking()
        .ToArrayAsync();
        if (groups.Length < 1)
        {
            return NotFound("This user has no Group");
        }

        return Ok(groups);
    }

    [HttpDelete]
    [Route("delete/{id}")]
    public async Task<IActionResult> DeleteGroup(string id)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group is null)
        {
            return BadRequest("Group with Given id doesnot exists");
        }
        var userId = User.FindFirstValue("Id");
        if (userId is null)
        {
            return StatusCode(401, "Not authorized");
        }
        var groupAdminId = await _context.GroupMembers
        .Where(g => g.GroupId == group.Id && g.IsAdmin == true)
        .Select(user => user.UserId)
        .AsNoTracking()
        .FirstOrDefaultAsync();

        if (groupAdminId is null)
        {
            return BadRequest("Group dosent have any admin");
        }

        if (groupAdminId != userId)
        {
            return StatusCode(401, "Only group Admin can delete the group");
        }

        var isGroupDeleted = await _context.Groups
        .Where(g => g.Id == group.Id)
        .ExecuteDeleteAsync();
        await _context.SaveChangesAsync();


        return Ok("Group Deleted Successfully");
    }

    [HttpPut]
    [Route("update/{id}")]
    public async Task<IActionResult> UpdateGroup(string id, [FromBody] UpdateGroupDto updateGroup)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var userId = User.FindFirstValue("Id");
        if (userId is null)
        {
            return StatusCode(401, "Not authorized");
        }
        var group = await _context.Groups.FindAsync(id);
        if (group is null)
        {
            return BadRequest("Group with Given id doesnot exists");
        }
        var groupAdminId = await _context.GroupMembers
        .Where(g => g.GroupId == group.Id && g.IsAdmin)
        .Select(g => g.UserId)
        .AsNoTracking()
        .FirstOrDefaultAsync();

        if (groupAdminId == null)
        {
            return BadRequest("Group doesnt have any admin");
        }


        if (groupAdminId != userId)
        {
            return StatusCode(401, "Only group Admin can delete the group");
        }
        group.Description = updateGroup.Description;
        group.GroupName = updateGroup.GroupName;
        _context.Groups.Update(group);
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Group Updated" });
    }

    [HttpPatch]
    [Route("remove/{id}")]
    public async Task<IActionResult> RemoveFromGroup(string id, [FromBody] RemoveFromGroupDto removeFromGroupDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        if (removeFromGroupDto.UserIds is null || removeFromGroupDto.UserIds.Count == 0)
        {
            return BadRequest("please provie the id of user to be removed");
        }
        var group = await _context.Groups
        .Include(g => g.GroupMembers)
        .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
        {
            return BadRequest("Group of give id not found");
        }

        var membersToRemove = group
        .GroupMembers
        .Where(gm => removeFromGroupDto.UserIds.Contains(gm.UserId))
        .ToList();

        if (membersToRemove.Count <= 0)
        {
            return BadRequest("No users provided exist in the group");
        }
        if ((group.GroupMembers.Count - membersToRemove.Count) < 2)
        {
            return BadRequest("Removing mmembers will make a group of single user either delete or reduce the list of members");
        }

        _context.GroupMembers.RemoveRange(membersToRemove);
        await _context.SaveChangesAsync();
        return Ok("Members removed successfuly");
    }

    [HttpPatch]
    [Route("add/{id}")]
    public async Task<IActionResult> AddMembersToGroup(string id, [FromBody] AddToGroupDto addToGroup)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        if (addToGroup.UserIds == null || addToGroup.UserIds.Count == 0)
        {
            return BadRequest("UserIds list cannot be null or empty.");
        }
        var group = await _context.Groups
        .Include(g => g.GroupMembers)
        .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
        {
            return BadRequest("Group of give id not found");
        }
        if ((group.GroupMembers.Count + addToGroup.UserIds.Count) > 50)
        {
            return BadRequest("Group cant have more than 50 members.");
        }
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var usersToAdd = addToGroup.UserIds
                .Select(userId => _userManager.FindByIdAsync(userId))
                .ToList();
            var users = await Task.WhenAll(usersToAdd);
            var groupMembers = new List<GroupMembers>();
            foreach (var user in users)
            {
                if (user is null)
                {
                    throw new Exception("provile a list of valid user Ids");
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
                    return BadRequest("No users found of given id");
                }

                await _context.GroupMembers.AddRangeAsync(groupMembers);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            return Ok("Member Added Sucessfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { Message = "An error occurred while creating the group.", Error = ex.Message });
        }
    }
    [HttpGet]
    [Route("settle/{id}")]
    public async Task<IActionResult> GetExpenseSettlement(string id)
    {
        var expenseShares = await _context.ExpenseShares
        .Where(es => es.Expense.GroupId == id)
        .Include(es => es.Expense)
        .ToListAsync();

        var userBalances = await _context.UserBalances
        .Where(ub => ub.GroupId == id)
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
        if (userBalanceSheet.Count != dbUserBalanceSheet.Count) return StatusCode(500, "The error ocoured beacuse the datat is not accurate ");
        foreach (var dbKey in dbUserBalanceSheet)
        {
            if (userBalanceSheet.TryGetValue(dbKey.Key, out decimal value)) return StatusCode(500, "The error ocoured beacuse the datat is not accurate ");
            if (dbKey.Value != value) return StatusCode(500, "The error ocoured beacuse the datat is not accurate ");
        }

        return Ok("");
    }
}