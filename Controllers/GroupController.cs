using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("[controller]")]
    public class GroupController(ILogger<GroupController> logger, AppDbContext context, UserManager<CustomUser> userManager, IMapper mapper) : Controller
    {
        private readonly ILogger<GroupController> _logger = logger;
        private readonly AppDbContext _context = context;
        private readonly IMapper _mapper = mapper;
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
                    if (user is null)
                    {
                        throw new Exception("User Id must be valid");
                    }
                    if (user.Id == groupDto.CreatedByUserId)
                    {
                        continue;
                    }
                    groupMembers.Add(new GroupMembers
                    {
                        GroupId = newGroup.Id,
                        UserId = user.Id,
                        IsAdmin = false,
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

                // Log the exception (you can use a logging framework here)
                return StatusCode(500, new { Message = "An error occurred while creating the group.", Error = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var AllGroups = await _context.Groups
            .Include(g => g.GroupMembers)
            .ThenInclude(gm=>gm.User)
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
                GroupMembers = g.GroupMembers.Select(m => new GroupMemberDto
                {
                    UserId = m.UserId, // Replace with actual property from GroupMembers
                    UserName = m.User?.UserName // Assuming this exists; adjust accordingly
                }).ToList()
            }).ToList();
            return Ok(readGroupDto);
        }

        [HttpGet]
        [Route("{id}")]
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
        [Route("/delete/{id}")]
        public async Task<IActionResult> DeleteGroup(string id)
        {
            var group = await _context.Groups.FindAsync(id);
            if (group is null)
            {
                return BadRequest("Group with Given id doesnot exists");
            }
            var groupAdminId = _context.GroupMembers
            .Where(g => g.GroupId == group.Id && g.IsAdmin == true).AsNoTracking();

            /*
            
            
            
             Check if the user requesting the request is the admin of the group or not 


            */

            var isGroupDeleted = await _context.Groups
            .Where(g => g.Id == group.Id)
            .ExecuteDeleteAsync();
            await _context.SaveChangesAsync();


            return Ok("Group Deleted Successfully");
        }

        [Authorize]
        [HttpPut]
        [Route("/update/{id}")]
        public async Task<IActionResult> UpdateGroup(string id, [FromBody] UpdateGroupDto updateGroup)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var userId = User.FindFirstValue("Id");
            Console.WriteLine(userId);
            var group = await _context.Groups.FindAsync(id);
            if (group is null)
            {
                return BadRequest("Group with Given id doesnot exists");
            }
            var groupAdminId = _context.GroupMembers
            .Where(g => g.GroupId == group.Id && g.IsAdmin)
            .Select(g => g.UserId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

            if (groupAdminId == null)
            {
                return BadRequest("No admin found for the group");
            }

            /*
            
             Check if the user requesting the request is the admin of the group or not 
             if not return not authorized


            */
            group.Description = updateGroup.Description;
            group.GroupName = updateGroup.GroupName;
            _context.Groups.Update(group);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Group Updated" });
        }


    }
}