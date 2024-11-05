using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Services.Group;

public interface IGroupService
{
    Task<GroupResults<string>> CreateGroupAsync(CreateGroupDto createGroupDto);
    
}