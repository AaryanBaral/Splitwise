using Splitwise_Back.Models;
using Splitwise_Back.Models.Dto;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Services.Group;

public interface IGroupService
{
    Task<ResponseResults<string>> CreateGroupAsync(CreateGroupDto createGroupDto);
    Task<ResponseResults<List<ReadGroupDto>>> GetAllGroups();

    Task<ResponseResults<string>> UpdateGroup(UpdateGroupDto updateGroup, string id, string currentUserId);
    Task<ResponseResults<string>> RemoveMembersFromGroup(RemoveFromGroupDto removeFromGroupDto, string id);
    Task<ResponseResults<string>> AddMembersToGroup(AddToGroupDto addToGroup, string id);
   Task<ResponseResults<List<TransactionResults>>> GetSettlementOfGroupByGreedy(string id);
   Task<ResponseResults<List<TransactionResults>>> SettleGroup(string id);
   public bool IsGroupSettled(string id);
   Task<ResponseResults<string>> DeleteGroup(string id, string currentUser);
   Task<ResponseResults<List<ReadGroupDto>>> GetGroupByCreator(string id);

}