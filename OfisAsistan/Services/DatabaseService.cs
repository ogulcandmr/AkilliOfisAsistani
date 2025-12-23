using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OfisAsistan.Models;

// Alias tanımları (Çakışmaları önlemek için)
using TaskModel = OfisAsistan.Models.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;

namespace OfisAsistan.Services
{
    public class DatabaseService
    {
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private readonly HttpClient _httpClient;

        public DatabaseService(string supabaseUrl, string supabaseKey)
        {
            _supabaseUrl = supabaseUrl;
            _supabaseKey = supabaseKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseKey}");
        }

        // --- TASK (GÖREV) İŞLEMLERİ ---

        public async System.Threading.Tasks.Task<List<TaskModel>> GetTasksAsync(int? employeeId = null, TaskStatusModel? status = null)
        {
            try
            {
                var url = $"{_supabaseUrl}/rest/v1/tasks?select=*";
                if (employeeId.HasValue)
                    url += $"&assigned_to_id=eq.{employeeId}";
                if (status.HasValue)
                    url += $"&status=eq.{(int)status.Value}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<TaskModel>>(json) ?? new List<TaskModel>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTasksAsync Error: {ex.Message}");
                return new List<TaskModel>();
            }
        }

        public async System.Threading.Tasks.Task<TaskModel> CreateTaskAsync(TaskModel task)
        {
            try
            {
                var json = JsonConvert.SerializeObject(task);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/rest/v1/tasks");
                request.Headers.Add("Prefer", "return=representation"); // Supabase'den objeyi geri iste
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var createdTasks = JsonConvert.DeserializeObject<List<TaskModel>>(responseJson);
                var createdTask = createdTasks?.FirstOrDefault();

                if (createdTask != null && createdTask.AssignedToId > 0)
                {
                    await UpdateEmployeeWorkloadAsync(createdTask.AssignedToId);
                }
                return createdTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTaskAsync Error: {ex.Message}");
                return null;
            }
        }

        public async System.Threading.Tasks.Task<bool> UpdateTaskAsync(TaskModel task, int? previousAssignedToId = null)
        {
            try
            {
                var json = JsonConvert.SerializeObject(task);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // PATCH işlemi için SendAsync kullanıyoruz (Daha güvenli)
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_supabaseUrl}/rest/v1/tasks?id=eq.{task.Id}")
                {
                    Content = content
                };
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode) return false;

                if (task.AssignedToId > 0)
                    await UpdateEmployeeWorkloadAsync(task.AssignedToId);

                if (previousAssignedToId.HasValue && previousAssignedToId.Value > 0 && previousAssignedToId.Value != task.AssignedToId)
                    await UpdateEmployeeWorkloadAsync(previousAssignedToId.Value);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTaskAsync Error: {ex.Message}");
                return false;
            }
        }

        // --- YORUM (COMMENT) İŞLEMLERİ ---

        public async System.Threading.Tasks.Task<List<TaskComment>> GetCommentsAsync(int taskId)
        {
            try
            {
                var url = $"{_supabaseUrl}/rest/v1/task_comments?task_id=eq.{taskId}&order=created_at.asc";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<TaskComment>();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<TaskComment>>(json) ?? new List<TaskComment>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCommentsAsync Error: {ex.Message}");
                return new List<TaskComment>();
            }
        }

        public async System.Threading.Tasks.Task<bool> AddCommentAsync(TaskComment comment)
        {
            try
            {
                // ÖNEMLİ DÜZELTME:
                // Modelin tamamını gönderirsek "id: 0" da gider ve veritabanı hata verir.
                // Bu yüzden sadece gerekli alanları içeren yeni bir paket yapıyoruz.
                var payload = new
                {
                    task_id = comment.TaskId,
                    user_id = comment.UserId,
                    user_name = comment.UserName,
                    comment_text = comment.CommentText,
                    created_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") // Tarih formatını sabitledik
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_supabaseUrl}/rest/v1/task_comments", content);

                // Hata varsa konsola yazdıralım ki görebilelim
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Yorum Gönderme Hatası: {err}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddCommentAsync Error: {ex.Message}");
                return false;
            }
        }

        // --- TOPLANTI (MEETING) İŞLEMLERİ --- (Eksik Olan Kısım)

        public async System.Threading.Tasks.Task<List<Meeting>> GetMeetingsAsync(int? employeeId = null, DateTime? startDate = null)
        {
            try
            {
                var url = $"{_supabaseUrl}/rest/v1/meetings?select=*";
                if (startDate.HasValue)
                    url += $"&start_time=gte.{startDate.Value:yyyy-MM-ddTHH:mm:ss}";

                var response = await _httpClient.GetAsync(url);

                // Hata durumunda boş liste dönelim
                if (!response.IsSuccessStatusCode) return new List<Meeting>();

                var json = await response.Content.ReadAsStringAsync();
                var meetings = JsonConvert.DeserializeObject<List<Meeting>>(json) ?? new List<Meeting>();

                // İstemci tarafında filtreleme (Supabase'de array filter biraz karmaşık olabilir)
                if (employeeId.HasValue)
                {
                    var empIdStr = employeeId.Value.ToString();
                    meetings = meetings.FindAll(m =>
                        m.OrganizerId == employeeId.Value ||
                        (m.AttendeeIds != null && m.AttendeeIds.Contains(empIdStr)));
                }

                return meetings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMeetingsAsync Error: {ex.Message}");
                return new List<Meeting>();
            }
        }

        // --- ÇALIŞAN (EMPLOYEE) İŞLEMLERİ ---

        public async System.Threading.Tasks.Task<List<Employee>> GetEmployeesAsync(int? departmentId = null)
        {
            try
            {
                var url = $"{_supabaseUrl}/rest/v1/employees?select=*";
                if (departmentId.HasValue)
                    url += $"&department_id=eq.{departmentId}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<Employee>>(json) ?? new List<Employee>();
            }
            catch
            {
                return new List<Employee>();
            }
        }

        public async System.Threading.Tasks.Task<Employee> GetEmployeeAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/employees?id=eq.{id}&select=*");
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var employees = JsonConvert.DeserializeObject<List<Employee>>(json);
                return employees?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEmployeeAsync Error: {ex.Message}");
                return null;
            }
        }

        public async System.Threading.Tasks.Task<List<Employee>> GetEmployeesForEmployeeRoleAsync()
        {
            return await GetEmployeesAsync();
        }

        private async System.Threading.Tasks.Task UpdateEmployeeWorkloadAsync(int employeeId)
        {
            try
            {
                var tasks = await GetTasksAsync(employeeId);
                var totalHours = tasks
                    .Where(t => t.Status != TaskStatusModel.Completed && t.Status != TaskStatusModel.Cancelled)
                    .Sum(t => t.EstimatedHours);

                var payload = new { current_workload = totalHours };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_supabaseUrl}/rest/v1/employees?id=eq.{employeeId}")
                {
                    Content = content
                };
                await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateWorkload Error: {ex.Message}");
            }
        }

        // --- DEPARTMAN İŞLEMLERİ ---

        public async System.Threading.Tasks.Task<List<Department>> GetDepartmentsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/departments?select=*");
                if (!response.IsSuccessStatusCode) return new List<Department>();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<Department>>(json) ?? new List<Department>();
            }
            catch
            {
                return new List<Department>();
            }
        }

        // --- İSTATİSTİK İŞLEMLERİ ---

        public async System.Threading.Tasks.Task<Dictionary<string, object>> GetTaskStatisticsAsync()
        {
            try
            {
                var tasks = await GetTasksAsync();
                return new Dictionary<string, object>
                {
                    { "Total", tasks.Count },
                    { "Pending", tasks.Count(t => t.Status == TaskStatusModel.Pending) },
                    { "InProgress", tasks.Count(t => t.Status == TaskStatusModel.InProgress) },
                    { "Completed", tasks.Count(t => t.Status == TaskStatusModel.Completed) },
                    { "Overdue", tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Now && t.Status != TaskStatusModel.Completed) }
                };
            }
            catch
            {
                return null;
            }
        }
    }
}