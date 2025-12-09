using System;
using System.ComponentModel;
using Newtonsoft.Json;

namespace OfisAsistan.Models
{
    public class Task
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("assigned_to_id")]
        public int AssignedToId { get; set; }

        [JsonProperty("created_by_id")]
        public int CreatedById { get; set; }

        [JsonProperty("created_date")]
        public DateTime CreatedDate { get; set; }

        [JsonProperty("due_date")]
        public DateTime? DueDate { get; set; }

        [JsonProperty("status")]
        public TaskStatus Status { get; set; }

        [JsonProperty("priority")]
        public TaskPriority Priority { get; set; }

        [JsonProperty("department_id")]
        public int DepartmentId { get; set; }

        [JsonProperty("skills_required")]
        public string SkillsRequired { get; set; } // JSON formatında yetenek listesi

        [JsonProperty("estimated_hours")]
        public int EstimatedHours { get; set; }

        [JsonProperty("actual_hours")]
        public int ActualHours { get; set; }

        [JsonProperty("completed_date")]
        public DateTime? CompletedDate { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        [JsonProperty("is_anomaly")]
        public bool IsAnomaly { get; set; } // Anomali tespiti için

        [JsonProperty("anomaly_reason")]
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

