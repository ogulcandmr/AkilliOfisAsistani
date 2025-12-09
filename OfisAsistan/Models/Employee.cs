using System;
using System.Collections.Generic;

namespace OfisAsistan.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public int DepartmentId { get; set; }
        public string Position { get; set; }
        public string Skills { get; set; } // JSON formatında yetenek listesi
        public int CurrentWorkload { get; set; } // Mevcut iş yükü (saat cinsinden)
        public int MaxWorkload { get; set; } // Maksimum iş yükü kapasitesi
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        
        // Navigation properties
        public Department Department { get; set; }
        public List<Task> Tasks { get; set; }
        
        public string FullName => $"{FirstName} {LastName}";
        
        public double WorkloadPercentage => MaxWorkload > 0 ? (double)CurrentWorkload / MaxWorkload * 100 : 0;
    }
}

