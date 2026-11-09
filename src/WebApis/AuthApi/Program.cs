using AuthApi.Extensions;
using Core.Infrastructure.Permissions.Grpc;
using Core.Web.Extensions;
using DatabaseBroker.DataContext;
using DatabaseBroker.Seeders;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureDefault<AuthDataContext>();
builder.Services
    .AddConfig(builder.Configuration)
    .AddService()
    .AddInfrastructure();

var app = builder.Build();

await app.UpMigration<AuthDataContext>();
await app.SeedAuthDataAsync();

app.UseResponseCompression();
app.UseRateLimiter();

await app.ConfigureDefault();

app.MapGrpcService<PermissionSyncGrpcService>();
app.MapControllers();

app.Run();
