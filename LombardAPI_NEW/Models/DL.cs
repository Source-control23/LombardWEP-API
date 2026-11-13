using System.ComponentModel.DataAnnotations;

namespace LombardAPI_NEW.Models
{
    public class DL
    {
        [Key]
        public int DLID { get; set; }

       public string ApprovalID {get;set;} = string.Empty;

        public string Event { get; set; } = string.Empty;   
        public string DLName { get; set; } = string.Empty;  
        public string DLOWner { get; set; } = string.Empty; 
        public string Action { get; set; } = string.Empty;  

        public string EmpIds { get; set; } = string.Empty;  
        public string Rights { get; set; } = string.Empty;  

        public bool IsLocked { get; set; }                  

        public DateTime CreatedTime { get; set; }         

        public string ApprovalStatus { get; set; } = "Pending"; 
        public string? ApproverEmail { get; set; }          
        public DateTime? ApprovalTime { get; set; }

        // Requestor (logged-in user who raised the request). Nullable so that
        // rows created before this column existed remain valid.
        public string? RequestorId { get; set; }
        public string? RequestorName { get; set; }
    }

}
