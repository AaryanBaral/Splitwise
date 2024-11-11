using System.Runtime.Intrinsics.Arm;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dto;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.Group;

namespace Splitwise_Back.Controllers;

// [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class GroupController : Controller
{
    private readonly ILogger<GroupController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<CustomUsers> _userManager;
    private readonly IGroupService _groupService;

    public GroupController(ILogger<GroupController> logger, AppDbContext context, UserManager<CustomUsers> userManager,
        IGroupService groupService)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _groupService = groupService;
    }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto groupDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _groupService.CreateGroupAsync(groupDto);

        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }


    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _groupService.GetAllGroups();
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }

    [HttpGet]
    [Route("creator/{id}")]
    public async Task<IActionResult> GetGroupByCreator(string id)
    {
        var result = await _groupService.GetGroupByCreator(id);
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }

    [HttpDelete]
    [Route("delete/{id}")]
    public async Task<IActionResult> DeleteGroup(string id)
    {
        var userId = User.FindFirstValue("Id");
        if (userId is null)
        {
            return StatusCode(401, "Not authorized");
        }

        var result = await _groupService.DeleteGroup(id, userId);
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
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

        var result = await _groupService.UpdateGroup(updateGroup, id, userId);
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }

    [HttpPatch]
    [Route("remove/{id}")]
    public async Task<IActionResult> RemoveFromGroup(string id, [FromBody] RemoveFromGroupDto removeFromGroupDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _groupService.RemoveMembersFromGroup(removeFromGroupDto, id);
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }

    [HttpPatch]
    [Route("add/{id}")]
    public async Task<IActionResult> AddMembersToGroup(string id, [FromBody] AddToGroupDto addToGroup)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _groupService.AddMembersToGroup(addToGroup, id);
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }

    [HttpGet]
    [Route("settle/greedy/{id}")]
    public async Task<IActionResult> GetExpenseSettlementByGreedy(string id)
    {
        var result = await _groupService.GetSettlementOfGroupByGreedy(id);
        return StatusCode(result.StatusCode,
            new { Data = result.Data, Success = result.Success, Errors = result.Errors });
    }
}