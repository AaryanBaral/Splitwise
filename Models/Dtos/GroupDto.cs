

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
public class UpdateGroupDto
{

}