using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace OfisAsistan.Models
{
    public class Employee
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("department_id")]
        public int DepartmentId { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; }

        [JsonProperty("skills")]
        public string Skills { get; set; } // JSON formatında yetenek listesi

        [JsonProperty("current_workload")]
        public int CurrentWorkload { get; set; } // Mevcut iş yükü (saat cinsinden)

        [JsonProperty("max_workload")]
        public int MaxWorkload { get; set; } // Maksimum iş yükü kapasitesi

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }

        [JsonProperty("created_date")]
        public DateTime CreatedDate { get; set; }
        
        // Navigation properties
        public Department Department { get; set; }
        public List<Task> Tasks { get; set; }
        
        public string FullName => $"{FirstName} {LastName}";
        
        public double WorkloadPercentage => MaxWorkload > 0 ? (double)CurrentWorkload / MaxWorkload * 100 : 0;
    }
}

