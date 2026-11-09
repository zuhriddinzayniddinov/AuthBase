using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DatabaseBroker.DataContext;

public class AuthDataContextFactory : IDesignTimeDbContextFactory<AuthDataContext>
{
    public AuthDataContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDataContext>();
        optionsBuilder.UseNpgsql("host=localhost;port=5432;database=auth_db;Username=postgres;password=1234");

        return new AuthDataContext(optionsBuilder.Options);
    }
}
