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
            _httpClient.Timeout = TimeSpan.FromSeconds(120); // Uzun işlemler için süre

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
            // Kullanıcı mesajını geçmişe ekle
            _chatHistory.Add(new { role = "user", content = userMessage });

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
            int maxRetries = 3; // Hata olursa 3 kere dene
            int delay = 1000; // İlk bekleme 1 saniye

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
                    dynamic result = JsonConvert.DeserializeObject(responseJson);
                    return result?.choices?[0]?.message?.content;
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
            var employees = await _databaseService.GetEmployeesAsync();
            var activeEmployees = employees?.Where(e => e.IsActive).ToList();

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
                    var obj = JObject.Parse(json);
                    int selectedId = (int)obj["TargetId"];
                    string reason = (string)obj["Reason"];

                    var selectedEmp = activeEmployees.FirstOrDefault(e => e.Id == selectedId);
                    if (selectedEmp != null)
                    {
                        return new EmployeeRecommendation
                        {
                            RecommendedEmployee = selectedEmp,
                            Reason = reason
                        };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("AI JSON Parse Hatası: " + ex.Message);
                }
            }

            // FALLBACK (Yedek Plan): AI başarısız olursa matematiksel hesap yap
            var fallback = activeEmployees
                .OrderByDescending(e => task.SkillsRequired != null && e.Skills != null && e.Skills.Contains(task.SkillsRequired)) // Yetenek var mı?
                .ThenBy(e => e.WorkloadPercentage) // Sonra iş yükü az olan
                .FirstOrDefault();

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
            }).Take(15).ToList(); // Token tasarrufu için max 15 görev

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
            var tasks = await _databaseService.GetTasksAsync(employeeId);
            var emp = (await _databaseService.GetEmployeesAsync()).FirstOrDefault(e => e.Id == employeeId);

            if (!tasks.Any()) return $"Sayın {emp?.FullName}, şu an üzerinizde bekleyen görev bulunmuyor. İyi çalışmalar!";

            string userPrompt = $@"
                KULLANICI: {emp?.FullName}
                GÖREVLERİ: {string.Join(", ", tasks.Where(t => t.Status != TaskStatusEnum.Completed).Select(t => $"{t.Title} ({t.Priority})"))}
                
                Bu kullanıcıya sabah brifingi ver. Motive edici ol, acil işleri vurgula. (Maksimum 3 cümle)";

            return await CallSingleShotAsync("Sen kişisel bir asistansın.", userPrompt) ?? "Özet oluşturulamadı.";
        }

    }
}