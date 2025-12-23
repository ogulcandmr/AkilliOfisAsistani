using System;
using System.Collections.Generic;

namespace OfisAsistan.Models
{
    // Görev Parçalama Modeli
    public class SubTask
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int EstimatedHours { get; set; }
        public int Order { get; set; }
    }

    // Personel Öneri Modeli
    public class EmployeeRecommendation
    {
        public Employee RecommendedEmployee { get; set; }
        public double Score { get; set; }
        public string Reason { get; set; }
        public List<Employee> AlternativeEmployees { get; set; }
    }

    // Anomali Tespit Modeli
    public class AnomalyDetection
    {
        public Task Task { get; set; } // OfisAsistan.Models.Task
        public AnomalyType Type { get; set; }
        public AnomalySeverity Severity { get; set; }
        public string Message { get; set; }
    }

    public enum AnomalyType
    {
        Overdue,
        WorkloadOverload,
        StuckTask,
        QualityIssue
    }

    public enum AnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}