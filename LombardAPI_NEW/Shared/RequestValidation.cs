using LombardAPI_NEW.ErrorHandling;
using System.Text.RegularExpressions;

namespace LombardAPI_NEW.Shared
{
    /// <summary>
    /// Input validation shared by the DL Membership and DL Rights request flows.
    /// Values validated here end up in PowerShell command templates, DB rows and emails.
    /// </summary>
    public static class RequestValidation
    {
        // Characters that would break out of / expand inside the double-quoted PowerShell templates
        // (straight and typographic double quotes, backtick, dollar).
        private static readonly char[] UnsafeDLNameChars = { '"', '“', '”', '„', '`', '$' };

        /// <summary>CWE-117: strips CR/LF/TAB/control characters before a value is written to a log.</summary>
        public static string ForLog(string? value) =>
            Regex.Replace(value ?? string.Empty, @"[\r\n\t\p{Cc}]", string.Empty, RegexOptions.None, TimeSpan.FromMilliseconds(200));

        public static string NormaliseDLName(string? dlName)
        {
            var value = dlName?.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
                throw new ServiceException("DL name is required.");

            if (value.IndexOfAny(UnsafeDLNameChars) >= 0 || value.Any(char.IsControl))
                throw new ServiceException("DL name contains invalid characters.");

            return value;
        }

        /// <summary>Downstream code compares case-sensitively ("Add" adds, anything else removes).</summary>
        public static string NormaliseAction(string? action)
        {
            var value = action?.Trim();
            if (string.Equals(value, "Add", StringComparison.OrdinalIgnoreCase)) return "Add";
            if (string.Equals(value, "Remove", StringComparison.OrdinalIgnoreCase)) return "Remove";
            throw new ServiceException("Action must be either 'Add' or 'Remove'.");
        }

        public static string NormaliseSource(string? source)
        {
            var value = source?.Trim();
            if (string.Equals(value, "OnPrem", StringComparison.OrdinalIgnoreCase)) return "OnPrem";
            if (string.Equals(value, "Cloud", StringComparison.OrdinalIgnoreCase)) return "Cloud";
            throw new ServiceException("Invalid source value. Allowed: OnPrem, Cloud.");
        }

        /// <summary>Validates a comma separated ID list and returns it trimmed, de-duplicated and comma-joined.</summary>
        public static string NormaliseEmployeeIds(string? empIds)
        {
            var ids = EmailRecipientResolver.SplitEmployeeIds(empIds);
            if (ids.Count == 0)
                throw new ServiceException("At least one Employee/Consultant ID is required.");

            if (!ids.All(EmailRecipientResolver.IsValidEmployeeId))
                throw new ServiceException("One or more Employee/Consultant IDs are invalid. Only letters, digits, '.', '_' and '-' are allowed.");

            return string.Join(",", ids);
        }
    }
}
