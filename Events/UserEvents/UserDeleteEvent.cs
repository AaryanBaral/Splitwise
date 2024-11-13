using MediatR;

namespace Splitwise_Back.Events.UserEvents;

public class UserDeleteEvent:INotification
{
    public string UserId { get; set; } 
    public string LoggedInUserId { get; set; }
    public UserDeleteEvent(string userId, string loggedInUserId)
    {
        UserId = userId;
        LoggedInUserId = loggedInUserId;
    }
}