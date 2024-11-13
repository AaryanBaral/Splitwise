using MediatR;
using Splitwise_Back.Events.UserEvents;
using Splitwise_Back.Services.Group;

namespace Splitwise_Back.EventHandler.UserEventHandler;

public class UserDeleteEventHandlerForGroup:INotificationHandler<UserDeleteEvent>
{
    private readonly IGroupService _groupService;
    public UserDeleteEventHandlerForGroup(IGroupService groupService)
    {
        _groupService = groupService;
    }
    public async Task Handle(UserDeleteEvent notification, CancellationToken cancellationToken)
    {
        // 1. Fetch all groups created by the user
        var results = await _groupService.GetGroupByCreator(notification.UserId);
        var groups = results.Data;
        if (groups is null)
        {
            return;
        }
        
        // 2. Delete all groups associated with the deleted user
        foreach (var group in groups)
        {
            await _groupService.DeleteGroup(group.Id, notification.LoggedInUserId);
        }
    }
}