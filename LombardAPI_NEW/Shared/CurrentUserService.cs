using LombardAPI_NEW.ErrorHandling;

namespace LombardAPI_NEW.Shared
{
    /// <summary>Resolves the logged-in user for the current HTTP request.</summary>
    public class CurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CurrentUserService> _logger;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, ILogger<CurrentUserService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>Caller from the validated Entra ID token, or null when the request is anonymous.</summary>
        public CallerIdentity? GetCaller() => CallerIdentity.FromPrincipal(_httpContextAccessor.HttpContext?.User);

        /// <summary>
        /// Requestor = currently logged-in user. When the request carries a validated token, that
        /// identity wins over the body values; otherwise the frontend values are used after strict
        /// validation (kept so existing clients without a token continue to work).
        /// Returns the validated (id, name).
        /// </summary>
        public (string Id, string Name) ResolveRequestor(string? requestorId, string? requestorName)
        {
            var caller = GetCaller();

            if (caller?.PrimaryId != null)
            {
                if (!string.IsNullOrWhiteSpace(requestorId) &&
                    !string.Equals(requestorId.Trim(), caller.PrimaryId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Submitted requestor differs from authenticated user. Authenticated identity will be used.");
                }

                requestorId = caller.PrimaryId;
                if (string.IsNullOrWhiteSpace(requestorName))
                    requestorName = caller.DisplayName;
            }

            var id = requestorId?.Trim();
            if (!EmailRecipientResolver.IsValidEmployeeId(id))
                throw new ServiceException("Requestor ID is missing or invalid.");

            var name = requestorName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = id;

            if (name!.Length > 256 || name.Any(char.IsControl))
                throw new ServiceException("Requestor name is invalid.");

            return (id!, name);
        }
    }
}
