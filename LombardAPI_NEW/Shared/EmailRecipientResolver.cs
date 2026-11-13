using System.Net.Mail;
using System.Text.RegularExpressions;

namespace LombardAPI_NEW.Shared
{
    /// <summary>
    /// Builds and validates notification recipients dynamically from request / AD data.
    /// No user-specific addresses are stored here; the mail domain comes from configuration
    /// (EmailSettings:EmailDomain).
    /// </summary>
    public class EmailRecipientResolver
    {
        // Employee / NT IDs: letters, digits, dot, underscore, hyphen. Anything else
        // (quotes, spaces, CR/LF, '@', ';', '$', '`') is rejected so the value can be safely
        // used both in an email address and in PowerShell command templates.
        private static readonly Regex EmployeeIdPattern =
            new(@"^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

        // Owner strings look like: "AKASH GOWDA /EXT/IT/ICICILOMBARD/BLR (ILOM35124)"
        // or, for Cloud, "Display Name (owner@domain)". Capture the LAST (...) group.
        private static readonly Regex TrailingParenthesesPattern =
            new(@"\(([^()]*)\)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

        private readonly string _emailDomain;

        public EmailRecipientResolver(IConfiguration config)
        {
            var domain = config["EmailSettings:EmailDomain"]?.Trim().TrimStart('@');

            if (string.IsNullOrWhiteSpace(domain) || domain.Any(c => char.IsWhiteSpace(c) || c == '@'))
                throw new InvalidOperationException("Configuration 'EmailSettings:EmailDomain' is missing or invalid.");

            _emailDomain = domain.ToLowerInvariant();
        }

        public static bool IsValidEmployeeId(string? value) =>
            !string.IsNullOrWhiteSpace(value) && EmployeeIdPattern.IsMatch(value.Trim());

        /// <summary>Splits a comma separated list of employee IDs, trimming and de-duplicating (case-insensitive).</summary>
        public static List<string> SplitEmployeeIds(string? empIds) =>
            (empIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Builds "{employeeId}@{configured domain}" after validating the ID. Returns null when invalid.</summary>
        public string? BuildEmailFromEmployeeId(string? employeeId)
        {
            if (!IsValidEmployeeId(employeeId))
                return null;

            return NormalizeAllowedEmail($"{employeeId!.Trim()}@{_emailDomain}");
        }

        /// <summary>
        /// Resolves the DL owner email address(es) from the owner string.
        /// "NAME (ILOM35124)"          → ILOM35124@{domain}
        /// "NAME (owner@icicilombard.com)" → owner@icicilombard.com (only if it belongs to the configured domain)
        /// Multiple owners separated by commas inside the brackets are supported.
        /// </summary>
        public List<string> ResolveOwnerEmails(string? dlOwner)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(dlOwner))
                return result;

            var match = TrailingParenthesesPattern.Match(dlOwner);
            if (!match.Success)
                return result;

            foreach (var token in match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string? email = token.Contains('@')
                    ? NormalizeAllowedEmail(token)
                    : BuildEmailFromEmployeeId(token);

                if (email != null && !result.Contains(email, StringComparer.OrdinalIgnoreCase))
                    result.Add(email);
            }

            return result;
        }

        /// <summary>Owner display name without the trailing "(ID)" part.</summary>
        public static string GetOwnerDisplayName(string? dlOwner)
        {
            if (string.IsNullOrWhiteSpace(dlOwner))
                return string.Empty;

            return TrailingParenthesesPattern.Replace(dlOwner, string.Empty).Trim();
        }

        /// <summary>First word of each owner's display name: "AKASH GOWDA /EXT/..." → "AKASH".</summary>
        public static string GetOwnerFirstNames(string? dlOwner)
        {
            var names = GetOwnerDisplayName(dlOwner)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => n.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(n => !string.IsNullOrWhiteSpace(n));

            return string.Join(", ", names);
        }

        /// <summary>
        /// Validates the address syntax and restricts it to the configured domain (or its sub-domains,
        /// e.g. ext.icicilombard.com) so a tampered request cannot redirect mail to an external address.
        /// </summary>
        private string? NormalizeAllowedEmail(string candidate)
        {
            try
            {
                if (candidate.Any(char.IsControl) || candidate.Length > 254)
                    return null;

                var address = new MailAddress(candidate.Trim());

                // Reject "Display Name <x@y>" forms – only a bare address is accepted.
                if (!string.Equals(address.Address, candidate.Trim(), StringComparison.OrdinalIgnoreCase))
                    return null;

                var host = address.Host.ToLowerInvariant();
                bool allowed = host == _emailDomain || host.EndsWith("." + _emailDomain, StringComparison.Ordinal);

                return allowed ? address.Address : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
