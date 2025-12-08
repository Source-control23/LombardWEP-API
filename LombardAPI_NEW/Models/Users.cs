using System.ComponentModel.DataAnnotations;

namespace LombardAPI_NEW.Models
{
    public class Users
    {
        [Key]
        public int Id { get; set; }
        public int? RollID { get; set; }

        public int? Deptid { get; set; }

        public string? FullName { get; set; }

        public string? Gender { get; set; }

        public string? Address { get; set; }

        public string? Line1 { get; set; }

        public string? Line2 { get; set; }


        public string? City { get; set; }
        public string? State { get; set; }


        public string? Country { get; set; }

        [RegularExpression(@"^[a-zA-Z0-9._-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email address")]
        public string? Email { get; set; }

        public string? Website { get; set; }

        public string? Twitter { get; set; }

        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Invalid mobile number")]
        public string? MobileNo { get; set; }
        public string? MaritalStatus { get; set; }


        public string? UserName { get; set; }

        public int? AccessFailedCount { get; set; }

        public string? Zipcode { get; set; }
        public int? Active { get; set; }
        public bool? UserActive { get; set; }
        public string? department { get; set; }

        public string? designation { get; set; }
        public string? office { get; set; }

        public string? location { get; set; }

        public int? user_type { get; set; }

        public string? UserToken { get; set; }

        public DateTime? CreatedOn { get; set; } = DateTime.Now;

    }
}

