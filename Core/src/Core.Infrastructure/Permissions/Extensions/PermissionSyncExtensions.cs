using Core.Domain.Permissions;
using Core.Infrastructure.Permissions.Consumers;
using Core.Infrastructure.Permissions.Grpc;
using Core.Infrastructure.Permissions.Services;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.ClientFactory;

namespace Core.Infrastructure.Permissions.Extensions;

/// <summary>
/// Ruxsatlarni (Permissions) avtomatik sinxronlashtirish va qabul qilish uchun universal DI kengaytmalari.
/// Har qanday mikroservis va qabul qiluvchi xizmat uchun to'liq moslashuvchan (database-agnostic).
/// </summary>
public static class PermissionSyncExtensions
{
    /// <summary>
    /// Mikroservisning o'z lokal ruxsatlar enum'ini ro'yxatdan o'tkazadi va
    /// servis ishga tushganda avtomatik tarzda markaziy xizmatga (RabbitMQ yoki gRPC orqali)
    /// yuborilishini ta'minlaydi.
    /// </summary>
    /// <typeparam name="TEnum">Mikroservisning ruxsatlar enum turi (masalan: QuranPermissions).</typeparam>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="mode">Yuborish usuli (sukut bo'yicha RabbitMq).</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// // Mikroservis Program.cs da:
    /// builder.Services.AddCorePermissionAutoSync&lt;QuranPermissions&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCorePermissionAutoSync<TEnum>(
        this IServiceCollection services,
        PermissionSyncMode mode = PermissionSyncMode.RabbitMq)
        where TEnum : struct, Enum
    {
        return services.AddCorePermissionAutoSync(mode, typeof(TEnum));
    }

    /// <summary>
    /// Bir nechta lokal ruxsatlar enum turlarini avtomatik sinxronlashtirish uchun ro'yxatdan o'tkazadi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="mode">Yuborish usuli.</param>
    /// <param name="enumTypes">Enum turlari ro'yxati.</param>
    /// <returns>Servislar to'plami.</returns>
    public static IServiceCollection AddCorePermissionAutoSync(
        this IServiceCollection services,
        PermissionSyncMode mode = PermissionSyncMode.RabbitMq,
        params Type[] enumTypes)
    {
        services.TryAddSingleton<IPermissionSyncSender>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PermissionSyncSender>>();
            return new PermissionSyncSender(enumTypes, sp, logger);
        });

        services.AddHostedService(sp =>
        {
            var sender = sp.GetRequiredService<IPermissionSyncSender>();
            var logger = sp.GetRequiredService<ILogger<PermissionAutoSyncHostedService>>();
            return new PermissionAutoSyncHostedService(sender, mode, logger);
        });

        return services;
    }

    /// <summary>
    /// Ruxsatlarni qabul qiluvchi xizmat tomonida (masalan, Auth yoki Permission Service)
    /// barcha mikroservislardan kelgan ruxsatlarni qabul qiluvchi tizimni ro'yxatdan o'tkazadi.
    /// Ham RabbitMQ Consumer hamda gRPC server servisini biriktiradi.
    /// Qabul qilingan ruxsatlarni saqlash logikasini <see cref="IPermissionStorageHandler"/> implementatsiyasi hal qiladi.
    /// </summary>
    /// <typeparam name="TStorageHandler">Ruxsatlarni qabul qilib oluvchi/saqlovchi xizmat implementatsiyasi.</typeparam>
    /// <param name="services">DI servislar to'plami.</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCorePermissionReceiver&lt;MyPermissionStorageHandler&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCorePermissionReceiver<TStorageHandler>(
        this IServiceCollection services)
        where TStorageHandler : class, IPermissionStorageHandler
    {
        services.TryAddScoped<IPermissionStorageHandler, TStorageHandler>();
        services.TryAddScoped<PermissionSyncRabbitMqConsumer>();
        services.TryAddScoped<PermissionSyncGrpcService>();

        return services;
    }

    /// <summary>
    /// Ruxsatlarni qabul qiluvchi xizmat tomonida receiver tizimini va shu servisning o'z lokal ruxsatlar enum'ini
    /// start vaqtida to'g'ridan-to'g'ri (Direct Mode) <see cref="IPermissionStorageHandler"/> ga uzatishni sozlaydi.
    /// </summary>
    /// <typeparam name="TStorageHandler">Ruxsatlarni qabul qilib oluvchi/saqlovchi xizmat implementatsiyasi.</typeparam>
    /// <typeparam name="TLocalEnum">Servisning o'z ruxsatlar enum turi.</typeparam>
    /// <param name="services">DI servislar to'plami.</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCorePermissionReceiver&lt;MyPermissionStorageHandler, MyPermissions&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCorePermissionReceiver<TStorageHandler, TLocalEnum>(
        this IServiceCollection services)
        where TStorageHandler : class, IPermissionStorageHandler
        where TLocalEnum : struct, Enum
    {
        services.AddCorePermissionReceiver<TStorageHandler>();
        services.AddCorePermissionAutoSync<TLocalEnum>(PermissionSyncMode.Direct);
        return services;
    }

    /// <summary>
    /// Ruxsatlarni qabul qiluvchi xizmat tomonida receiver tizimini va bir nechta lokal ruxsatlar enum'larini
    /// start vaqtida to'g'ridan-to'g'ri (Direct Mode) <see cref="IPermissionStorageHandler"/> ga uzatishni ro'yxatdan o'tkazadi.
    /// </summary>
    public static IServiceCollection AddCorePermissionReceiver<TStorageHandler>(
        this IServiceCollection services,
        params Type[] localEnumTypes)
        where TStorageHandler : class, IPermissionStorageHandler
    {
        services.AddCorePermissionReceiver<TStorageHandler>();
        if (localEnumTypes is { Length: > 0 })
        {
            services.AddCorePermissionAutoSync(PermissionSyncMode.Direct, localEnumTypes);
        }
        return services;
    }

    /// <summary>
    /// MassTransit bus konfiguratsiyasiga boshqa mikroservislardan keluvchi ruxsatlar
    /// integratsion hodisalarini qabul qiluvchi consumerni (<see cref="PermissionSyncRabbitMqConsumer"/>) ro'yxatdan o'tkazadi.
    /// </summary>
    /// <param name="bus">MassTransit bus registratori.</param>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreMassTransit&lt;AppDbContext&gt;(builder.Configuration, bus =>
    /// {
    ///     bus.AddPermissionSyncConsumer();
    /// });
    /// </code>
    /// </example>
    public static void AddPermissionSyncConsumer(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<PermissionSyncRabbitMqConsumer>();
    }

    /// <summary>
    /// Mikroservisda AuthApi gRPC mijozi orqali IPermissionSyncGrpcService ni ro'yxatdan o'tkazadi.
    /// Ushbu mijoz yordamida servis start vaqtida ruxsatlarni sinxronlashtiradi va o'ziga tegishli
    /// struktura ruxsatlarini qabul qilib oladi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="authApiGrpcAddress">AuthApi gRPC manzili (masalan: "http://localhost:50051" yoki "http://auth-api:50051").</param>
    public static IServiceCollection AddCorePermissionSyncGrpcClient(
        this IServiceCollection services,
        string authApiGrpcAddress)
    {
        services.AddCodeFirstGrpcClient<IPermissionSyncGrpcService>(o =>
        {
            o.Address = new Uri(authApiGrpcAddress);
        });

        return services;
    }
}
