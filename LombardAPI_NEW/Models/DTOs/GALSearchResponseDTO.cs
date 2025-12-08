namespace LombardAPI_NEW.Models.DTOs
{
    public class GALSearchResponseDTO
    {
        // Basic Information
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Grade { get; set; }
        public string? Initials { get; set; }
        public string? Display { get; set; }
        public string? KnownAs { get; set; }
        public string? WorkStationID { get; set; }

        // Company & Employment Info
        public string? Company { get; set; }
        public string? Designation { get; set; }
        public string? EmpNo { get; set; }
        public string? Group { get; set; }
        public string? Location { get; set; }
        public string? Department { get; set; }
        public string? Manager { get; set; }
        public string? DirectReports { get; set; }
        public string? MainGroup { get; set; }
        public string? CostCenter { get; set; }
        public string? Region { get; set; }

        // Contact Information
        public string? BoardNo { get; set; }
        public string? DirectPhone { get; set; }
        public string? EmailAddress { get; set; }
        public string? Mobile { get; set; }
        public string? Business { get; set; }
        public string? CountryCode { get; set; }
        public string? Business2 { get; set; }
        public string? CityCode { get; set; }
        public string? Fax { get; set; }
        public string? Home { get; set; }
        public string? Assisted { get; set; }

        // Address Info
        public string? AddressLocation { get; set; }  // Renamed to avoid conflict with 'Location'
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
    }
}
