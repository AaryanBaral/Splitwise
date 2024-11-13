using MediatR;
using Splitwise_Back.Events.GroupEvents;
using Splitwise_Back.Services.Expense;

namespace Splitwise_Back.EventHandler.GroupEventHandler;

public class GroupDeleteHandlerForExpense:INotificationHandler<GroupDeleteEvent>
{
    private readonly IExpenseService _expenseService;
    public GroupDeleteHandlerForExpense(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }
    public async Task Handle(GroupDeleteEvent notification, CancellationToken cancellationToken)
    {
       await _expenseService.DeleteAllExpenses(notification.GroupId);
    }
}