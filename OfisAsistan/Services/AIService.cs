using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OfisAsistan.Models;

// Alias Tanımları
using AppTask = OfisAsistan.Models.Task;
using TaskStatusEnum = OfisAsistan.Models.TaskStatus;

namespace OfisAsistan.Services
{
    public class AIService
    {
        private readonly string _apiKey;
        private readonly string _baseApiUrl;
        private readonly HttpClient _httpClient;
        private readonly DatabaseService _databaseService;

        public AIService(string apiKey, string apiUrl, DatabaseService databaseService)
        {
            _apiKey = apiKey;
            _baseApiUrl = apiUrl?.TrimEnd('/');
            _httpClient = new HttpClient();
            // Analiz işlemleri uzun sürebilir, süreyi artırdık
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _databaseService = databaseService;
        }

        // =================================================================================
        // MERKEZİ AI MOTORU (Groq Llama-3.3)
        // =================================================================================
        private async System.Threading.Tasks.Task<string> CallAIAsync(string systemPrompt, string userPrompt, bool requireJson = false)
        {
            try
            {
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile", // En güçlü model
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = requireJson ? 0.2 : 0.7, // JSON istiyorsak yaratıcılığı kısıyoruz
                    response_format = requireJson ? new { type = "json_object" } : null
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                // URL Düzeltme
                string finalUrl = _baseApiUrl;
                if (finalUrl.Contains("api.groq.com") && !finalUrl.Contains("/v1"))
                {
                    if (finalUrl.EndsWith("/openai")) finalUrl += "/v1";
                    else if (!finalUrl.Contains("/openai")) finalUrl += "/openai/v1";
                }
                if (!finalUrl.EndsWith("/chat/completions")) finalUrl = finalUrl.TrimEnd('/') + "/chat/completions";

                var response = await _httpClient.PostAsync(finalUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[AI HATA] {response.StatusCode}: {err}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(responseJson);
                string aiText = result?.choices?[0]?.message?.content;

                return requireJson ? CleanJson(aiText) : aiText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI EXCEPTION] {ex.Message}");
                return null;
            }
        }

        private string CleanJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = text.Replace("```json", "").Replace("```", "").Trim();
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');

            if (start == -1 || end == -1)
            {
                // Array kontrolü
                start = text.IndexOf('[');
                end = text.LastIndexOf(']');
            }

            if (start > -1 && end > start) return text.Substring(start, end - start + 1);
            return text;
        }

        // =================================================================================
        // 1. ZEKİ PERSONEL ÖNERİSİ
        // =================================================================================
        public async System.Threading.Tasks.Task<EmployeeRecommendation> RecommendEmployeeForTaskAsync(AppTask task)
        {
            try
            {
                var employees = await _databaseService.GetEmployeesAsync();

                // DÜZELTME: e.Role yerine e.Position kullanıldı
                var empList = string.Join("\n", employees.Select(e =>
                    $"- ID: {e.Id} | İsim: {e.FullName} | Pozisyon: {e.Position ?? "Belirtilmemiş"} | Yetenekler: {e.Skills} | İş Yükü: %{e.WorkloadPercentage:F1}"
                ));

                string systemPrompt = "Sen uzman bir İnsan Kaynakları yöneticisisin. Görev gereksinimlerini çalışanların yetenekleri ve pozisyonlarıyla eşleştirirsin.";

                string userPrompt = $@"
                GÖREV:
                Başlık: {task.Title}
                Açıklama: {task.Description}
                Gereken Öncelik: {task.Priority}

                ADAYLAR:
                {empList}

                GÖREVİN:
                1. Görevi analiz et ve hangi yeteneklerin gerektiğini belirle.
                2. İş yükü %80'in üzerinde olanları ele (çok acil değilse).
                3. Pozisyonu ve yeteneği en uygun olanı seç.
                4. Asla 'ID yüksek diye' gibi saçma nedenler sunma. Mantıklı bir neden yaz.
                
                JSON CEVAP FORMATI:
                {{
                    ""EmployeeId"": 123,
                    ""Reason"": ""Ahmet Bey Backend Developer pozisyonunda ve C# yetkinliği bu görev için uygun.""
                }}";

                var json = await CallAIAsync(systemPrompt, userPrompt, true);
                if (string.IsNullOrEmpty(json)) return null;

                dynamic result = JsonConvert.DeserializeObject(json);
                int empId = (int)result.EmployeeId;
                string reason = (string)result.Reason;

                var bestEmp = employees.FirstOrDefault(e => e.Id == empId);
                return bestEmp != null ? new EmployeeRecommendation { RecommendedEmployee = bestEmp, Reason = reason, Score = 95 } : null;
            }
            catch { return null; }
        }

        // =================================================================================
        // 2. GELİŞMİŞ ANOMALİ TESPİTİ
        // =================================================================================
        public async System.Threading.Tasks.Task<List<AnomalyDetection>> DetectAnomaliesAsync()
        {
            // Async uyarısını çözmek için kısa bekleme
            await System.Threading.Tasks.Task.Delay(10);

            try
            {
                // Verileri çek
                var tasks = await _databaseService.GetTasksAsync();
                var employees = await _databaseService.GetEmployeesAsync();

                // Sadece aktif görevleri analiz et (AI kotası için sınırla: 15)
                var activeTasks = tasks.Where(t => t.Status != TaskStatusEnum.Completed).Take(15).ToList();

                if (!activeTasks.Any()) return new List<AnomalyDetection>();

                // DÜZELTME: Role yerine Position kullanıldı
                var tasksData = activeTasks.Select(t => new {
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Priority,
                    t.DueDate,
                    AssignedTo = employees.FirstOrDefault(e => e.Id == t.AssignedToId)?.FullName ?? "Atanmamış",
                    AssignedPosition = employees.FirstOrDefault(e => e.Id == t.AssignedToId)?.Position ?? "Yok"
                });

                string systemPrompt = "Sen bir Proje Denetçisisin. Görev listesindeki mantıksızlıkları ve riskleri tespit edersin.";

                string userPrompt = $@"Aşağıdaki görev listesini analiz et.
                
                Veriler: {JsonConvert.SerializeObject(tasksData)}
                Bugün: {DateTime.Now:yyyy-MM-dd}

                ARANACAK HATALAR:
                1. Teslim tarihi geçmiş görevler.
                2. Pozisyonu 'Stajyer' veya 'Junior' olanlara 'Kritik' veya çok zor görev verilmesi.
                3. Başlığı çok belirsiz görevler.

                CEVAP FORMATI (JSON Array):
                {{
                    ""anomalies"": [
                        {{ ""TaskId"": 1, ""Type"": ""Overdue"", ""Severity"": ""High"", ""Message"": ""Teslim tarihi geçmiş."" }}
                    ]
                }}
                Hata yoksa boş dizi dön.";

                var json = await CallAIAsync(systemPrompt, userPrompt, true);
                if (string.IsNullOrEmpty(json)) return new List<AnomalyDetection>();

                var anomalies = new List<AnomalyDetection>();
                dynamic result = JsonConvert.DeserializeObject(json);

                if (result.anomalies != null)
                {
                    foreach (var item in result.anomalies)
                    {
                        int tId = (int)item.TaskId;
                        var originalTask = tasks.FirstOrDefault(t => t.Id == tId);
                        if (originalTask != null)
                        {
                            anomalies.Add(new AnomalyDetection
                            {
                                Task = originalTask,
                                Message = (string)item.Message,
                                Type = AnomalyType.QualityIssue, // Default
                                Severity = AnomalySeverity.Medium // Default
                            });
                        }
                    }
                }
                return anomalies;
            }
            catch { return new List<AnomalyDetection>(); }
        }

        // =================================================================================
        // 3. GÜNLÜK BRİFİNG
        // =================================================================================
        public async System.Threading.Tasks.Task<string> GenerateDailyBriefingAsync(int employeeId)
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync(employeeId);
                var activeTasks = tasks.Where(t => t.Status != TaskStatusEnum.Completed).ToList();

                if (!activeTasks.Any()) return "Merhaba! Bugün için aktif bir göreviniz görünmüyor. Kendinizi geliştirmek için harika bir gün!";

                var taskListText = string.Join("\n", activeTasks.Select(t => $"- {t.Title} (Öncelik: {t.Priority}, Teslim: {t.DueDate:dd.MM})"));

                string systemPrompt = "Sen profesyonel bir kariyer koçusun. Türkçe konuşursun.";
                string userPrompt = $@"Şu görevlere sahip çalışan için sabah brifingi hazırla:
                {taskListText}
                
                Kurallar:
                - Samimi ama profesyonel ol.
                - Kritik görevleri vurgula.
                - Maksimum 3 cümle olsun.
                - Markdown kullanma.";

                var result = await CallAIAsync(systemPrompt, userPrompt, false);
                return result ?? "Brifing servisine ulaşılamıyor.";
            }
            catch { return "Brifing hatası."; }
        }

        // =================================================================================
        // 4. GÖREV PARÇALAMA
        // =================================================================================
        public async System.Threading.Tasks.Task<List<SubTask>> BreakDownTaskAsync(string taskDescription)
        {
            string systemPrompt = "Sen bir iş analistisin. Sadece JSON Array döndür.";
            string userPrompt = $@"Görevi 3-5 alt adıma böl: '{taskDescription}'
             Format: [{{ ""Title"": ""..."", ""Description"": ""..."", ""EstimatedHours"": 1 }}]";

            var json = await CallAIAsync(systemPrompt, userPrompt, true);
            if (string.IsNullOrEmpty(json)) return new List<SubTask>();

            try
            {
                return JsonConvert.DeserializeObject<List<SubTask>>(json) ?? new List<SubTask>();
            }
            catch { return new List<SubTask>(); }
        }

        // =================================================================================
        // 5. SESLİ KOMUT (Placeholder)
        // =================================================================================
        public async System.Threading.Tasks.Task<AppTask> ParseVoiceCommandToTaskAsync(string command)
        {
            // İleride ses işleme buraya gelecek
            await System.Threading.Tasks.Task.Delay(10);
            return null;
        }
    }
}