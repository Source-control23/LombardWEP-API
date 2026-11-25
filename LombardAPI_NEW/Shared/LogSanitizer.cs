using System.Text.RegularExpressions;

namespace LombardAPI_NEW.Shared
{
    /// <summary>
    /// Neutralises user-controlled values before they are written to logs.
    /// Prevents CWE-117 (Log Forging / Log Injection): an attacker cannot
    /// inject CR/LF to forge new log lines, nor smuggle control characters
    /// that corrupt log viewers.
    /// </summary>
    public static class LogSanitizer
    {
        /// <summary>
        /// Returns a single-line, control-character-free representation of
        /// <paramref name="value"/> that is safe to place in a log message.
        /// </summary>
        public static string Clean(string? value, int maxLength = 512)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Collapse CR / LF / TAB and any other control char into '_' (same output as before),
            // using Regex.Replace so static analysers recognise the CWE-117 sanitisation.
            string cleaned = Regex.Replace(value, @"[\r\n\t\p{Cc}]", "_");

            return cleaned.Length >= maxLength
                ? cleaned.Substring(0, maxLength) + "..."
                : cleaned;
        }

        /// <summary>Convenience overload for any object (uses ToString()).</summary>
        public static string Clean(object? value, int maxLength = 512)
            => Clean(value?.ToString(), maxLength);
    }
}