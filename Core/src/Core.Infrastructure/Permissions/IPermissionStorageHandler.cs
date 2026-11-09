using Core.Domain.Permissions;

namespace Core.Infrastructure.Permissions;

/// <summary>
/// AuthApi tomonida mikroservislardan kelgan ruxsatlarni (RabbitMQ yoki gRPC orqali)
/// qabul qilib, o'zining ma'lumotlar bazasiga (Permissions jadvaliga) saqlash
/// yoki yangilash uchun ishlatiladigan interfeys.
/// AuthApi ushbu interfeysni amalga oshiradi (implement qiladi).
/// </summary>
public interface IPermissionStorageHandler
{
    /// <summary>
    /// Mikroservisdan kelgan barcha ruxsatlar to'plamini bazaga saqlaydi yoki yangilaydi (Upsert).
    /// </summary>
    /// <param name="serviceName">Ruxsatlarni yuborgan mikroservis nomi.</param>
    /// <param name="serviceCode">Mikroservis kodi (masalan: 60).</param>
    /// <param name="permissions">Kelgan ruxsatlar ro'yxati.</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    /// <returns>Qayta ishlangan ruxsatlar soni.</returns>
    Task<int> HandlePermissionsReceivedAsync(
        string serviceName,
        int? serviceCode,
        IReadOnlyList<PermissionDefinition> permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mikroservis uchun faqat unga tegishli strukturalar va ruxsat kodlari lug'atini (StructureId -> PermissionCodes) qaytaradi.
    /// Begona yoki boshqa servislarga tegishli ruxsatlar filtrlanadi va berilmaydi.
    /// </summary>
    /// <param name="serviceName">Mikroservis nomi.</param>
    /// <param name="serviceCode">Mikroservis prefiks kodi.</param>
    /// <param name="permissionCodes">Mikroservis ruxsat kodlari ro'yxati (ixtiyoriy filter).</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    /// <returns>Struktura ID si va unga tegishli ushbu servisning ruxsat kodlari lug'ati.</returns>
    Task<Dictionary<long, List<int>>> GetStructurePermissionsForServiceAsync(
        string? serviceName,
        int? serviceCode,
        IReadOnlyList<int>? permissionCodes,
        CancellationToken cancellationToken = default);
}
