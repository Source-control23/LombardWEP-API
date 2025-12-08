namespace LombardAPI_NEW.Models
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }

        public static ApiResponse Ok(object data) =>
            new() { Success = true, Data = data };

        public static ApiResponse Fail(string message) =>
            new() { Success = false, Message = message };
    }

}
