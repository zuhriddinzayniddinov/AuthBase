namespace Core.Infrastructure.Permissions.Services;

/// <summary>
/// Ruxsatlarni sinxronlashtirish rejimi (qaysi transport orqali yuborish yoki saqlash).
/// </summary>
public enum PermissionSyncMode
{
    /// <summary>
    /// RabbitMQ orqali asinxron event yuborish (tavsiya etiladi).
    /// </summary>
    RabbitMq = 1,

    /// <summary>
    /// Code-First gRPC orqali to'g'ridan-to'g'ri AuthApi ga sinxron murojaat qilish.
    /// </summary>
    Grpc = 2,

    /// <summary>
    /// Ikkala kanal (RabbitMQ va gRPC) orqali yuborishga urinish.
    /// </summary>
    Both = 3,

    /// <summary>
    /// Tarmoqsiz, to'g'ridan-to'g'ri DI dagi <see cref="IPermissionStorageHandler"/> orqali bazaga saqlash.
    /// Ushbu rejim AuthApi ning o'zida lokal enumlarni start vaqtida to'g'ridan-to'g'ri bazaga yozish uchun ishlatiladi.
    /// </summary>
    Direct = 4
}

/// <summary>
/// Mikroservis ruxsatlarini AuthApi ga yuboruvchi yoki to'g'ridan-to'g'ri bazaga yozuvchi xizmat interfeysi.
/// </summary>
public interface IPermissionSyncSender
{
    /// <summary>
    /// Ro'yxatdan o'tgan barcha enum turlaridagi ruxsatlarni RabbitMQ orqali yuboradi.
    /// </summary>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task SyncViaRabbitMqAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ro'yxatdan o'tgan barcha enum turlaridagi ruxsatlarni gRPC orqali AuthApi ga yuboradi.
    /// </summary>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task SyncViaGrpcAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ro'yxatdan o'tgan barcha enum turlaridagi ruxsatlarni to'g'ridan-to'g'ri <see cref="IPermissionStorageHandler"/> orqali bazaga saqlaydi.
    /// AuthApi o'zining ichki enumlarini saqlashi uchun ishlatiladi.
    /// </summary>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task SyncDirectAsync(CancellationToken cancellationToken = default);
}
