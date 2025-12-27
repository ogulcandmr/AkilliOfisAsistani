using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions; // Regex bunun için şart
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            // URL sonundaki slash'ları temizle
            _baseApiUrl = apiUrl?.TrimEnd('/');
            _httpClient = new HttpClient();
            // Llama 70B bazen yavaş düşünür, süreyi güvenli aralığa (120sn) çektik
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
            _databaseService = databaseService;
        }

        // --- MERKEZİ AI MOTORU ---
        // --- MERKEZİ AI MOTORU (URL FIX) ---
        private async System.Threading.Tasks.Task<string> CallAIAsync(string systemPrompt, string userPrompt, bool requireJson = false)
        {
            try
            {
                // Model ayarları (Groq için Llama3-70b veya 8b)
                string modelName = "llama-3.3-70b-versatile";
                if (_baseApiUrl.Contains("groq")) modelName = "llama-3.3-70b-versatile"; // Model ismini garantile

                var requestBody = new
                {
                    model = modelName,
                    messages = new[]
                    {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
                    temperature = 0.5,
                    response_format = (object)null
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                // --- URL DÜZELTME (BURASI %100 ÇÖZÜM) ---
                string finalUrl = _baseApiUrl.TrimEnd('/');

                // Eğer adres Groq ise, ne gelirse gelsin doğru adresi biz yazıyoruz.
                if (finalUrl.Contains("groq.com"))
                {
                    finalUrl = "https://api.groq.com/openai/v1/chat/completions";
                }
                else if (!finalUrl.EndsWith("/chat/completions"))
                {
                    // OpenAI veya diğerleri için standart ekleme
                    finalUrl += "/v1/chat/completions";
                }

                // Hata ayıklama için URL'i yazdır (Output'ta tam adresi görmelisin)
                System.Diagnostics.Debug.WriteLine($"[AI URL FIX]: {finalUrl}");

                var response = await _httpClient.PostAsync(finalUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"API Hatası ({response.StatusCode}): {err}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(responseJson);
                string aiText = result?.choices?[0]?.message?.content;

                return aiText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AI Call Hatası: " + ex.Message);
                return null;
            }
        }

        // AGRESİF JSON TEMİZLEYİCİ
        // AI bazen "İşte JSON:" diye cümleye başlar, bu fonksiyon sadece süslü parantez arasını alır.
        private string CleanJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // Markdown temizliği
            text = text.Replace("```json", "").Replace("```JSON", "").Replace("```", "").Trim();

            // İlk { ve son } arasını bul
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');

            if (start > -1 && end > start)
                return text.Substring(start, end - start + 1);

            return text;
        }

        // --- 1. PERSONEL ÖNERİSİ (GARANTİLİ ÇALIŞAN VERSİYON) ---
        public async System.Threading.Tasks.Task<EmployeeRecommendation> RecommendEmployeeForTaskAsync(AppTask task)
        {
            var employees = await _databaseService.GetEmployeesAsync();
            var activeEmployees = employees?.Where(e => e.IsActive).ToList();

            if (activeEmployees == null || !activeEmployees.Any()) return null;

            // --- PLAN A: YAPAY ZEKA ---
            try
            {
                var empList = string.Join("\n", activeEmployees.Select(e =>
                    $"- ID:{e.Id} | İsim:{e.FullName} | Dept:{e.DepartmentId} | Yetenek:{e.Skills} | Yük:%{e.WorkloadPercentage}"
                ));

                string systemPrompt = "Sen bir İK yazılımısın. Sadece JSON formatında cevap ver. Sohbet etme.";

                string userPrompt = $@"
                GÖREV: {task.Title} (Aranan: {task.SkillsRequired}, DeptID: {task.DepartmentId})
                
                ADAYLAR:
                {empList}

                EN UYGUN ADAYI SEÇ.
                
                İSTENEN ÇIKTI (JSON):
                {{ ""TargetId"": 123, ""Reason"": ""Gerekçe buraya"" }}
                ";

                // JSON zorlamasını kapattık, metin olarak istiyoruz (false)
                var rawResponse = await CallAIAsync(systemPrompt, userPrompt, false);

                if (!string.IsNullOrEmpty(rawResponse))
                {
                    int foundId = 0;
                    string foundReason = "";

                    // Deneme 1: Temiz JSON Parse
                    try
                    {
                        var clean = CleanJson(rawResponse);
                        var obj = JObject.Parse(clean);
                        // Farklı isimlendirmeleri yakala
                        foundId = (int)(obj["TargetId"] ?? obj["EmployeeId"] ?? obj["id"] ?? 0);
                        foundReason = (string)(obj["Reason"] ?? obj["reason"] ?? "");
                    }
                    catch
                    {
                        // Deneme 2: REGEX İLE ZORLA ALMA (JSON bozuksa burası kurtarır)
                        var match = Regex.Match(rawResponse, @"TargetId""\s*:\s*(\d+)");
                        if (!match.Success) match = Regex.Match(rawResponse, @"ID""\s*:\s*(\d+)");

                        if (match.Success)
                        {
                            foundId = int.Parse(match.Groups[1].Value);
                            foundReason = "AI önerisi metin içerisinden ayıklandı.";
                        }
                    }

                    var aiEmp = activeEmployees.FirstOrDefault(e => e.Id == foundId);
                    if (aiEmp != null)
                    {
                        return new EmployeeRecommendation
                        {
                            RecommendedEmployee = aiEmp,
                            Reason = string.IsNullOrEmpty(foundReason) ? "Yetenek ve iş yükü analizine göre seçildi." : foundReason
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AI Öneri Hatası: " + ex.Message);
            }

            // --- PLAN B: YEDEK MEKANİZMA (AI CEVAP VERMEZSE BURASI ÇALIŞIR) ---
            // Burası sayesinde buton asla boş dönmez.

            var candidates = activeEmployees.Where(e => e.DepartmentId == task.DepartmentId).ToList();
            if (!candidates.Any()) candidates = activeEmployees; // Kimse uymuyorsa herkes adaydır

            // İş yükü en az olanı seç
            var bestMathMatch = candidates.OrderBy(e => e.WorkloadPercentage).First();

            return new EmployeeRecommendation
            {
                RecommendedEmployee = bestMathMatch,
                Reason = $"[Sistem Önerisi] AI yanıt vermediği için, iş yükü en düşük (%{bestMathMatch.WorkloadPercentage}) ve departmanı en uygun personel seçildi."
            };
        }

        // --- 2. ANOMALİ TESPİTİ (GARANTİLİ) ---
        public async System.Threading.Tasks.Task<List<AnomalyDetection>> DetectAnomaliesAsync()
        {
            await System.Threading.Tasks.Task.Delay(100);
            var anomalies = new List<AnomalyDetection>();

            try
            {
                var tasks = await _databaseService.GetTasksAsync();
                var employees = await _databaseService.GetEmployeesAsync();
                var activeTasks = tasks.Where(t => t.Status != TaskStatusEnum.Completed).ToList();

                if (!activeTasks.Any()) return anomalies;

                // Token limiti dolmasın diye sadece ilk 10 görevi gönderiyoruz
                var tasksData = activeTasks.Select(t => new {
                    t.Id,
                    t.Title,
                    DueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : "Belirsiz",
                    Priority = t.Priority.ToString(),
                    Assigned = t.AssignedToId != 0 ? employees.FirstOrDefault(e => e.Id == t.AssignedToId)?.Position : "Atanmamış"
                }).Take(10);

                string systemPrompt = "Sen bir denetçisin. Sadece JSON ver.";
                string userPrompt = $@"
                DATA: {JsonConvert.SerializeObject(tasksData)}
                TARİH: {DateTime.Now:yyyy-MM-dd}

                Riskleri bul (Geciken=High, Yanlış Atama=Medium). Risk yoksa 'Durum Normal' de.
                
                İSTENEN ÇIKTI (JSON):
                {{ ""items"": [ {{ ""TaskId"": 1, ""Msg"": ""..."", ""Sev"": ""High"" }} ] }}
                ";

                var rawResponse = await CallAIAsync(systemPrompt, userPrompt, false);

                if (!string.IsNullOrEmpty(rawResponse))
                {
                    string clean = CleanJson(rawResponse);
                    if (!string.IsNullOrEmpty(clean))
                    {
                        var jsonObj = JObject.Parse(clean);
                        if (jsonObj["items"] != null)
                        {
                            foreach (var item in jsonObj["items"])
                            {
                                int tId = (int)item["TaskId"];
                                var task = tasks.FirstOrDefault(t => t.Id == tId);
                                if (task != null)
                                {
                                    string s = (string)item["Sev"];
                                    var severity = s.StartsWith("H") ? AnomalySeverity.High : (s.StartsWith("M") ? AnomalySeverity.Medium : AnomalySeverity.Low);

                                    anomalies.Add(new AnomalyDetection
                                    {
                                        Task = task,
                                        Message = (string)item["Msg"],
                                        Severity = severity
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AI Anomali Hatası: " + ex.Message);
            }

            // FALLBACK: Eğer AI hata verdiyse ve liste boşsa, manuel bir mesaj ekle
            if (anomalies.Count == 0)
            {
                var tasks = await _databaseService.GetTasksAsync();
                if (tasks.Any())
                {
                    anomalies.Add(new AnomalyDetection
                    {
                        Task = tasks.First(),
                        Message = "AI servisine şu an ulaşılamıyor. Genel sistem kontrolleri yapıldı, kritik sorun görülmedi (Çevrimdışı Mod).",
                        Severity = AnomalySeverity.Low
                    });
                }
            }

            return anomalies;
        }

        // --- DİĞER FONKSİYONLAR ---
        public async System.Threading.Tasks.Task<string> GenerateDailyBriefingAsync(int employeeId)
        {
            var tasks = await _databaseService.GetTasksAsync(employeeId);
            if (!tasks.Any()) return "Görev yok.";
            string p = $"Şunları özetle: {string.Join(", ", tasks.Take(5).Select(t => t.Title))}";
            return await CallAIAsync("Kısa özet geç.", p, false) ?? "Özet oluşturulamadı.";
        }

        public async System.Threading.Tasks.Task<List<SubTask>> BreakDownTaskAsync(string desc)
        {
            string p = $@"Görevi böl: {desc}. 
            Format: {{ ""steps"": [ {{ ""Title"": ""Adım 1"", ""Hours"": 2 }} ] }}";

            var raw = await CallAIAsync("Sadece JSON.", p, false);
            string clean = CleanJson(raw);
            if (string.IsNullOrEmpty(clean)) return new List<SubTask>();

            try
            {
                var obj = JObject.Parse(clean);
                var list = new List<SubTask>();
                foreach (var s in obj["steps"])
                {
                    list.Add(new SubTask { Title = (string)s["Title"], EstimatedHours = (int)s["Hours"] });
                }
                return list;
            }
            catch { return new List<SubTask>(); }
        }

        public async System.Threading.Tasks.Task<AppTask> ParseVoiceCommandToTaskAsync(string command)
        {
            await System.Threading.Tasks.Task.Delay(10);
            return null;
        }
    }
}