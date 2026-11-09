using System.ServiceModel;
using ProtoBuf;
using ProtoBuf.Grpc;

namespace Entity.Contracts.StructurePermissions;

/// <summary>
/// Mikroservis tomonidan faqat o'ziga tegishli struktura (rol) va ruxsat kodlarini AuthApi dan olish so'rovi.
/// Ortiqcha yoki begona servis ma'lumotlari qaytarilmaydi.
/// </summary>
[ProtoContract]
public class GetServiceStructurePermissionsRequest
{
    /// <summary>
    /// Servis nomi (masalan: "QuranCourse", "OrderService").
    /// </summary>
    [ProtoMember(1)]
    public string? ServiceName { get; set; }

    /// <summary>
    /// Servis kodi prefiksi (masalan: 60).
    /// </summary>
    [ProtoMember(2)]
    public int? ServiceCode { get; set; }

    /// <summary>
    /// Servisning o'ziga tegishli ruxsat kodlari ro'yxati (ixtiyoriy, filtrlash uchun).
    /// </summary>
    [ProtoMember(3)]
    public List<int> PermissionCodes { get; set; } = [];
}

/// <summary>
/// Yagona struktura va unga tegishli faqat ushbu mikroservisning ruxsat kodlari.
/// </summary>
[ProtoContract]
public class StructurePermissionItem
{
    /// <summary>
    /// Struktura (Role) ID si.
    /// </summary>
    [ProtoMember(1)]
    public long StructureId { get; set; }

    /// <summary>
    /// Ushbu strukturada mavjud bo'lgan, faqat so'ralgan servisga tegishli ruxsat kodlari ro'yxati.
    /// </summary>
    [ProtoMember(2)]
    public List<int> PermissionCodes { get; set; } = [];
}

/// <summary>
/// Mikroservisga tegishli strukturalar va ularning ruxsat kodlari javobi.
/// </summary>
[ProtoContract]
public class GetServiceStructurePermissionsResponse
{
    /// <summary>
    /// Muvaffaqiyatli yakunlanganligi.
    /// </summary>
    [ProtoMember(1)]
    public bool Success { get; set; }

    /// <summary>
    /// Strukturalar va ruxsatlar ro'yxati (faqat so'ralgan servisga tegishli).
    /// </summary>
    [ProtoMember(2)]
    public List<StructurePermissionItem> Items { get; set; } = [];

    /// <summary>
    /// Xabar yoki xatolik matni.
    /// </summary>
    [ProtoMember(3)]
    public string? Message { get; set; }
}

/// <summary>
/// Mikroservislar start vaqtida faqat o'ziga tegishli struktura va ruxsatlarni
/// AuthApi dan Code-First gRPC orqali olishi uchun servis shartnomasi.
/// </summary>
[ServiceContract]
public interface IStructurePermissionGrpcService
{
    /// <summary>
    /// Mikroservisga tegishli strukturalar va ularning ruxsat kodlarini qaytaradi.
    /// Begona servislarning ruxsatlari filtrlanadi va qaytarilmaydi.
    /// </summary>
    [OperationContract]
    Task<GetServiceStructurePermissionsResponse> GetServiceStructurePermissionsAsync(
        GetServiceStructurePermissionsRequest request,
        CallContext context = default);
}
