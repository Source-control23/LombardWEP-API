using System.Net.Mail;
using System.Security.Claims;

namespace LombardAPI_NEW.Shared
{
    /// <summary>
    /// The signed-in user, taken ONLY from a validated Microsoft Entra ID access token
    /// (never from request body / query values, which a client can tamper with).
    /// </summary>
    public sealed class CallerIdentity
    {
        // Raw (unmapped) claim names; ClaimTypes.* are included in case inbound mapping is on.
        private static readonly string[] SamClaims = { "onprem_sam_account_name", "samAccountName", "samaccountname" };
        private static readonly string[] AddressClaims = { "preferred_username", "upn", "unique_name", "email", ClaimTypes.Upn, ClaimTypes.Email, ClaimTypes.Name };
        private static readonly string[] NameClaims = { "name", ClaimTypes.GivenName };

        /// <summary>Email-style identifiers of the caller (UPN / preferred_username / email), lower-case.</summary>
        public IReadOnlyCollection<string> Emails { get; }

        /// <summary>Employee / NT IDs of the caller (sAMAccountName and local parts of the addresses), lower-case.</summary>
        public IReadOnlyCollection<string> Ids { get; }

        /// <summary>Main employee ID, same precedence as the frontend: sAMAccountName, then UPN local part.</summary>
        public string? PrimaryId { get; }

        public string DisplayName { get; }

        private CallerIdentity(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> ids, string? primaryId, string displayName)
        {
            Emails = emails;
            Ids = ids;
            PrimaryId = primaryId;
            DisplayName = displayName;
        }

        /// <summary>Returns null when the principal is not authenticated or carries no usable identifier.</summary>
        public static CallerIdentity? FromPrincipal(ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var orderedIds = new List<string>();

            void AddId(string? raw)
            {
                var id = NormaliseId(raw);
                if (id != null && ids.Add(id)) orderedIds.Add(id);
            }

            foreach (var type in SamClaims)
                foreach (var claim in principal.FindAll(type))
                    AddId(claim.Value);

            foreach (var type in AddressClaims)
            {
                foreach (var claim in principal.FindAll(type))
                {
                    var value = claim.Value?.Trim() ?? string.Empty;
                    if (value.Contains('@'))
                    {
                        var email = NormaliseEmail(value);
                        if (email != null) emails.Add(email);
                    }
                    AddId(value);   // "DOMAIN\user" or "user@domain" → "user"
                }
            }

            if (emails.Count == 0 && ids.Count == 0)
                return null;

            string displayName = NameClaims
                .Select(t => principal.FindFirst(t)?.Value?.Trim())
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                ?? orderedIds.FirstOrDefault()
                ?? emails.First();

            return new CallerIdentity(emails, ids, orderedIds.FirstOrDefault(), displayName);
        }

        /// <summary>
        /// True when the caller is one of the approvers. Matches on full address, or on employee ID
        /// against the approver address local part (e.g. ILOM35124@icicilombard.com ↔ UPN ilom35124@...).
        /// The token issuer is restricted to the organisation's tenant, so IDs are unique within it.
        /// </summary>
        public bool MatchesAny(IEnumerable<string> approverEmails)
        {
            foreach (var approver in approverEmails)
            {
                if (string.IsNullOrWhiteSpace(approver))
                    continue;

                var candidate = approver.Trim();
                if (Emails.Contains(candidate))
                    return true;

                int at = candidate.IndexOf('@');
                if (at > 0 && Ids.Contains(candidate[..at]))
                    return true;
            }
            return false;
        }

        private static string? NormaliseId(string? raw)
        {
            var value = raw?.Trim() ?? string.Empty;

            int slash = value.LastIndexOf('\\');
            if (slash >= 0) value = value[(slash + 1)..];

            int at = value.IndexOf('@');
            if (at >= 0) value = value[..at];

            return EmailRecipientResolver.IsValidEmployeeId(value) ? value.ToLowerInvariant() : null;
        }

        private static string? NormaliseEmail(string raw)
        {
            try
            {
                if (raw.Any(char.IsControl) || raw.Length > 254) return null;
                var address = new MailAddress(raw);
                return string.Equals(address.Address, raw, StringComparison.OrdinalIgnoreCase)
                    ? address.Address.ToLowerInvariant()
                    : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
