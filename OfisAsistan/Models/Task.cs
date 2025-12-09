using System;
using System.ComponentModel;

namespace OfisAsistan.Models
{
    public class Task
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int AssignedToId { get; set; }
        public int CreatedById { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority Priority { get; set; }
        public int DepartmentId { get; set; }
        public string SkillsRequired { get; set; } // JSON formatında yetenek listesi
        public int EstimatedHours { get; set; }
        public int ActualHours { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Notes { get; set; }
        public bool IsAnomaly { get; set; } // Anomali tespiti için
        public string AnomalyReason { get; set; }
    }

    public enum TaskStatus
    {
        [Description("Bekliyor")]
        Pending = 0,
        [Description("Yapılıyor")]
        InProgress = 1,
        [Description("Tamamlandı")]
        Completed = 2,
        [Description("İptal")]
        Cancelled = 3
    }

    public enum TaskPriority
    {
        [Description("Düşük")]
        Low = 0,
        [Description("Normal")]
        Normal = 1,
        [Description("Yüksek")]
        High = 2,
        [Description("Kritik")]
        Critical = 3
    }
}

