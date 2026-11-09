using System.ServiceModel;
using Core.Domain.Permissions;
using ProtoBuf.Grpc;

namespace Core.Infrastructure.Permissions.Grpc;

/// <summary>
/// Ruxsatlarni AuthApi ga sinxron ravishda ro'yxatdan o'tkazish uchun Code-First gRPC xizmati interfeysi.
/// </summary>
[ServiceContract]
public interface IPermissionSyncGrpcService
{
    /// <summary>
    /// Mikroservis ruxsatlarini AuthApi ga yuborish va bazaga saqlash/yangilash metodi.
    /// </summary>
    /// <param name="request">Ruxsatlar ro'yxatini o'z ichiga olgan so'rov.</param>
    /// <param name="context">gRPC chaqiruv konteksti.</param>
    /// <returns>Ro'yxatdan o'tganlik holati haqida javob.</returns>
    [OperationContract]
    Task<SyncPermissionsResponse> SyncPermissionsAsync(SyncPermissionsRequest request, CallContext context = default);

    /// <summary>
    /// Mikroservis ishga tushganda faqat o'ziga tegishli strukturalar (rollar) va ruxsat kodlarini
    /// AuthApi dan olish metodi. Begona servislar ruxsatlari qaytarilmaydi.
    /// </summary>
    /// <param name="request">Servis nomi, kodi yoki ruxsat kodlari filtri.</param>
    /// <param name="context">gRPC chaqiruv konteksti.</param>
    /// <returns>Struktura ID lari va ularga tegishli ruxsat kodlari xaritasi.</returns>
    [OperationContract]
    Task<GetServiceStructurePermissionsResponse> GetServiceStructurePermissionsAsync(GetServiceStructurePermissionsRequest request, CallContext context = default);
}
