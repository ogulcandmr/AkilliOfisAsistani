using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TaskModel = OfisAsistan.Models.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using Newtonsoft.Json;
using OfisAsistan.Models;

namespace OfisAsistan.Services
{
    public class AIService
    {
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly HttpClient _httpClient;
        private readonly DatabaseService _databaseService;

        public AIService(string apiKey, string apiUrl, DatabaseService databaseService)
        {
            _apiKey = apiKey;
            _apiUrl = apiUrl; // OpenAI veya Gemini API URL
            _httpClient = new HttpClient();
            _databaseService = databaseService;
        }

        // Sesli komuttan görev oluşturma
        public async System.Threading.Tasks.Task<TaskModel> ParseVoiceCommandToTaskAsync(string voiceCommand)
        {
            try
            {
                var prompt = $@"Aşağıdaki sesli komutu analiz et ve JSON formatında görev bilgilerini çıkar:
Komut: {voiceCommand}

Çıkarılacak bilgiler:
- title: Görev başlığı
- description: Görev açıklaması
- assignedToName: Atanacak kişinin adı (varsa)
- dueDate: Son teslim tarihi (format: yyyy-MM-dd, yoksa null)
- priority: Öncelik (Low, Normal, High, Critical)
- department: Departman adı (varsa)

Sadece JSON döndür, başka açıklama yapma.";

                var response = await CallAIAsync(prompt);
                var taskData = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

                // Employee ID bul
                int assignedToId = 0;
                if (taskData.ContainsKey("assignedToName"))
                {
                    var employees = await _databaseService.GetEmployeesAsync();
                    var assignedName = taskData["assignedToName"].ToString().ToLower();
                    var employee = employees.FirstOrDefault(e => 
                        e.FirstName.ToLower().Contains(assignedName) ||
                        e.LastName.ToLower().Contains(assignedName));
                    if (employee != null)
                        assignedToId = employee.Id;
                }

                // Department ID bul
                int departmentId = 1; // Default
                if (taskData.ContainsKey("department"))
                {
                    var departments = await _databaseService.GetDepartmentsAsync();
                    var deptName = taskData["department"].ToString().ToLower();
                    var dept = departments.FirstOrDefault(d => 
                        d.Name.ToLower().Contains(deptName));
                    if (dept != null)
                        departmentId = dept.Id;
                }

                // Priority parse
                TaskPriority priority = TaskPriority.Normal;
                if (taskData.ContainsKey("priority"))
                {
                    Enum.TryParse<TaskPriority>(taskData["priority"].ToString(), out priority);
                }

                // Due date parse
                DateTime? dueDate = null;
                if (taskData.ContainsKey("dueDate") && taskData["dueDate"] != null)
                {
                    if (DateTime.TryParse(taskData["dueDate"].ToString(), out DateTime parsedDate))
                        dueDate = parsedDate;
                }

                return new TaskModel
                {
                    Title = taskData.ContainsKey("title") ? taskData["title"].ToString() : "Yeni Görev",
                    Description = taskData.ContainsKey("description") ? taskData["description"].ToString() : voiceCommand,
                    AssignedToId = assignedToId,
                    CreatedDate = DateTime.Now,
                    DueDate = dueDate,
                    Priority = priority,
                    DepartmentId = departmentId,
                    Status = TaskStatusModel.Pending
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ParseVoiceCommandToTaskAsync Error: {ex.Message}");
                return null;
            }
        }

        // AI destekli personel atama önerisi
        public async System.Threading.Tasks.Task<EmployeeRecommendation> RecommendEmployeeForTaskAsync(TaskModel task)
        {
            try
            {
                var employees = await _databaseService.GetEmployeesAsync();
                var tasks = await _databaseService.GetTasksAsync();

                var prompt = $@"Aşağıdaki görev için en uygun çalışanı öner:
Görev: {task.Title}
Açıklama: {task.Description}
Gerekli Yetenekler: {task.SkillsRequired}
Öncelik: {task.Priority}
Tahmini Süre: {task.EstimatedHours} saat

Çalışanlar:
{string.Join("\n", employees.Select(e => $"- {e.FullName} (Yetenekler: {e.Skills}, Mevcut İş Yükü: {e.CurrentWorkload}/{e.MaxWorkload} saat)"))}

Mevcut Görevler:
{string.Join("\n", tasks.Where(t => t.Status != TaskStatusModel.Completed).Select(t => $"- {t.Title} ({t.AssignedToId})"))}

En uygun 3 çalışanı öncelik sırasına göre listele ve nedenlerini açıkla.";

                var response = await CallAIAsync(prompt);
                
                // Response'dan en uygun çalışanı bul
                var bestMatch = employees
                    .OrderByDescending(e => CalculateEmployeeScore(e, task, tasks))
                    .FirstOrDefault();

                return new EmployeeRecommendation
                {
                    RecommendedEmployee = bestMatch,
                    Score = bestMatch != null ? CalculateEmployeeScore(bestMatch, task, tasks) : 0,
                    Reason = response,
                    AlternativeEmployees = employees
                        .OrderByDescending(e => CalculateEmployeeScore(e, task, tasks))
                        .Take(3)
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecommendEmployeeForTaskAsync Error: {ex.Message}");
                return null;
            }
        }

        // AI alt görev sihirbazı
        public async System.Threading.Tasks.Task<List<SubTask>> BreakDownTaskAsync(string taskDescription)
        {
            try
            {
                var prompt = $@"Aşağıdaki büyük görevi mantıklı alt görevlere böl:
Görev: {taskDescription}

Her alt görev için:
- title: Alt görev başlığı
- description: Açıklama
- estimatedHours: Tahmini süre (saat)
- order: Sıralama (1, 2, 3...)

JSON array formatında döndür, sadece JSON, başka açıklama yapma.";

                var response = await CallAIAsync(prompt);
                var subTasks = JsonConvert.DeserializeObject<List<SubTask>>(response);
                return subTasks ?? new List<SubTask>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BreakDownTaskAsync Error: {ex.Message}");
                return new List<SubTask>();
            }
        }

        // Anomali tespiti
        public async System.Threading.Tasks.Task<List<AnomalyDetection>> DetectAnomaliesAsync()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync();
                var anomalies = new List<AnomalyDetection>();

                foreach (var task in tasks.Where(t => t.Status != TaskStatusModel.Completed && t.DueDate.HasValue))
                {
                    var daysOverdue = (DateTime.Now - task.DueDate.Value).TotalDays;
                    if (daysOverdue > 3)
                    {
                        anomalies.Add(new AnomalyDetection
                        {
                            Task = task,
                            Type = AnomalyType.Overdue,
                            Severity = daysOverdue > 7 ? AnomalySeverity.Critical : AnomalySeverity.High,
                            Message = $"Bu görev {daysOverdue:F0} gün gecikmiş. Müdahale gerekli."
                        });
                    }
                }

                // Sürekli ertelenen görevler
                var employees = await _databaseService.GetEmployeesAsync();
                foreach (var employee in employees)
                {
                    var employeeTasks = tasks.Where(t => t.AssignedToId == employee.Id && t.Status == TaskStatusModel.Pending).ToList();
                    if (employeeTasks.Count > 5 && employee.CurrentWorkload > employee.MaxWorkload * 0.8)
                    {
                        anomalies.Add(new AnomalyDetection
                        {
                            Task = null,
                            Type = AnomalyType.WorkloadOverload,
                            Severity = AnomalySeverity.Medium,
                            Message = $"{employee.FullName} çok yoğun. İş yükü dağıtımı gerekli."
                        });
                    }
                }

                return anomalies;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetectAnomaliesAsync Error: {ex.Message}");
                return new List<AnomalyDetection>();
            }
        }

        // Günlük brifing oluştur
        public async System.Threading.Tasks.Task<string> GenerateDailyBriefingAsync(int employeeId)
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync(employeeId);
                var meetings = await _databaseService.GetMeetingsAsync(employeeId, DateTime.Today);
                var employee = await _databaseService.GetEmployeeAsync(employeeId);

                var prompt = $@"{employee?.FullName} için günlük brifing oluştur:

Bugünkü Görevler:
{string.Join("\n", tasks.Where(t => t.Status != TaskStatusModel.Completed).Select(t => $"- {t.Title} (Öncelik: {t.Priority}, Teslim: {t.DueDate:dd.MM.yyyy})"))}

Bugünkü Toplantılar:
{string.Join("\n", meetings.Select(m => $"- {m.Title} ({m.StartTime:HH:mm})"))}

Kısa, samimi ve motive edici bir brifing yaz. Türkçe.";

                return await CallAIAsync(prompt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateDailyBriefingAsync Error: {ex.Message}");
                return "Günlük brifing oluşturulamadı.";
            }
        }

        // AI API çağrısı
        private async System.Threading.Tasks.Task<string> CallAIAsync(string prompt)
        {
            try
            {
                // OpenAI format
                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = "Sen bir ofis otomasyon asistanısın. Türkçe yanıt ver." },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_tokens = 1000
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.PostAsync($"{_apiUrl}/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<OpenAIResponse>(responseJson);
                return result?.choices?[0]?.message?.content ?? "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CallAIAsync Error: {ex.Message}");
                return "AI servisi yanıt veremedi.";
            }
        }

        private double CalculateEmployeeScore(Employee employee, TaskModel task, List<TaskModel> allTasks)
        {
            double score = 0;

            // İş yükü skoru (düşük iş yükü = yüksek skor)
            var workloadRatio = employee.WorkloadPercentage / 100.0;
            score += (1 - workloadRatio) * 40;

            // Yetenek uyumu (basit kontrol)
            if (!string.IsNullOrEmpty(task.SkillsRequired) && !string.IsNullOrEmpty(employee.Skills))
            {
                var taskSkills = task.SkillsRequired.ToLower();
                var empSkills = employee.Skills.ToLower();
                if (empSkills.Contains(taskSkills) || taskSkills.Contains(empSkills))
                    score += 30;
            }

            // Departman uyumu
            if (employee.DepartmentId == task.DepartmentId)
                score += 20;

            // Mevcut görev sayısı (az görev = yüksek skor)
            var currentTaskCount = allTasks.Count(t => t.AssignedToId == employee.Id && t.Status != TaskStatusModel.Completed);
            score += Math.Max(0, 10 - currentTaskCount);

            return score;
        }
    }

    // Helper classes
    public class EmployeeRecommendation
    {
        public Employee RecommendedEmployee { get; set; }
        public double Score { get; set; }
        public string Reason { get; set; }
        public List<Employee> AlternativeEmployees { get; set; }
    }

    public class SubTask
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int EstimatedHours { get; set; }
        public int Order { get; set; }
    }

    public class AnomalyDetection
    {
        public TaskModel Task { get; set; }
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

    public class OpenAIResponse
    {
        public List<Choice> choices { get; set; }
    }

    public class Choice
    {
        public Message message { get; set; }
    }

    public class Message
    {
        public string content { get; set; }
    }
}

