using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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

        // CHATBOT HAFIZASI (Sohbet geçmişini burada tutacağız)
        private List<object> _chatHistory;

        public AIService(string apiKey, string apiUrl, DatabaseService databaseService)
        {
            _apiKey = apiKey;
            _baseApiUrl = apiUrl?.TrimEnd('/');
            _databaseService = databaseService;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(Constants.AI_TIMEOUT_SECONDS);

            // Sohbet geçmişini başlat ve sistem rolünü ata
            ResetChatHistory();
        }

        // --- 1. SOHBET YÖNETİMİ (CHATBOT) ---

        /// <summary>
        /// Sohbet geçmişini temizler ve asistanı sıfırlar.
        /// </summary>
        public void ResetChatHistory()
        {
            _chatHistory = new List<object>
            {
                new { role = "system", content = "Sen 'Ofis Asistanı' adında yardımsever, zeki ve profesyonel bir yapay zeka asistanısın. Türkçe konuş. Kullanıcının ofis işlerini, görevlerini ve planlamalarını yönetmesine yardımcı ol. Kısa ve net cevaplar ver." }
            };
        }

        /// <summary>
        /// Chatbot ile konuşmak için bu fonksiyonu kullanın. Geçmişi hatırlar.
        /// </summary>
        public async Task<string> ChatWithAssistantAsync(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return "Lütfen bir mesaj girin.";
            }

            // Kullanıcı mesajını geçmişe ekle
            _chatHistory.Add(new { role = "user", content = userMessage });

            // Chat geçmişi limitini kontrol et (sistem mesajı hariç)
            if (_chatHistory.Count > Constants.MAX_CHAT_HISTORY + 1) // +1 sistem mesajı için
            {
                // En eski mesajları sil (sistem mesajı hariç)
                var systemMessage = _chatHistory[0];
                _chatHistory.RemoveRange(1, _chatHistory.Count - Constants.MAX_CHAT_HISTORY - 1);
            }

            // API'ye tüm geçmişi gönder
            string aiResponse = await SendRequestToAIAsync(_chatHistory);

            if (!string.IsNullOrEmpty(aiResponse))
            {
                // AI cevabını da geçmişe ekle
                _chatHistory.Add(new { role = "assistant", content = aiResponse });
                return aiResponse;
            }

            return "Üzgünüm, şu an bağlantı kuramıyorum.";
        }

        // --- 2. ÇEKİRDEK AI MOTORU (RETRY MEKANİZMALI) ---

        private async Task<string> SendRequestToAIAsync(object messages, bool jsonMode = false)
        {
            int maxRetries = Constants.AI_MAX_RETRIES;
            int delay = Constants.AI_INITIAL_DELAY_MS;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // URL Düzenleme
                    string finalUrl = _baseApiUrl;
                    if (finalUrl.Contains("groq.com"))
                        finalUrl = "https://api.groq.com/openai/v1/chat/completions";
                    else if (!finalUrl.EndsWith("/chat/completions"))
                        finalUrl += "/v1/chat/completions";

                    // İstek Gövdesi
                    var requestBody = new
                    {
                        model = "llama-3.3-70b-versatile", // Veya gpt-4o-mini vs.
                        messages = messages,
                        temperature = jsonMode ? 0.3 : 0.7, // JSON istiyorsak daha tutarlı olsun
                        response_format = jsonMode ? new { type = "json_object" } : null
                    };

                    var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                    var response = await _httpClient.PostAsync(finalUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"AI API Hatası ({response.StatusCode}): {err}");

                        // Eğer 429 (Too Many Requests) veya 5xx hatasıysa bekle ve tekrar dene
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            await System.Threading.Tasks.Task.Delay(delay);
                            delay *= 2; // Bekleme süresini katla (Exponential Backoff)
                            continue;
                        }
                        return null;
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(responseJson))
                    {
                        System.Diagnostics.Debug.WriteLine("AI API: Boş yanıt alındı.");
                        return null;
                    }

                    dynamic result = JsonConvert.DeserializeObject(responseJson);
                    if (result?.choices == null || result.choices.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("AI API: Choices boş veya null.");
                        return null;
                    }

                    return result?.choices?[0]?.message?.content?.ToString();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AI Bağlantı Hatası (Deneme {i + 1}): {ex.Message}");
                    if (i == maxRetries - 1) return null; // Son deneme de başarısızsa null dön
                    await System.Threading.Tasks.Task.Delay(delay);
                }
            }
            return null;
        }

        // Tek seferlik komutlar için yardımcı metod (Stateless)
        private async Task<string> CallSingleShotAsync(string systemPrompt, string userPrompt, bool forceJson = false)
        {
            var messages = new[]
            {
                new { role = "system", content = systemPrompt + (forceJson ? " Yanıtı SADECE geçerli bir JSON formatında ver. Başka açıklama yapma." : "") },
                new { role = "user", content = userPrompt }
            };
            return await SendRequestToAIAsync(messages, forceJson);
        }

        // --- 3. AKILLI JSON TEMİZLEYİCİ (Regex Destekli) ---
        private string ExtractJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // Markdown temizliği
            text = text.Replace("```json", "").Replace("```JSON", "").Replace("```", "").Trim();

            // JSON bloğunu bul
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');

            if (start > -1 && end > start)
            {
                return text.Substring(start, end - start + 1);
            }

            return text; // Bulamazsa ham metni döndür (belki zaten temizdir)
        }

        // --- 4. GELİŞMİŞ PERSONEL ÖNERİSİ ---
        public async Task<EmployeeRecommendation> RecommendEmployeeForTaskAsync(AppTask task)
        {
            if (task == null)
            {
                System.Diagnostics.Debug.WriteLine("RecommendEmployeeForTaskAsync: Task null!");
                return null;
            }

            if (_databaseService == null)
            {
                System.Diagnostics.Debug.WriteLine("RecommendEmployeeForTaskAsync: DatabaseService null!");
                return null;
            }

            var employees = await _databaseService.GetEmployeesAsync();
            var activeEmployees = employees?.Where(e => e != null && e.IsActive).ToList();

            if (activeEmployees == null || !activeEmployees.Any()) return null;

            // Veriyi string'e çevir
            var empList = string.Join("\n", activeEmployees.Select(e =>
                $"- ID:{e.Id}, İsim:{e.FullName}, Dept:{e.DepartmentId}, Yetenekler:[{e.Skills}], ŞuAnkiYük:%{e.WorkloadPercentage}"
            ));

            string systemPrompt = @"Sen uzman bir İnsan Kaynakları yöneticisisin. Görev için en uygun personeli seçmelisin.
                                    Kriterler:
                                    1. Yetenek uyumu (En önemli).
                                    2. İş yükü dengesi (Aşırı yüklü kişiye verme).
                                    3. Departman uygunluğu.";

            string userPrompt = $@"
                GÖREV: {task.Title}
                GEREKEN YETENEKLER: {task.SkillsRequired}
                DEPARTMAN ID: {task.DepartmentId}
                
                ADAY LİSTESİ:
                {empList}

                Lütfen analiz et ve sonucu aşağıdaki JSON formatında ver:
                {{
                    ""TargetId"": 123,
                    ""Reason"": ""Neden seçildiğine dair detaylı ve mantıklı bir açıklama.""
                }}";

            var aiResponse = await CallSingleShotAsync(systemPrompt, userPrompt, true);

            // Yapay Zeka Cevabını İşle
            if (!string.IsNullOrEmpty(aiResponse))
            {
                try
                {
                    string json = ExtractJson(aiResponse);
                    if (string.IsNullOrEmpty(json))
                    {
                        System.Diagnostics.Debug.WriteLine("AI JSON çıkarılamadı.");
                    }
                    else
                    {
                        var obj = JObject.Parse(json);
                        var targetIdToken = obj["TargetId"];
                        var reasonToken = obj["Reason"];

                        if (targetIdToken != null && reasonToken != null)
                        {
                            int selectedId = targetIdToken.Value<int>();
                            string reason = reasonToken.Value<string>() ?? "Neden belirtilmedi.";

                            var selectedEmp = activeEmployees.FirstOrDefault(e => e != null && e.Id == selectedId);
                            if (selectedEmp != null)
                            {
                                return new EmployeeRecommendation
                                {
                                    RecommendedEmployee = selectedEmp,
                                    Reason = reason
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("AI JSON Parse Hatası: " + ex.Message);
                }
            }

            // FALLBACK (Yedek Plan): AI başarısız olursa matematiksel hesap yap
            var fallback = activeEmployees
                .Where(e => e != null)
                .OrderByDescending(e => !string.IsNullOrEmpty(task.SkillsRequired) && !string.IsNullOrEmpty(e.Skills) && e.Skills.Contains(task.SkillsRequired)) // Yetenek var mı?
                .ThenBy(e => e.WorkloadPercentage) // Sonra iş yükü az olan
                .FirstOrDefault();

            if (fallback == null)
            {
                return null;
            }

            return new EmployeeRecommendation
            {
                RecommendedEmployee = fallback,
                Reason = "AI servisine erişilemediği için iş yükü en uygun personel otomatik seçildi."
            };
        }

        // --- 5. DETAYLI ANOMALİ TESPİTİ ---
        public async Task<List<AnomalyDetection>> DetectAnomaliesAsync()
        {
            var anomalies = new List<AnomalyDetection>();
            var tasks = await _databaseService.GetTasksAsync();
            var employees = await _databaseService.GetEmployeesAsync();

            // Tamamlanmamış görevleri al
            var activeTasks = tasks.Where(t => t.Status != TaskStatusEnum.Completed).ToList();
            if (!activeTasks.Any()) return anomalies;

            // Veri seti hazırlığı (Anonimleştirilmiş ve özet)
            var analysisData = activeTasks.Select(t => new
            {
                t.Id,
                t.Title,
                DueDate = t.DueDate?.ToString("yyyy-MM-dd"),
                Priority = t.Priority.ToString(),
                AssignedPerson = employees.FirstOrDefault(e => e.Id == t.AssignedToId)?.FullName ?? "Atanmamış",
                AssignedPersonWorkload = employees.FirstOrDefault(e => e.Id == t.AssignedToId)?.WorkloadPercentage ?? 0
            }).Take(Constants.AI_MAX_TASKS_FOR_ANALYSIS).ToList();

            string systemPrompt = "Sen bir Proje Denetçisisin. Projedeki riskleri, mantıksız atamaları ve gecikmeleri tespit et.";
            string userPrompt = $@"
                Aşağıdaki görev listesini analiz et.
                BUGÜNÜN TARİHİ: {DateTime.Now:yyyy-MM-dd}

                VERİLER:
                {JsonConvert.SerializeObject(analysisData)}

                Kurallar:
                - Tarihi geçmiş görevler: Yüksek Risk (High)
                - İş yükü %80 üzeri kişiye atanan yeni görevler: Orta Risk (Medium)
                - Atanmamış yüksek öncelikli görevler: Yüksek Risk (High)
                
                Çıktı Formatı (JSON Dizisi):
                {{
                    ""anomalies"": [
                        {{ ""TaskId"": 1, ""Message"": ""Açıklama"", ""Severity"": ""High"" }}
                    ]
                }}";

            var response = await CallSingleShotAsync(systemPrompt, userPrompt, true);

            if (!string.IsNullOrEmpty(response))
            {
                try
                {
                    string cleanJson = ExtractJson(response);
                    var root = JObject.Parse(cleanJson);

                    if (root["anomalies"] is JArray arr)
                    {
                        foreach (var item in arr)
                        {
                            int tId = (int)item["TaskId"];
                            var originalTask = tasks.FirstOrDefault(t => t.Id == tId);
                            if (originalTask != null)
                            {
                                string sevStr = (string)item["Severity"];
                                var severity = sevStr.StartsWith("H", StringComparison.OrdinalIgnoreCase) ? AnomalySeverity.High :
                                               (sevStr.StartsWith("M", StringComparison.OrdinalIgnoreCase) ? AnomalySeverity.Medium : AnomalySeverity.Low);

                                anomalies.Add(new AnomalyDetection
                                {
                                    Task = originalTask,
                                    Message = (string)item["Message"],
                                    Severity = severity
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Anomali Parse Hatası: " + ex.Message);
                }
            }

            return anomalies;
        }

        // --- 6. AKILLI GÖREV BÖLÜCÜ (Task Breakdown) ---
        public async Task<List<SubTask>> BreakDownTaskAsync(string taskDescription)
        {
            string systemPrompt = "Sen bir proje yöneticisisin. Verilen ana görevi mantıklı, yapılabilir küçük alt görevlere böl.";
            string userPrompt = $@"
                GÖREV TANIMI: {taskDescription}
                
                Bu görevi alt adımlara ayır ve her biri için tahmini saat (Hours) belirle.
                JSON Formatı:
                {{
                    ""steps"": [
                        {{ ""Title"": ""Gereksinim analizi yap"", ""Hours"": 2 }},
                        {{ ""Title"": ""Veritabanı tasarımını çıkar"", ""Hours"": 4 }}
                    ]
                }}";

            var response = await CallSingleShotAsync(systemPrompt, userPrompt, true);
            var resultList = new List<SubTask>();

            if (!string.IsNullOrEmpty(response))
            {
                try
                {
                    var obj = JObject.Parse(ExtractJson(response));
                    foreach (var s in obj["steps"])
                    {
                        resultList.Add(new SubTask
                        {
                            Title = (string)s["Title"],
                            EstimatedHours = (int)s["Hours"]
                        });
                    }
                }
                catch { /* Sessizce başarısız ol, boş liste dön */ }
            }
            return resultList;
        }

        // --- 7. GÜNLÜK ÖZET (Smart Briefing) ---
        public async Task<string> GenerateDailyBriefingAsync(int employeeId)
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync(employeeId);
                var employees = await _databaseService.GetEmployeesAsync();
                var emp = employees?.FirstOrDefault(e => e != null && e.Id == employeeId);

                if (tasks == null || !tasks.Any())
                {
                    return $"Merhaba {emp?.FullName ?? "Değerli Çalışan"}! 🎉\n\nBugün üzerinizde bekleyen görev bulunmuyor. İyi çalışmalar!";
                }

                var activeTasks = tasks.Where(t => t != null && t.Status != TaskStatusEnum.Completed && t.Status != TaskStatusEnum.Cancelled).ToList();
                var overdueTasks = activeTasks.Where(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Now).ToList();
                var todayTasks = activeTasks.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date == DateTime.Now.Date).ToList();
                var highPriorityTasks = activeTasks.Where(t => t.Priority == TaskPriority.High || t.Priority == TaskPriority.Critical).ToList();

                if (!activeTasks.Any())
                {
                    return $"Merhaba {emp?.FullName ?? "Değerli Çalışan"}! 🎉\n\nTüm görevleriniz tamamlanmış görünüyor. Harika iş çıkardınız!";
                }

                string systemPrompt = @"Sen profesyonel ve motive edici bir ofis asistanısın. Kullanıcıya günlük brifing verirken:
- Kısa, net ve anlaşılır ol
- Acil ve önemli görevleri önceliklendir
- Gecikmiş görevler varsa bunları vurgula
- Motive edici ve pozitif bir dil kullan
- Maksimum 4-5 cümle kullan
- Türkçe yaz";

                string taskDetails = "";
                if (overdueTasks.Any())
                {
                    taskDetails += $"⚠️ GECİKMİŞ GÖREVLER ({overdueTasks.Count}): {string.Join(", ", overdueTasks.Select(t => t.Title))}\n";
                }
                if (todayTasks.Any())
                {
                    taskDetails += $"📅 BUGÜN TESLİM ({todayTasks.Count}): {string.Join(", ", todayTasks.Select(t => t.Title))}\n";
                }
                if (highPriorityTasks.Any())
                {
                    taskDetails += $"🔥 YÜKSEK ÖNCELİK ({highPriorityTasks.Count}): {string.Join(", ", highPriorityTasks.Select(t => t.Title))}\n";
                }
                taskDetails += $"📋 TOPLAM AKTİF GÖREV: {activeTasks.Count}";

                string userPrompt = $@"
KULLANICI: {emp?.FullName ?? "Çalışan"}
TOPLAM AKTİF GÖREV: {activeTasks.Count}

GÖREV DETAYLARI:
{taskDetails}

Lütfen bu kullanıcıya profesyonel, motive edici ve kısa bir günlük brifing ver. Gecikmiş görevler varsa bunları özellikle vurgula.";

                var response = await CallSingleShotAsync(systemPrompt, userPrompt);
                
                if (string.IsNullOrEmpty(response))
                {
                    // Fallback: Basit bir özet
                    var summary = $"Merhaba {emp?.FullName ?? "Değerli Çalışan"}! 👋\n\n";
                    if (overdueTasks.Any())
                    {
                        summary += $"⚠️ {overdueTasks.Count} gecikmiş göreviniz var. Lütfen öncelik verin.\n";
                    }
                    if (todayTasks.Any())
                    {
                        summary += $"📅 Bugün {todayTasks.Count} görevinizin teslim tarihi var.\n";
                    }
                    summary += $"📋 Toplam {activeTasks.Count} aktif göreviniz bulunuyor.\n\nİyi çalışmalar! 💪";
                    return summary;
                }

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateDailyBriefingAsync Error: {ex.Message}");
                return "Brifing oluşturulurken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
            }
        }

    }
}