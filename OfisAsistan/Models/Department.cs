using System;
using System.Collections.Generic;

namespace OfisAsistan.Models
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ManagerId { get; set; }
        public DateTime CreatedDate { get; set; }
        
        // Navigation properties
        public List<Employee> Employees { get; set; }
        public List<Task> Tasks { get; set; }
    }
}

