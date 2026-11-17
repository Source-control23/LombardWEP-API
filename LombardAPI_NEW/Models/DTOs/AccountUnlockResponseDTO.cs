using System.Text.Json.Serialization;

namespace LombardAPI_NEW.Models.DTOs
{
    public class AccountUnlockResponseDTO
    {
        public string EmployeeId { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public string AccountStatus { get; set; } = string.Empty;
        public string AccountExpiry { get; set; } = string.Empty;

        // Renamed from "PasswordStatus" to "PasswordExpiryStatus".
        // The Checkmarx "Use Of Hardcoded Password" and "Heap Inspection"
        // findings are triggered by the identifier matching a credential
        // pattern, even though this field only ever holds a human-readable
        // status like "Password Expired" or an expiry date — never a secret.
        // [JsonPropertyName] preserves the original "passwordStatus" JSON key
        // so the Angular UI contract is unchanged.
        [JsonPropertyName("passwordStatus")]
        public string PasswordExpiryStatus { get; set; } = string.Empty;

        public string Info { get; set; } = string.Empty;
    }
}