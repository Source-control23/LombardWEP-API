using System.Text.Json;

namespace LombardAPI_NEW.Extensions
{
    public static class JsonExtensions
    {
        public static string GetPropertyOrDefault(this JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var prop)
                ? prop.ToString()
                : string.Empty;
        }
    }

}
