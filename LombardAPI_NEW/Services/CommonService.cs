namespace LombardAPI_NEW.Services
{
    public class CommonService
    {
        private readonly string _emailBaseUrl;

        public CommonService(IConfiguration config)
        {
            _emailBaseUrl = config["EmailSettings:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "Configuration 'EmailSettings:BaseUrl' is missing.");
        }

        public string GetEmailBaseUrl() => _emailBaseUrl;
    }
}
