namespace Splitwise_Back.Data.Transaction;

public interface IUnitOfWork: IDisposable
{
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}