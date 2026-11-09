using Core.Domain.Common.Context;
using Core.Domain.Enums;
using Core.Infrastructure.Data.Context;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace DatabaseBroker.DataContext;

public class AuthDataContext : CoreDbContext
{
    public AuthDataContext(
        DbContextOptions<AuthDataContext> options,
        IServiceProvider? serviceProvider = null,
        ITenantContext? tenantContext = null)
        : base(options, serviceProvider, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .Entity<SignMethod>()
            .HasOne(x => x.User)
            .WithMany(x => x.SignMethods)
            .HasForeignKey(x => x.UserId);

        modelBuilder
            .Entity<SignMethod>()
            .HasDiscriminator(x => x.Type)
            .HasValue<DefaultSignMethod>(SignMethods.Normal);
    }

    public DbSet<Permission> Permissions { get; set; }
    public DbSet<Structure> Structures { get; set; }
    public DbSet<StructurePermission> StructurePermissions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserStructure> UserStructures { get; set; }
    public DbSet<SignMethod> UserSignMethods { get; set; }
    public DbSet<TokenModel> Tokens { get; set; }
}
