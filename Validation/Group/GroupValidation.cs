
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Validation.Group;

public class GroupValidation
{
    private readonly AppDbContext _context;
    public GroupValidation(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<Groups> ValidateExpenseSharedMembersInGroup(List<ExpenseSharedMembers> groupMembers, string groupId)
    {
        // Validate Group
        var group = await _context.Groups
            .Include(g => g.GroupMembers)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group is null)
        {
            throw new KeyNotFoundException("Group with given groupId could not be found.");
        }
        
        // Validate Share Participants
        var userIdsInGroup = group.GroupMembers.Select(gm => gm.UserId).ToHashSet();
        var invalidUsers = groupMembers.Where(es => !userIdsInGroup.Contains(es.UserId))
            .ToList();
        if (invalidUsers.Count != 0)
        {
            throw new InvalidOperationException("One or more users are not in the group.");
        }
        return group;
    }
}