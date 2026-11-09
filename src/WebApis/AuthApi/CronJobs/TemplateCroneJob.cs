using AuthService.Services;
using Core.Web.CronSchedulers;
using Serilog;

namespace AuthApi.CronJobs;

public class TemplateCroneJob(IServiceProvider serviceProvider) : ICronJob
{
    public Task Run(CancellationToken token = default)
    {
        Log.Information("Start template cron");
        using var scope = serviceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        Log.Information("Finish template cron");
        return Task.CompletedTask;
    }
}
