

using System.ComponentModel.DataAnnotations;

namespace Splitwise_Back.Models.Dtos;

public class CreateGroupDto
{
    [Required]
    public required string GroupName { get; set; }
    public required string Description { get; set; }
    public List<string>? UserIds { get; set; }
    [Required]
    public required string CreatedByUserId { get; set; }
}
public class ReadGroupDto
{
    public int Id { get; set; }
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; }
    public List<GroupMemberDto>? GroupMembers { get; set; } // Or create another DTO for members
}
public class GroupMemberDto
{
    public string? UserId { get; set; } // Add any other relevant properties
    public string? UserName { get; set; } // Assuming you want the name of the member
}
public class UpdateGroupDto
{
    [Required]
    public required string GroupName { get; set; }
    public required string Description { get; set; }
}