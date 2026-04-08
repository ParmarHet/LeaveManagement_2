using System;
using System.ComponentModel.DataAnnotations;

namespace LMS.Models
{
    public class UserProfileViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string Address { get; set; } = string.Empty;
        
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime DateJoined { get; set; }
        
        public string? DepartmentName { get; set; }
        public string? ManagerName { get; set; }

        // Added to match properties used in the Employee/Profile view
        public UserStatus Status { get; set; } = UserStatus.Pending;
        public Department? Department { get; set; }
        public ApplicationUser? Manager { get; set; }
    }
}
