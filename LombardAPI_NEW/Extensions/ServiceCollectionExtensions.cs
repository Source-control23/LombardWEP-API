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
            IConfiguration configuration)
        {
            services.AddScoped<IAccountUnlock, AccountUnlockService>();
            services.AddScoped<IGroupMembership, GroupMembershipService>();
            services.AddScoped<IRightsManagement, RightsManagementService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IApproval, ApprovalService>();
            services.AddScoped<IGAL, GALService>();
            services.AddScoped<PowershellCommandService>();
            services.AddScoped<CommonService>();

            // ✅ Correct way
            services.Configure<PowerShellCommandSettings>(
                configuration.GetSection("PowerShellCommands"));

            return services;
        }
    }
}
