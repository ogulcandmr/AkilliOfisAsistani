using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TaskModel = OfisAsistan.Models.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using Newtonsoft.Json;
using OfisAsistan.Models;

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

        // Task Operations
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
                var response = await _httpClient.PostAsync($"{_supabaseUrl}/rest/v1/tasks", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TaskModel>(responseJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTaskAsync Error: {ex.Message}");
                return null;
            }
        }

        public async System.Threading.Tasks.Task<bool> UpdateTaskAsync(TaskModel task)
        {
            try
            {
                var json = JsonConvert.SerializeObject(task);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_supabaseUrl}/rest/v1/tasks?id=eq.{task.Id}")
                {
                    Content = content
                };
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTaskAsync Error: {ex.Message}");
                return false;
            }
        }

        // Employee Operations
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEmployeesAsync Error: {ex.Message}");
                return new List<Employee>();
            }
        }

        public async System.Threading.Tasks.Task<Employee> GetEmployeeAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/employees?id=eq.{id}&select=*");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var employees = JsonConvert.DeserializeObject<List<Employee>>(json);
                return employees?.Count > 0 ? employees[0] : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEmployeeAsync Error: {ex.Message}");
                return null;
            }
        }

        // Department Operations
        public async System.Threading.Tasks.Task<List<Department>> GetDepartmentsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/departments?select=*");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<Department>>(json) ?? new List<Department>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDepartmentsAsync Error: {ex.Message}");
                return new List<Department>();
            }
        }

        // Meeting Operations
        public async System.Threading.Tasks.Task<List<Meeting>> GetMeetingsAsync(int? employeeId = null, DateTime? startDate = null)
        {
            try
            {
                var url = $"{_supabaseUrl}/rest/v1/meetings?select=*";
                if (startDate.HasValue)
                    url += $"&start_time=gte.{startDate.Value:yyyy-MM-ddTHH:mm:ss}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var meetings = JsonConvert.DeserializeObject<List<Meeting>>(json) ?? new List<Meeting>();
                
                // Filter by employee if needed
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

        public async System.Threading.Tasks.Task<Meeting> CreateMeetingAsync(Meeting meeting)
        {
            try
            {
                var json = JsonConvert.SerializeObject(meeting);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_supabaseUrl}/rest/v1/meetings", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<Meeting>(responseJson);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateMeetingAsync Error: {ex.Message}");
                return null;
            }
        }

        // Statistics
        public async System.Threading.Tasks.Task<Dictionary<string, object>> GetTaskStatisticsAsync(int? departmentId = null)
        {
            try
            {
                var tasks = await GetTasksAsync();
                if (departmentId.HasValue)
                    tasks = tasks.FindAll(t => t.DepartmentId == departmentId.Value);

                return new Dictionary<string, object>
                {
                    { "Total", tasks.Count },
                    { "Pending", tasks.FindAll(t => t.Status == TaskStatusModel.Pending).Count },
                    { "InProgress", tasks.FindAll(t => t.Status == TaskStatusModel.InProgress).Count },
                    { "Completed", tasks.FindAll(t => t.Status == TaskStatusModel.Completed).Count },
                    { "Overdue", tasks.FindAll(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Now && t.Status != TaskStatusModel.Completed).Count }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTaskStatisticsAsync Error: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }
    }
}

