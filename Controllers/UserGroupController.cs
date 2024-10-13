using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserGroupController(ILogger<UserGroupController> logger, AppDbContext context, UserManager<CustomUser> userManager) : Controller
    {
        private readonly ILogger<UserGroupController> _logger = logger;
        private readonly AppDbContext _context = context;
        private readonly UserManager<CustomUser> _userManager = userManager;



        [HttpPost]
        [Route("create")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto groupDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (groupDto.UserIds is null)
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
                        JoinDate = DateTime.Now
                    }
                };
                var usersToAdd = groupDto.UserIds
                .Select(userId => _userManager.FindByIdAsync(userId))
                .ToList();
                var users = await Task.WhenAll(usersToAdd);
                foreach (var user in users)
                {
                    if (user != null)
                    {
                        groupMembers.Add(new GroupMembers
                        {
                            GroupId = newGroup.Id,
                            UserId = user.Id,
                            IsAdmin = false,
                            JoinDate = DateTime.Now
                        });
                    }
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

                // Log the exception (you can use a logging framework here)
                return StatusCode(500, new { Message = "An error occurred while creating the group.", Error = ex.Message });
            }
        }


    }
}