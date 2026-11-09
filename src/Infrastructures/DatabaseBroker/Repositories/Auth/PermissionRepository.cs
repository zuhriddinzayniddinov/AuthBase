using Core.Infrastructure.Data.Repositories;
using DatabaseBroker.DataContext;
using Entity.Models.Auth;

namespace DatabaseBroker.Repositories.Auth;

public class PermissionRepository(AuthDataContext dbContext)
    : RepositoryBase<Permission, long>(dbContext), IPermissionRepository;
