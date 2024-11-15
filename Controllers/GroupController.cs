using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitwise_Back.Data;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.Group;

namespace Splitwise_Back.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ApiController]
[Route("api/[controller]")]
public class GroupController : Controller
{
    private readonly ILogger<GroupController> _logger;
    private readonly IGroupService _groupService;
    private readonly AppDbContext _context;

    public GroupController(ILogger<GroupController> logger,
        IGroupService groupService, AppDbContext context)
    {
        _logger = logger;
        _groupService = groupService;
        _context = context;
    }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto groupDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await _groupService.CreateGroupAsync(groupDto);
            await transaction.CommitAsync();
            return StatusCode(result.StatusCode,
                new { Data = result.Data, Success = result.Success, Errors = result.Errors });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
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
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var userId = User.FindFirstValue("Id");
            if (userId is null)
            {
                return StatusCode(401, "Not authorized");
            }

            var result = await _groupService.DeleteGroup(id, userId);
            await transaction.CommitAsync();
            return StatusCode(result.StatusCode,
                new { Data = result.Data, Success = result.Success, Errors = result.Errors });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPut]
    [Route("update/{id}")]
    public async Task<IActionResult> UpdateGroup(string id, [FromBody] UpdateGroupDto updateGroup)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var userId = User.FindFirstValue("Id");
            if (userId is null)
            {
                return StatusCode(401, "Not authorized");
            }

            var result = await _groupService.UpdateGroup(updateGroup, id, userId);
            await transaction.CommitAsync();
            return StatusCode(result.StatusCode,
                new { Data = result.Data, Success = result.Success, Errors = result.Errors });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPatch]
    [Route("remove/{id}")]
    public async Task<IActionResult> RemoveFromGroup(string id, [FromBody] RemoveFromGroupDto removeFromGroupDto)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _groupService.RemoveMembersFromGroup(removeFromGroupDto, id);
            await transaction.CommitAsync();
            return StatusCode(result.StatusCode,
                new { Data = result.Data, Success = result.Success, Errors = result.Errors });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPatch]
    [Route("add/{id}")]
    public async Task<IActionResult> AddMembersToGroup(string id, [FromBody] AddToGroupDto addToGroup)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await _groupService.AddMembersToGroup(addToGroup, id);
            await transaction.CommitAsync();
            return StatusCode(result.StatusCode,
                new { Data = result.Data, Success = result.Success, Errors = result.Errors });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpGet]
    [Route("settle/greedy/{id}")]
    public async Task<IActionResult> GetExpenseSettlementByGreedy(string id)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await _groupService.GetSettlementOfGroupByGreedy(id);
            await transaction.CommitAsync();
            return StatusCode(result.StatusCode,
                new { Data = result.Data, Success = result.Success, Errors = result.Errors });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}