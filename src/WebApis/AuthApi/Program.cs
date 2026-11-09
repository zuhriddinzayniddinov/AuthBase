using AuthApi.Extensions;
using Core.Web.Extensions;
using DatabaseBroker.DataContext;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureDefault<AuthDataContext>();
builder.Services
    .AddConfig(builder.Configuration)
    .AddService()
    .AddInfrastructure();

var app = builder.Build();
await app.UpMigration<AuthDataContext>();
await app.ConfigureDefault();
app.MapControllers();
app.Run();
