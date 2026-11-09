using Core.Infrastructure.Data.Repositories;
using DatabaseBroker.DataContext;
using Entity.Models.Auth;

namespace DatabaseBroker.Repositories.Auth;

public class StructureRepository(AuthDataContext dbContext)
    : RepositoryBase<Structure, long>(dbContext), IStructureRepository;
