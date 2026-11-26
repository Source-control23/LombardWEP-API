using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;

namespace LombardAPI_NEW.Shared
{
    /// <summary>
    /// Named authorization policies. Every controller action declares one of these explicitly
    /// ([Authorize(Policy = ...)]) so function-level authorization is visible on each endpoint
    /// (Checkmarx "Missing Function Level Authorization", OWASP API5).
    /// </summary>
    public static class AuthPolicies
    {
        /// <summary>
        /// Any signed-in user with a valid Microsoft Entra ID token for this API.
        /// Used by GAL Search and the Approval page (DL owners are normal users; the
        /// owner-only check is enforced per request in ApprovalService).
        /// </summary>
        public const string SignedInUser = "SignedInUser";

        /// <summary>
        /// Signed-in user who also holds the "Feature.Access" Entra app role.
        /// Used by Account Unlock, Group Membership and Rights Management.
        /// </summary>
        public const string FeatureAccess = "FeatureAccess";

        /// <summary>The Entra app role value required by <see cref="FeatureAccess"/>.</summary>
        public const string FeatureAccessRole = "Feature.Access";

        /// <summary>
        /// Registers both policies and the audited role handler. Each registration step is
        /// written to the startup log (Checkmarx "Insufficient Logging of Sensitive Operations").
        /// </summary>
        public static IServiceCollection AddLombardAuthorization(
            this IServiceCollection services,
            ILogger? startupLogger = null)
        {
            services.AddSingleton<IAuthorizationHandler, FeatureAccessHandler>();
            startupLogger?.LogInformation("Authorization handler registered: FeatureAccessHandler (audits Feature.Access decisions).");

            services.AddAuthorization(options =>
            {
                options.AddPolicy(SignedInUser, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    startupLogger?.LogInformation("Authorization policy '{Policy}' configured: requires an authenticated user.", SignedInUser);
                });

                options.AddPolicy(FeatureAccess, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new FeatureAccessRequirement());
                    startupLogger?.LogInformation("Authorization policy '{Policy}' configured: requires an authenticated user with the '{Role}' app role.", FeatureAccess, FeatureAccessRole);
                });

                startupLogger?.LogInformation("Authorization options configured with policies '{SignedIn}' and '{Feature}'.", SignedInUser, FeatureAccess);
            });
            startupLogger?.LogInformation("Authorization services registered (AddAuthorization).");

            return services;
        }
    }

    /// <summary>Requirement: the caller's token carries the Feature.Access app role.</summary>
    public sealed class FeatureAccessRequirement : IAuthorizationRequirement
    {
    }

    /// <summary>
    /// Evaluates <see cref="FeatureAccessRequirement"/> with the same claim check the controllers
    /// used before (raw "roles" / "role" claims, immune to RoleClaimType mapping) and writes an
    /// audit log entry for every denied attempt (Insufficient Logging of Sensitive Operations).
    /// A failed check returns 403 exactly as the previous in-action Forbid() did.
    /// </summary>
    public sealed class FeatureAccessHandler : AuthorizationHandler<FeatureAccessRequirement>
    {
        private readonly ILogger<FeatureAccessHandler> _logger;

        public FeatureAccessHandler(ILogger<FeatureAccessHandler> logger)
        {
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureAccessRequirement requirement)
        {
            bool hasRole = context.User.Claims.Any(c =>
                (c.Type == "roles" || c.Type == "role" ||
                 c.Type.EndsWith("/role") || c.Type.EndsWith("/roles"))
                && c.Value == AuthPolicies.FeatureAccessRole);

            string path = context.Resource is HttpContext http ? http.Request.Path.Value ?? string.Empty : string.Empty;
            string caller = context.User.FindFirst("preferred_username")?.Value
                            ?? context.User.FindFirst("upn")?.Value
                            ?? context.User.FindFirst("oid")?.Value
                            ?? "unknown";

            // CWE-117: strip CR/LF/control characters before the values reach the log.
            path = Regex.Replace(path, @"[\r\n\t\p{Cc}]", string.Empty);
            caller = Regex.Replace(caller, @"[\r\n\t\p{Cc}]", string.Empty);

            if (hasRole)
            {
                _logger.LogDebug("Feature.Access granted. Caller: {Caller}, Path: {Path}", caller, path);
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("Feature.Access denied (403). Caller: {Caller}, Path: {Path}", caller, path);
            }

            return Task.CompletedTask;
        }
    }
}
