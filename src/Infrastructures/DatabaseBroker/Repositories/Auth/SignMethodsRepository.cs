using Core.Infrastructure.Data.Repositories;
using DatabaseBroker.DataContext;
using Entity.Models.Auth;

namespace DatabaseBroker.Repositories.Auth;

public class SignMethodsRepository(AuthDataContext dbContext)
    : RepositoryBase<SignMethod, long>(dbContext), ISignMethodsRepository;
