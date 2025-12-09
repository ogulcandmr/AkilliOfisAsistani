using System;

namespace OfisAsistan.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public int EmployeeId { get; set; }
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastLoginDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public enum UserRole
    {
        Employee = 0,
        Manager = 1,
        Admin = 2
    }
}

