using Core.Infrastructure.Data.Repositories;
using Entity.Models.Auth;

namespace DatabaseBroker.Repositories.Auth;

public interface ISignMethodsRepository : IRepositoryBase<SignMethod, long>
{
}
