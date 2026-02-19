using LombardAPI_NEW.Interfaces;
using LombardAPI_NEW.Models.DTOs;
using LombardAPI_NEW.Models.Enums;
using LombardAPI_NEW.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace LombardAPI_NEW.Controllers
{



    [Route("api/[controller]")]
    [ApiController]
    public class GALController : ControllerBase
    {

        private readonly IGAL _gALService;

        public GALController(IGAL gALService)
        {
            _gALService = gALService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
       [FromQuery] GALSearchEnum parameter,
       [FromQuery] string value,
       [FromQuery] string debug = null)   // <-- debug switch
        {
            if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(debug))
                return BadRequest("Search value cannot be empty.");

            // =========================
            // DEBUG MODE (Mock Responses)
            // =========================
            //debug = "page";
            if (!string.IsNullOrWhiteSpace(debug))
            {
                var mockData = GetMockUsers(debug.ToLower());

                if (!mockData.Any())
                    return Ok(new List<GALSearchResponseDTO>()); // always return list

                return Ok(mockData);
            }

            // =========================
            // REAL SERVICE CALL
            // =========================
            var result = await _gALService.SearchAsync(parameter, value);

            if (result == null)
                return Ok(new List<GALSearchResponseDTO>()); // return empty list instead of 404

            // normalize to list (fix Angular iterable issue)
            var normalized = result is IEnumerable<GALSearchResponseDTO> list
                ? list.ToList()
                : new List<GALSearchResponseDTO> { (GALSearchResponseDTO)result };

            return Ok(normalized);
        }

        private List<GALSearchResponseDTO> GetMockUsers(string caseType)
        {
            return caseType switch
            {
                "single" => new List<GALSearchResponseDTO>
        {
            new GALSearchResponseDTO
            {
                FirstName = "Ayush",
                LastName = "Kamble",
                Display = "Ayush Kamble",
                EmpNo = "EMP10234",
                EmailAddress = "ayush.kamble@company.com",
                Mobile = "+919876543210",
                Department = "Technology",
                Designation = "Software Engineer",
                Manager = "Rahul Mehta",
                Location = "Gurugram",
                Company = "ICICI Lombard",
                Country = "India"
            }
        },

                "multiple" => new List<GALSearchResponseDTO>
        {
            new GALSearchResponseDTO
            {
                FirstName = "Rohit",
                LastName = "Sharma",
                Display = "Rohit Sharma",
                EmpNo = "EMP20011",
                EmailAddress = "rohit.sharma@company.com",
                Mobile = "+919812345670",
                Department = "Claims",
                Designation = "Senior Analyst",
                Manager = "Neha Verma",
                Location = "Mumbai",
                Company = "ICICI Lombard",
                Country = "India"
            },
            new GALSearchResponseDTO
            {
                FirstName = "Rohit",
                LastName = "Sharma",
                Display = "Rohit Sharma",
                EmpNo = "EMP20045",
                EmailAddress = "rohit.sharma2@company.com",
                Mobile = "+919876500001",
                Department = "Underwriting",
                Designation = "Associate",
                Manager = "Amit Jain",
                Location = "Gurugram",
                Company = "ICICI Lombard",
                Country = "India"
            }
        },

                "partial" => new List<GALSearchResponseDTO>
        {
            new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            }
        },
                "page" => new List<GALSearchResponseDTO>
        {
            new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },
              new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },  new GALSearchResponseDTO
            {
                FirstName = "Sneha",
                LastName = "Patil",
                Display = "Sneha Patil",
                EmpNo = "EMP30987",
                EmailAddress = "sneha.patil@company.com",
                Mobile = null,
                Department = "HR",
                Designation = null,
                Manager = "Vikas Singh",
                Location = "Pune",
                Company = "ICICI Lombard",
                Country = "India"
            },
        },

                "empty" => new List<GALSearchResponseDTO>(),

                _ => new List<GALSearchResponseDTO>()
            };
        }

    }
}
