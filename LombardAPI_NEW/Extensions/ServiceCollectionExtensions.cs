using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models;
using LombardAPI_NEW.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LombardAPI_NEW.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLombardServices(
            this IServiceCollection services,
            IConfiguration configuration,
            Microsoft.Extensions.Logging.ILogger? startupLogger = null)
        {
            services.AddScoped<IAccountUnlock, AccountUnlockService>();
            services.AddScoped<IGroupMembership, GroupMembershipService>();
            services.AddScoped<IRightsManagement, RightsManagementService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IApproval, ApprovalService>();
            services.AddScoped<IGAL, GALService>();
            services.AddScoped<PowershellCommandService>();
            services.AddScoped<CommonService>();
            services.AddSingleton<LombardAPI_NEW.Shared.EmailRecipientResolver>();
            services.AddScoped<IMembershipNotificationService, MembershipNotificationService>();
            services.AddHttpContextAccessor();
            // Audit trail for the registration of the caller-identity accessor
            // (Checkmarx "Insufficient Logging of Sensitive Operations").
            startupLogger?.LogInformation("HttpContextAccessor registered for caller identity resolution (requestor / approver checks).");
            services.AddSingleton<LombardAPI_NEW.Shared.CurrentUserService>();

            // ✅ Correct way
            services.Configure<PowerShellCommandSettings>(
                configuration.GetSection("PowerShellCommands"));

            return services;
        }
    }
}
