using System.Reflection;
using Core.Domain.Permissions;
using Core.Infrastructure.Permissions.Grpc;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Permissions.Services;

/// <summary>
/// Mikroservis ruxsatlarini (Permissions) yig'ib, ularni RabbitMQ, gRPC
/// yoki to'g'ridan-to'g'ri (Direct) AuthApi DB ga saqlovchi implementatsiya.
/// </summary>
public class PermissionSyncSender : IPermissionSyncSender
{
    private readonly IEnumerable<Type> _enumTypes;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionSyncSender> _logger;

    /// <summary>
    /// <see cref="PermissionSyncSender"/> konstruktori.
    /// </summary>
    public PermissionSyncSender(
        IEnumerable<Type> enumTypes,
        IServiceProvider serviceProvider,
        ILogger<PermissionSyncSender> logger)
    {
        _enumTypes = enumTypes;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SyncViaRabbitMqAsync(CancellationToken cancellationToken = default)
    {
        var publishEndpoint = _serviceProvider.GetService<IPublishEndpoint>();
        if (publishEndpoint == null)
        {
            _logger.LogWarning("MassTransit IPublishEndpoint topilmadi. Ruxsatlarni RabbitMQ orqali yuborib bo'lmadi.");
            return;
        }

        var events = BuildEvents();
        foreach (var @event in events)
        {
            _logger.LogInformation(
                "RabbitMQ ga ruxsatlar yuborilmoqda: Servis: {ServiceName}, Soni: {Count}",
                @event.ServiceName,
                @event.Permissions.Count);

            await publishEndpoint.Publish(@event, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SyncViaGrpcAsync(CancellationToken cancellationToken = default)
    {
        var grpcClient = _serviceProvider.GetService<IPermissionSyncGrpcService>();
        if (grpcClient == null)
        {
            _logger.LogWarning("IPermissionSyncGrpcService mijoz topilmadi. Ruxsatlarni gRPC orqali yuborib bo'lmadi.");
            return;
        }

        var events = BuildEvents();
        foreach (var ev in events)
        {
            _logger.LogInformation(
                "gRPC orqali AuthApi ga ruxsatlar yuborilmoqda: Servis: {ServiceName}, Soni: {Count}",
                ev.ServiceName,
                ev.Permissions.Count);

            var request = new SyncPermissionsRequest
            {
                ServiceName = ev.ServiceName,
                ServiceCode = ev.ServiceCode,
                Permissions = ev.Permissions
            };

            var response = await grpcClient.SyncPermissionsAsync(request);
            if (response.Success)
            {
                _logger.LogInformation("gRPC orqali ruxsatlar muvaffaqiyatli saqlandi: {Message}", response.Message);
            }
            else
            {
                _logger.LogError("gRPC ruxsatlarni saqlashda xatolik: {Message}", response.Message);
            }
        }
    }

    /// <inheritdoc />
    public async Task SyncDirectAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var storageHandler = scope.ServiceProvider.GetService<IPermissionStorageHandler>();
        if (storageHandler == null)
        {
            _logger.LogWarning("IPermissionStorageHandler topilmadi. Lokal ruxsatlarni to'g'ridan-to'g'ri saqlab bo'lmadi.");
            return;
        }

        var events = BuildEvents();
        foreach (var ev in events)
        {
            _logger.LogInformation(
                "Lokal ruxsatlar to'g'ridan-to'g'ri bazaga saqlanmoqda: Servis: {ServiceName}, Soni: {Count}",
                ev.ServiceName,
                ev.Permissions.Count);

            var processed = await storageHandler.HandlePermissionsReceivedAsync(
                ev.ServiceName,
                ev.ServiceCode,
                ev.Permissions,
                cancellationToken);

            _logger.LogInformation(
                "{ServiceName} lokal servisi uchun {Processed} ta ruxsat to'g'ridan-to'g'ri saqlandi/yangilandi.",
                ev.ServiceName,
                processed);
        }
    }

    /// <inheritdoc />
    public async Task FetchAndLoadStructurePermissionsAsync(CancellationToken cancellationToken = default)
    {
        var events = BuildEvents();
        var allCodes = events.SelectMany(e => e.Permissions.Select(p => p.Code)).Distinct().ToList();
        var firstEvent = events.FirstOrDefault();

        // 1. Agar Direct rejim bo'lsa (AuthApi ning o'zida)
        using var scope = _serviceProvider.CreateScope();
        var storageHandler = scope.ServiceProvider.GetService<IPermissionStorageHandler>();
        if (storageHandler != null)
        {
            var directDict = await storageHandler.GetStructurePermissionsForServiceAsync(
                firstEvent?.ServiceName,
                firstEvent?.ServiceCode,
                allCodes.Count > 0 ? allCodes : null,
                cancellationToken);

            foreach (var kv in directDict)
            {
                CoreStaticCache.Permissions[kv.Key] = kv.Value;
            }

            _logger.LogInformation(
                "Lokal (Direct) rejimda {Count} ta struktura uchun ruxsatlar CoreStaticCache ga yuklandi.",
                directDict.Count);
            return;
        }

        // 2. Mikroservislar uchun gRPC orqali olish
        var grpcClient = _serviceProvider.GetService<IPermissionSyncGrpcService>();
        if (grpcClient == null)
        {
            _logger.LogWarning("IPermissionSyncGrpcService mijozi topilmadi. Struktura ruxsatlarini gRPC orqali olib bo'lmadi.");
            return;
        }

        var request = new GetServiceStructurePermissionsRequest
        {
            ServiceName = firstEvent?.ServiceName,
            ServiceCode = firstEvent?.ServiceCode,
            PermissionCodes = allCodes
        };

        _logger.LogInformation(
            "gRPC orqali AuthApi dan struktura ruxsatlari so'ralmoqda (Servis: {ServiceName}, Kodlar soni: {CodesCount})...",
            request.ServiceName,
            request.PermissionCodes.Count);

        var response = await grpcClient.GetServiceStructurePermissionsAsync(request);
        if (response.Success)
        {
            foreach (var item in response.Items)
            {
                CoreStaticCache.Permissions[item.StructureId] = item.PermissionCodes;
            }

            _logger.LogInformation(
                "gRPC orqali AuthApi dan {Count} ta struktura uchun ruxsatlar muvaffaqiyatli yuklandi.",
                response.Items.Count);
        }
        else
        {
            _logger.LogWarning("gRPC orqali struktura ruxsatlarini olishda xatolik: {Message}", response.Message);
        }
    }

    private List<SyncPermissionsIntegrationEvent> BuildEvents()
    {
        var result = new List<SyncPermissionsIntegrationEvent>();

        foreach (var enumType in _enumTypes)
        {
            var permissions = PermissionDiscoveryHelper.DiscoverFromEnum(enumType);
            if (permissions.Count == 0)
                continue;

            var first = permissions[0];
            var moduleAttr = enumType.GetCustomAttribute<PermissionsModuleAttribute>();
            var serviceName = moduleAttr?.ServiceName ?? first.ServiceName ?? enumType.Name;
            var serviceCode = moduleAttr?.ServiceCode ?? (permissions.Count > 0 ? permissions[0].Code / 1000000 : null);

            result.Add(new SyncPermissionsIntegrationEvent
            {
                ServiceName = serviceName,
                ServiceCode = serviceCode,
                Permissions = permissions
            });
        }

        return result;
    }
}
