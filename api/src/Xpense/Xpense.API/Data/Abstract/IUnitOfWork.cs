namespace Xpense.API.Data.Abstract;

public interface IUnitOfWork
{
    public int Commit();
}