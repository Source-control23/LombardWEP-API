using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;

namespace LombardAPI_NEW.Services
{
    public class GALService : IGAL
    {
        public async Task<GALSearchResponseDTO> SearchAsync(GALSearchEnum parameter, string value)
        {

            switch (parameter)
            {
                // case GroupMemberSearchParameter.EmployeeId:
            }
            var res = new GALSearchResponseDTO()
            {
                // Basic Information
                FirstName = "Ayush",
                LastName = "Kamble",
                Grade = "G5",
                Initials = "AK",
                Display = "Ayush Kamble",
                KnownAs = "Ayush",
                WorkStationID = "WS-1022",

                // Company & Employment Info
                Company = "TechNova Pvt Ltd",
                Designation = "Software Engineer",
                EmpNo = "EMP12345",
                Group = "IT",
                Location = "Pune",
                Department = "Software Development",
                Manager = "Rohan Patil",
                DirectReports = "None",
                MainGroup = "Engineering",
                CostCenter = "CC-4502",
                Region = "West",

                // Contact Information
                BoardNo = "+91-20-123456",
                DirectPhone = "+91-20-987654",
                EmailAddress = "ayush.kamble@technova.com",
                Mobile = "+91-9876543210",
                Business = "+91-20-554433",
                CountryCode = "+91",
                Business2 = "+91-20-443322",
                CityCode = "020",
                Fax = "+91-20-442211",
                Home = "+91-712-665544",
                Assisted = "N/A",

                // Address Info
                AddressLocation = "TechNova Campus",
                Address = "Tower 2, Magarpatta Cybercity",
                State = "Maharashtra",
                ZipCode = "411028",
                Country = "India"
            };

            return res;
        }
    }
}