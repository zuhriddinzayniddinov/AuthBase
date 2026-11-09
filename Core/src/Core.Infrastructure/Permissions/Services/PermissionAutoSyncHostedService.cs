using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Permissions.Services;

/// <summary>
/// Mikroservis yoki AuthApi ishga tushganida (Application Startup) fonda avtomatik ravishda
/// ruxsatlarni AuthApi ga yuboruvchi yoki to'g'ridan-to'g'ri bazaga saqlovchi orqa fon xizmati (<see cref="IHostedService"/>).
/// </summary>
public class PermissionAutoSyncHostedService : BackgroundService
{
    private readonly IPermissionSyncSender _sender;
    private readonly PermissionSyncMode _mode;
    private readonly ILogger<PermissionAutoSyncHostedService> _logger;

    /// <summary>
    /// <see cref="PermissionAutoSyncHostedService"/> konstruktori.
    /// </summary>
    /// <param name="sender">Ruxsatlarni yuboruvchi xizmat.</param>
    /// <param name="mode">Sinxronlashtirish usuli (RabbitMQ, gRPC yoki Direct).</param>
    /// <param name="logger">Tizim logeri.</param>
    public PermissionAutoSyncHostedService(
        IPermissionSyncSender sender,
        PermissionSyncMode mode,
        ILogger<PermissionAutoSyncHostedService> logger)
    {
        _sender = sender;
        _mode = mode;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Agar rejim Direct bo'lsa (AuthApi o'zining ichki enumlarini bazaga saqlashi uchun),
            // tarmoq ulanishini kutmasdan darhol saqlashni boshlaymiz
            if (_mode == PermissionSyncMode.Direct)
            {
                _logger.LogInformation("Lokal ruxsatlarni to'g'ridan-to'g'ri bazaga saqlash boshlandi (Direct Mode)...");
                await _sender.SyncDirectAsync(stoppingToken);
                _logger.LogInformation("Lokal ruxsatlarni bazaga saqlash muvaffaqiyatli yakunlandi.");
                return;
            }

            // Tashqi mikroservislar uchun RabbitMQ / tarmoq to'liq ulanib olishi uchun dastlab 2 soniya kutamiz
            await Task.Delay(2000, stoppingToken);

            if (_mode is PermissionSyncMode.RabbitMq or PermissionSyncMode.Both)
            {
                _logger.LogInformation("Avtomatik ruxsatlar sinxronizatsiyasi boshlandi (RabbitMQ)...");
                await _sender.SyncViaRabbitMqAsync(stoppingToken);
            }

            if (_mode is PermissionSyncMode.Grpc or PermissionSyncMode.Both)
            {
                _logger.LogInformation("Avtomatik ruxsatlar sinxronizatsiyasi boshlandi (gRPC)...");
                await _sender.SyncViaGrpcAsync(stoppingToken);
            }

            _logger.LogInformation("Avtomatik ruxsatlar sinxronizatsiyasi yakunlandi.");
        }
        catch (OperationCanceledException)
        {
            // Ilova to'xtatildi
        }
        catch (Exception ex)
        {
            // Xatolik ilovani to'xtatib qo'ymasligi kerak
            _logger.LogError(ex, "Avtomatik ruxsatlarni sinxronlashda xatolik yuz berdi");
        }
    }
}
