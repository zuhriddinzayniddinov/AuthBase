using Core.Infrastructure.Data.Repositories;
using Entity.Models.Auth;

namespace DatabaseBroker.Repositories.Auth;

public interface ITokenRepository : IRepositoryBase<TokenModel, long>
{
}
