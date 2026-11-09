using AuthService.Services;
using DatabaseBroker.Repositories.Auth;
using Entity.Models.Configurations;
using MinIoBroker.Services;
using RoleService.Service;

namespace AuthApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfig(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinIoConfiguration>(configuration.GetSection("Minio"));
        return services;
    }

    public static IServiceCollection AddService(this IServiceCollection services)
    {
        services.AddScoped<HttpClient>();
        services.AddScoped<IAuthService, AuthService.Services.AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService.Service.RoleService>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStructureRepository, StructureRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IStructurePermissionRepository, StructurePermissionRepository>();
        services.AddScoped<ITokenRepository, TokenRepository>();
        services.AddScoped<ISignMethodsRepository, SignMethodsRepository>();
        services.AddScoped<IMinioService, MinioService>();

        return services;
    }
}
