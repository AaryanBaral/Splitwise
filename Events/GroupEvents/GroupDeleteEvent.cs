using MediatR;

namespace Splitwise_Back.Events.GroupEvents;

public class GroupDeleteEvent:INotification
{
    public string GroupId{ get; set; } 
    public GroupDeleteEvent(string groupId)
    {
        GroupId = groupId;
    }

}