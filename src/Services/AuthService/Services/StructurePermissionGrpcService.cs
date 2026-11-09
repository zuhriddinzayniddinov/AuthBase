using DatabaseBroker.DataContext;
using Entity.Contracts.StructurePermissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;

namespace AuthService.Services;

/// <summary>
/// Mikroservislarning start vaqtida faqat o'zlariga tegishli struktura va ruxsat kodlarini
/// AuthApi dan gRPC orqali olishini ta'minlovchi xizmat.
/// Ortiqcha yoki begona servis ma'lumotlari uzatilmaydi.
/// </summary>
public class StructurePermissionGrpcService : IStructurePermissionGrpcService
{
    private readonly AuthDataContext _dbContext;
    private readonly ILogger<StructurePermissionGrpcService> _logger;

    public StructurePermissionGrpcService(
        AuthDataContext dbContext,
        ILogger<StructurePermissionGrpcService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GetServiceStructurePermissionsResponse> GetServiceStructurePermissionsAsync(
        GetServiceStructurePermissionsRequest request,
        CallContext context = default)
    {
        _logger.LogInformation(
            "Mikroservis o'zining struktura ruxsatlarini gRPC orqali so'radi. Servis: {ServiceName}, Kod: {ServiceCode}, Kodlar soni: {CodesCount}",
            request.ServiceName,
            request.ServiceCode,
            request.PermissionCodes?.Count ?? 0);

        try
        {
            var cancellationToken = context.CancellationToken;
            IQueryable<Entity.Models.Auth.Permission> query = _dbContext.Permissions.AsNoTracking();

            // 1. So'ralgan servisga tegishli ruxsatlarni filtrlaymiz
            if (request.PermissionCodes != null && request.PermissionCodes.Count > 0)
            {
                query = query.Where(p => request.PermissionCodes.Contains(p.Code));
            }
            else if (request.ServiceCode.HasValue)
            {
                var minCode = request.ServiceCode.Value * 1000000;
                var maxCode = (request.ServiceCode.Value + 1) * 1000000;
                query = query.Where(p => (p.Code >= minCode && p.Code < maxCode) || p.ServiceName == request.ServiceName);
            }
            else if (!string.IsNullOrEmpty(request.ServiceName))
            {
                query = query.Where(p => p.ServiceName == request.ServiceName);
            }

            var matchedPermissions = await query.Select(p => new { p.Id, p.Code }).ToListAsync(cancellationToken);
            if (matchedPermissions.Count == 0)
            {
                return new GetServiceStructurePermissionsResponse
                {
                    Success = true,
                    Items = [],
                    Message = $"{request.ServiceName} servisi uchun bazada ruxsatlar topilmadi."
                };
            }

            var matchedPermissionIds = matchedPermissions.Select(p => p.Id).ToList();
            var permissionIdToCode = matchedPermissions.ToDictionary(p => p.Id, p => p.Code);

            // 2. Ushbu ruxsatlar qaysi strukturalarga biriktirilganini olamiz
            var structurePermissions = await _dbContext.StructurePermissions
                .AsNoTracking()
                .Where(sp => !sp.IsDelete && matchedPermissionIds.Contains(sp.PermissionId))
                .Select(sp => new { sp.StructureId, sp.PermissionId })
                .ToListAsync(cancellationToken);

            // 3. Faqat so'ralgan servisga tegishli bo'lgan struktura va ruxsat kodlarini shakllantiramiz
            var dict = new Dictionary<long, List<int>>();
            foreach (var sp in structurePermissions)
            {
                if (!dict.TryGetValue(sp.StructureId, out var codes))
                {
                    codes = [];
                    dict[sp.StructureId] = codes;
                }

                if (permissionIdToCode.TryGetValue(sp.PermissionId, out var code) && !codes.Contains(code))
                {
                    codes.Add(code);
                }
            }

            var items = dict.Select(kv => new StructurePermissionItem
            {
                StructureId = kv.Key,
                PermissionCodes = kv.Value
            }).ToList();

            return new GetServiceStructurePermissionsResponse
            {
                Success = true,
                Items = items,
                Message = $"{items.Count} ta struktura uchun ruxsatlar muvaffaqiyatli qaytarildi."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ServiceName} servisining struktura ruxsatlarini gRPC orqali olishda xatolik", request.ServiceName);
            return new GetServiceStructurePermissionsResponse
            {
                Success = false,
                Items = [],
                Message = $"Xatolik: {ex.Message}"
            };
        }
    }
}
