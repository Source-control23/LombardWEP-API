using System.Text;

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

            var sb = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                // Collapse CR / LF / TAB and any other control char into '_'
                if (c == '\r' || c == '\n' || c == '\t' || char.IsControl(c))
                    sb.Append('_');
                else
                    sb.Append(c);

                if (sb.Length >= maxLength)
                {
                    sb.Append("...");
                    break;
                }
            }

            return sb.ToString();
        }

        /// <summary>Convenience overload for any object (uses ToString()).</summary>
        public static string Clean(object? value, int maxLength = 512)
            => Clean(value?.ToString(), maxLength);
    }
}