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
            // Timeout süresini artırarak uzun süren işlemlerde hemen hata almayı engelliyoruz
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

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

            // API'ye tüm geçmişi gönder - ConfigureAwait(false) ile UI thread'i bloklamayı önle
            string aiResponse = await SendRequestToAIAsync(_chatHistory).ConfigureAwait(false);

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

                    // HttpClient header ayarları (Thread-safe olmayabilir, dikkatli olunmalı ama burada tek akış var)
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                    // PostAsync çağrısında ConfigureAwait(false) kullanarak donmayı önle
                    var response = await _httpClient.PostAsync(finalUrl, content).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine($"AI API Hatası ({response.StatusCode}): {err}");

                        // Eğer 429 (Too Many Requests) veya 5xx hatasıysa bekle ve tekrar dene
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                            delay *= 2; // Bekleme süresini katla (Exponential Backoff)
                            continue;
                        }
                        return null;
                    }

                    var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
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
            return await SendRequestToAIAsync(messages, forceJson).ConfigureAwait(false);
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

        // --- 4. GELİŞMİŞ PERSONEL ÖNERİSİ (DETAYLI ANALİZ) ---
        public async Task<EmployeeRecommendation> RecommendEmployeeForTaskAsync(AppTask task)
        {
            // ConfigureAwait(false) ile UI thread'den bağımsız çalış
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

            // Veritabanı çağrısını da ConfigureAwait(false) ile yap
            var employees = await _databaseService.GetEmployeesAsync().ConfigureAwait(false);
            var activeEmployees = employees?.Where(e => e != null && e.IsActive).ToList();

            if (activeEmployees == null || !activeEmployees.Any()) return null;

            // DETAYLI VERİ HAZIRLIĞI
            var empList = string.Join("\n", activeEmployees.Select(e =>
            {
                // Buradaki GetTasksAsync çağrısını da asenkron yapabilmek için Task.Run içinde veya 
                // Result kullanmadan (ki deadlock riski var) çağırmak lazım ama LINQ içinde async zordur.
                // Basitlik için ve DB Service hızlı ise .Result kullanılabilir ama dikkatli olunmalı.
                // En iyisi önceden verileri çekmek.

                // Güvenli yöntem: Senkronize çalışan bir metod varsa onu kullanın yoksa 
                // bu yapı küçük veride sorun yaratmaz ama büyük veride yavaşlatır.
                var tasks = _databaseService.GetTasksAsync(e.Id).Result;
                var activeTaskCount = tasks?.Count(t => t.Status != TaskStatusEnum.Completed) ?? 0;
                var avgCompletionTime = tasks?.Where(t => t.CompletedDate.HasValue)
                    .Select(t => (t.CompletedDate.Value - t.CreatedDate).TotalDays)
                    .DefaultIfEmpty(0).Average() ?? 0;

                return $"- ID:{e.Id}, İsim:{e.FullName}, Departman:{e.DepartmentId}, " +
                       $"Yetenekler:[{e.Skills}], İşYükü:%{e.WorkloadPercentage}, " +
                       $"AktifGörev:{activeTaskCount}, OrtTamamlanmaSüresi:{avgCompletionTime:F1} gün, " +
                       $"Pozisyon:{e.Position}";
            }));

            // Görev detayları
            var taskDetails = $"Başlık: {task.Title}\n";
            if (!string.IsNullOrEmpty(task.Description))
                taskDetails += $"Açıklama: {task.Description}\n";
            if (task.DueDate.HasValue)
                taskDetails += $"Teslim Tarihi: {task.DueDate.Value:dd.MM.yyyy}\n";
            if (task.EstimatedHours > 0)
                taskDetails += $"Tahmini Süre: {task.EstimatedHours} saat\n";
            taskDetails += $"Öncelik: {task.Priority}\n";
            taskDetails += $"Durum: {task.Status}";

            string systemPrompt = @"Sen uzman bir İnsan Kaynakları ve Proje Yönetimi danışmanısın. Görev için en uygun personeli seçerken şu kriterleri DETAYLI analiz et:

1. YETENEK UYUMU (Ağırlık: %40)
   - Görev için gereken yeteneklerle personelin yeteneklerinin eşleşme oranı
   - İlgili deneyim ve geçmiş projeler
   - Teknik yeterlilik seviyesi

2. İŞ YÜKÜ DENGESİ (Ağırlık: %30)
   - Mevcut iş yükü yüzdesi
   - Aktif görev sayısı
   - Ortalama tamamlanma süresi
   - Aşırı yüklü personelden kaçın

3. DEPARTMAN UYUMU (Ağırlık: %15)
   - Departman uygunluğu
   - Takım içi işbirliği potansiyeli

4. PERFORMANS VE GÜVENİLİRLİK (Ağırlık: %15)
   - Geçmiş performans metrikleri
   - Görev tamamlama oranı
   - Zamanında teslim geçmişi

Her aday için 0-100 arası skor ver ve EN İYİ 3 adayı listele.";

            string userPrompt = $@"
GÖREV DETAYLARI:
{taskDetails}

GEREKEN YETENEKLER: {task.SkillsRequired ?? "Belirtilmemiş"}
DEPARTMAN ID: {task.DepartmentId?.ToString() ?? "Belirtilmemiş"}

ADAY PERSONEL LİSTESİ:
{empList}

BUGÜNÜN TARİHİ: {DateTime.Now:dd.MM.yyyy}

Lütfen her adayı detaylı analiz et ve sonucu aşağıdaki JSON formatında ver:
{{
    ""recommendations"": [
        {{
            ""EmployeeId"": 123,
            ""Score"": 85.5,
            ""Reason"": ""Detaylı analiz: Yetenek uyumu %90, iş yükü %45 (ideal), departman uyumu mükemmel. Geçmiş projelerde benzer görevlerde başarılı olmuş."",
            ""SkillMatch"": 90,
            ""WorkloadScore"": 85,
            ""DepartmentMatch"": 100,
            ""PerformanceScore"": 80
        }},
        {{
            ""EmployeeId"": 124,
            ""Score"": 72.3,
            ""Reason"": ""İyi alternatif: Yetenekler uyumlu ancak iş yükü %65 (yüksek). Yine de görevi üstlenebilir."",
            ""SkillMatch"": 75,
            ""WorkloadScore"": 60,
            ""DepartmentMatch"": 90,
            ""PerformanceScore"": 75
        }}
    ]
}}";

            var aiResponse = await CallSingleShotAsync(systemPrompt, userPrompt, true).ConfigureAwait(false);

            // Yapay Zeka Cevabını İşle (DETAYLI)
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
                        var recommendations = obj["recommendations"] as JArray;

                        if (recommendations != null && recommendations.Count > 0)
                        {
                            // En yüksek skorlu adayı al
                            var topRecommendation = recommendations
                                .OrderByDescending(r => r["Score"]?.Value<double>() ?? 0)
                                .FirstOrDefault();

                            if (topRecommendation != null)
                            {
                                int selectedId = topRecommendation["EmployeeId"]?.Value<int>() ?? 0;
                                double score = topRecommendation["Score"]?.Value<double>() ?? 0;
                                string reason = topRecommendation["Reason"]?.Value<string>() ?? "Neden belirtilmedi.";

                                var selectedEmp = activeEmployees.FirstOrDefault(e => e != null && e.Id == selectedId);
                                if (selectedEmp != null)
                                {
                                    // Alternatif adayları da al (2. ve 3. sıradakiler)
                                    var alternatives = new List<Employee>();
                                    for (int i = 1; i < Math.Min(3, recommendations.Count); i++)
                                    {
                                        var alt = recommendations[i];
                                        int altId = alt["EmployeeId"]?.Value<int>() ?? 0;
                                        var altEmp = activeEmployees.FirstOrDefault(e => e != null && e.Id == altId);
                                        if (altEmp != null) alternatives.Add(altEmp);
                                    }

                                    return new EmployeeRecommendation
                                    {
                                        RecommendedEmployee = selectedEmp,
                                        Score = score,
                                        Reason = $"🎯 Uygunluk Skoru: %{score:F1}\n\n" + reason,
                                        AlternativeEmployees = alternatives
                                    };
                                }
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
                .OrderByDescending(e => {
                    if (string.IsNullOrEmpty(task.SkillsRequired) || string.IsNullOrEmpty(e.Skills))
                        return false;
                    // Skills JSON array string olabilir, basit string karşılaştırması yap
                    return e.Skills.IndexOf(task.SkillsRequired, StringComparison.OrdinalIgnoreCase) >= 0;
                }) // Yetenek var mı?
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

        // --- 5. DETAYLI ANOMALİ TESPİTİ (GELİŞTİRİLMİŞ) ---
        public async Task<List<AnomalyDetection>> DetectAnomaliesAsync()
        {
            var anomalies = new List<AnomalyDetection>();
            // ConfigureAwait(false) ekleyerek UI donmasını önle
            var tasks = await _databaseService.GetTasksAsync().ConfigureAwait(false);
            var employees = await _databaseService.GetEmployeesAsync().ConfigureAwait(false);

            // Tamamlanmamış görevleri al
            var activeTasks = tasks.Where(t => t.Status != TaskStatusEnum.Completed).ToList();
            if (!activeTasks.Any()) return anomalies;

            // DETAYLI VERİ SETİ HAZIRLIĞI
            var analysisData = activeTasks.Select(t =>
            {
                var emp = employees.FirstOrDefault(e => e.Id == t.AssignedToId);
                var daysOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.Now
                    ? (DateTime.Now - t.DueDate.Value).Days
                    : 0;
                var daysUntilDue = t.DueDate.HasValue && t.DueDate.Value > DateTime.Now
                    ? (t.DueDate.Value - DateTime.Now).Days
                    : -1;
                var daysInProgress = (DateTime.Now - t.CreatedDate).Days;

                return new
                {
                    t.Id,
                    t.Title,
                    Description = t.Description != null ? t.Description.Substring(0, Math.Min(100, t.Description.Length)) : "",
                    DueDate = t.DueDate?.ToString("yyyy-MM-dd"),
                    CreatedDate = t.CreatedDate.ToString("yyyy-MM-dd"),
                    Priority = t.Priority.ToString(),
                    Status = t.Status.ToString(),
                    AssignedPerson = emp?.FullName ?? "Atanmamış",
                    AssignedPersonWorkload = emp?.WorkloadPercentage ?? 0,
                    AssignedPersonActiveTasks = tasks.Count(ta => ta.AssignedToId == t.AssignedToId && ta.Status != TaskStatusEnum.Completed),
                    EstimatedHours = t.EstimatedHours.HasValue ? t.EstimatedHours.Value : 0,
                    DaysOverdue = daysOverdue,
                    DaysUntilDue = daysUntilDue,
                    DaysInProgress = daysInProgress,
                    DepartmentId = t.DepartmentId?.ToString() ?? "Belirtilmemiş"
                };
            }).Take(Constants.AI_MAX_TASKS_FOR_ANALYSIS).ToList();

            string systemPrompt = @"Sen deneyimli bir Proje Denetçisi ve Risk Analisti'sin. Projedeki riskleri, mantıksız atamaları, gecikmeleri ve potansiyel sorunları DETAYLI analiz et.

ANOMALİ TİPLERİ:
1. OVERDUE (Gecikmiş): Tarihi geçmiş görevler
2. WORKLOAD_OVERLOAD (Aşırı Yük): İş yükü %80+ kişiye yeni görev atanması
3. STUCK_TASK (Takılı Görev): Uzun süredir ilerlemeyen görevler (30+ gün)
4. QUALITY_ISSUE (Kalite Sorunu): Yüksek öncelikli ama atanmamış görevler
5. RESOURCE_MISMATCH (Kaynak Uyumsuzluğu): Yetenek uyumsuzluğu olan atamalar

SEVERITY SEVİYELERİ:
- Critical: Acil müdahale gerektiren, projeyi durdurabilecek sorunlar
- High: Önemli riskler, hızlıca ele alınmalı
- Medium: Orta seviye riskler, takip edilmeli
- Low: Düşük öncelikli, bilgilendirme amaçlı";

            string userPrompt = $@"
BUGÜNÜN TARİHİ: {DateTime.Now:yyyy-MM-dd HH:mm}

GÖREV VERİLERİ (DETAYLI):
{JsonConvert.SerializeObject(analysisData, Formatting.Indented)}

Lütfen her görevi detaylı analiz et ve tespit ettiğin anomalileri aşağıdaki JSON formatında ver:
{{
    ""anomalies"": [
        {{
            ""TaskId"": 1,
            ""Type"": ""OVERDUE"",
            ""Severity"": ""Critical"",
            ""Message"": ""Detaylı açıklama: Bu görev 5 gün önce gecikmiş. Yüksek öncelikli ve kritik. Acil müdahale gerekiyor. Etkilenen departman: IT. Önerilen aksiyon: Görev sahibiyle acil görüşme yapılmalı."",
            ""Impact"": ""Proje zaman çizelgesini etkileyebilir"",
            ""RecommendedAction"": ""Görev sahibiyle acil görüşme, kaynak artırımı düşünülebilir""
        }}
    ]
}}";

            var response = await CallSingleShotAsync(systemPrompt, userPrompt, true).ConfigureAwait(false);

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
                            int tId = item["TaskId"]?.Value<int>() ?? 0;
                            var originalTask = tasks.FirstOrDefault(t => t.Id == tId);
                            if (originalTask != null)
                            {
                                string sevStr = item["Severity"]?.Value<string>() ?? "Medium";
                                var severity = sevStr.IndexOf("Critical", StringComparison.OrdinalIgnoreCase) >= 0 ? AnomalySeverity.Critical :
                                               sevStr.StartsWith("H", StringComparison.OrdinalIgnoreCase) ? AnomalySeverity.High :
                                               sevStr.StartsWith("M", StringComparison.OrdinalIgnoreCase) ? AnomalySeverity.Medium : AnomalySeverity.Low;

                                string typeStr = item["Type"]?.Value<string>() ?? "StuckTask";
                                var type = Enum.TryParse<AnomalyType>(typeStr, out var parsedType) ? parsedType : AnomalyType.StuckTask;

                                string message = item["Message"]?.Value<string>() ?? "Anomali tespit edildi.";
                                string impact = item["Impact"]?.Value<string>() ?? "";
                                string recommendedAction = item["RecommendedAction"]?.Value<string>() ?? "";

                                // Detaylı mesaj oluştur
                                if (!string.IsNullOrEmpty(impact))
                                    message += $"\n\n📊 Etki: {impact}";
                                if (!string.IsNullOrEmpty(recommendedAction))
                                    message += $"\n\n💡 Önerilen Aksiyon: {recommendedAction}";

                                anomalies.Add(new AnomalyDetection
                                {
                                    Task = originalTask,
                                    Type = type,
                                    Message = message,
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

        // --- 6. AKILLI GÖREV BÖLÜCÜ (DETAYLI TASK BREAKDOWN) ---
        public async Task<List<SubTask>> BreakDownTaskAsync(string taskDescription)
        {
            // Görev açıklaması boşsa erken dön
            if (string.IsNullOrWhiteSpace(taskDescription))
            {
                System.Diagnostics.Debug.WriteLine("BreakDownTaskAsync: Görev açıklaması boş!");
                return new List<SubTask>();
            }

            System.Diagnostics.Debug.WriteLine($"BreakDownTaskAsync çağrıldı: {taskDescription.Substring(0, Math.Min(100, taskDescription.Length))}...");

            string systemPrompt = @"Sen deneyimli bir Proje Yöneticisi ve İş Analisti'sin. Verilen ana görevi mantıklı, yapılabilir ve ölçülebilir alt görevlere böl.

Her alt görev için:
- Spesifik ve net bir başlık
- Detaylı açıklama (ne yapılacak, nasıl yapılacak)
- Gerçekçi tahmini süre (saat cinsinden)
- Öncelik sırası (hangi adım önce gelmeli)
- Bağımlılıklar (hangi adımlar birbirine bağlı)

Adımlar mantıklı bir sırayla, bağımlılıkları göz önünde bulundurarak düzenlenmeli.";

            string userPrompt = $@"
GÖREV TANIMI: {taskDescription}

BUGÜNÜN TARİHİ: {DateTime.Now:dd.MM.yyyy HH:mm}

Bu görevi 5-10 arası detaylı alt adıma ayır. Her adım için:
- Başlık (kısa ve net)
- Açıklama (ne yapılacak, nasıl yapılacak - 1-2 cümle)
- Tahmini süre (saat)
- Sıra numarası (hangi sırada yapılmalı)

JSON Formatı:
{{
    ""steps"": [
        {{
            ""Title"": ""Gereksinim analizi yap"",
            ""Description"": ""Müşteri gereksinimlerini topla, analiz et ve dokümante et. Paydaşlarla görüşmeler yap."",
            ""Hours"": 4,
            ""Order"": 1
        }},
        {{
            ""Title"": ""Teknik tasarım dokümantasyonu"",
            ""Description"": ""Sistem mimarisi ve teknik tasarım dokümantasyonunu hazırla. Veritabanı şemasını çıkar."",
            ""Hours"": 6,
            ""Order"": 2
        }}
    ],
    ""totalEstimatedHours"": 10,
    ""complexity"": ""Medium"",
    ""recommendedApproach"": ""Bu görev için önerilen yaklaşım: Önce gereksinimleri netleştir, sonra teknik tasarım yap, ardından geliştirmeye başla.""
}}";

            var response = await CallSingleShotWithTempAsync(systemPrompt, userPrompt, 0.7).ConfigureAwait(false); // Orta temperature
            var resultList = new List<SubTask>();

            if (!string.IsNullOrEmpty(response))
            {
                try
                {
                    var obj = JObject.Parse(ExtractJson(response));
                    if (obj["steps"] != null)
                    {
                        foreach (var s in obj["steps"])
                        {
                            resultList.Add(new SubTask
                            {
                                Title = (string)s["Title"] ?? "İsimsiz Adım",
                                Description = (string)s["Description"] ?? "",
                                EstimatedHours = s["Hours"]?.Value<int>() ?? 2,
                                Order = s["Order"]?.Value<int>() ?? (resultList.Count + 1)
                            });
                        }

                        // Sıraya göre sırala
                        resultList = resultList.OrderBy(st => st.Order).ToList();
                    }
                    System.Diagnostics.Debug.WriteLine($"BreakDownTaskAsync: {resultList.Count} alt görev oluşturuldu.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"BreakDownTaskAsync Parse Hatası: {ex.Message}");
                }
            }
            return resultList;
        }

        // Temperature parametreli özel metod
        private async Task<string> CallSingleShotWithTempAsync(string systemPrompt, string userPrompt, double temperature)
        {
            var messages = new[]
            {
                new { role = "system", content = systemPrompt + " Yanıtı SADECE geçerli bir JSON formatında ver. Başka açıklama yapma." },
                new { role = "user", content = userPrompt }
            };
            return await SendRequestToAIWithTempAsync(messages, temperature).ConfigureAwait(false);
        }

        // Temperature destekli istek metodu
        private async Task<string> SendRequestToAIWithTempAsync(object messages, double temperature)
        {
            int maxRetries = Constants.AI_MAX_RETRIES;
            int delay = Constants.AI_INITIAL_DELAY_MS;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    string finalUrl = _baseApiUrl;
                    if (finalUrl.Contains("groq.com"))
                        finalUrl = "[https://api.groq.com/openai/v1/chat/completions](https://api.groq.com/openai/v1/chat/completions)";
                    else if (!finalUrl.EndsWith("/chat/completions"))
                        finalUrl += "/v1/chat/completions";

                    var requestBody = new
                    {
                        model = "llama-3.3-70b-versatile",
                        messages = messages,
                        temperature = temperature, // Dinamik temperature
                        response_format = new { type = "json_object" }
                    };

                    var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                    var response = await _httpClient.PostAsync(finalUrl, content).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine($"AI API Hatası ({response.StatusCode}): {err}");
                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                            delay *= 2;
                            continue;
                        }
                        return null;
                    }

                    var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(responseJson)) return null;

                    dynamic result = JsonConvert.DeserializeObject(responseJson);
                    return result?.choices?[0]?.message?.content?.ToString();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AI Bağlantı Hatası (Deneme {i + 1}): {ex.Message}");
                    if (i == maxRetries - 1) return null;
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
            }
            return null;
        }

        // --- 7. GÜNLÜK ÖZET (Smart Briefing) ---
        public async Task<string> GenerateDailyBriefingAsync(int employeeId)
        {
            try
            {
                // ConfigureAwait(false) ile UI thread'i kilitlemeden çağır
                var tasks = await _databaseService.GetTasksAsync(employeeId).ConfigureAwait(false);
                var employees = await _databaseService.GetEmployeesAsync().ConfigureAwait(false);
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

                string systemPrompt = @"Sen profesyonel, analitik ve motive edici bir ofis asistanısın. Kullanıcıya DETAYLI günlük brifing verirken:

1. KİŞİSELLEŞTİRİLMİŞ SELAMLAMA
   - Kullanıcının adını kullan
   - Bugünün tarihini belirt
   - Genel durum özeti ver

2. GÖREV ANALİZİ (DETAYLI)
   - Toplam aktif görev sayısı
   - Gecikmiş görevler (varsa detaylı listele)
   - Bugün teslim tarihi olan görevler
   - Yüksek öncelikli görevler
   - Her kategori için sayı ve örnekler ver

3. ÖNCELİKLENDİRME ÖNERİLERİ
   - Hangi görevlere öncelik verilmeli
   - Neden öncelikli oldukları
   - Tahmini süre gereksinimleri

4. MOTİVASYON VE YÖNLENDİRME
   - Pozitif ve motive edici dil
   - Başarıları vurgula (varsa)
   - Bugün için hedefler öner
   - İpuçları ve öneriler

5. FORMAT
   - Türkçe yaz
   - Emoji kullan (ölçülü)
   - Paragraflar halinde düzenle
   - Okunabilir ve anlaşılır ol";

                // DETAYLI GÖREV ANALİZİ
                var taskDetails = new StringBuilder();
                taskDetails.AppendLine($"📊 GÖREV ANALİZİ - {DateTime.Now:dd.MM.yyyy}");
                taskDetails.AppendLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                taskDetails.AppendLine($"📋 Toplam Aktif Görev: {activeTasks.Count}");

                if (overdueTasks.Any())
                {
                    taskDetails.AppendLine($"\n⚠️ GECİKMİŞ GÖREVLER ({overdueTasks.Count}):");
                    foreach (var t in overdueTasks.Take(5))
                    {
                        var daysOverdue = (DateTime.Now - t.DueDate.Value).Days;
                        taskDetails.AppendLine($"   • {t.Title} ({daysOverdue} gün gecikmiş, Öncelik: {t.Priority})");
                    }
                }

                if (todayTasks.Any())
                {
                    taskDetails.AppendLine($"\n📅 BUGÜN TESLİM TARİHİ ({todayTasks.Count}):");
                    foreach (var t in todayTasks.Take(5))
                    {
                        taskDetails.AppendLine($"   • {t.Title} (Öncelik: {t.Priority}, Tahmini: {(t.EstimatedHours.HasValue ? t.EstimatedHours.Value : 0)} saat)");
                    }
                }

                if (highPriorityTasks.Any())
                {
                    taskDetails.AppendLine($"\n🔥 YÜKSEK ÖNCELİK ({highPriorityTasks.Count}):");
                    foreach (var t in highPriorityTasks.Take(5))
                    {
                        taskDetails.AppendLine($"   • {t.Title} (Durum: {t.Status}, Teslim: {t.DueDate?.ToString("dd.MM.yyyy") ?? "Belirtilmemiş"})");
                    }
                }

                // İstatistikler
                var avgEstimatedHours = activeTasks.Where(t => t.EstimatedHours.HasValue).Average(t => t.EstimatedHours.Value);
                var totalEstimatedHours = activeTasks.Where(t => t.EstimatedHours.HasValue).Sum(t => t.EstimatedHours.Value);
                taskDetails.AppendLine($"\n📈 İSTATİSTİKLER:");
                taskDetails.AppendLine($"   • Ortalama Görev Süresi: {avgEstimatedHours:F1} saat");
                taskDetails.AppendLine($"   • Toplam Tahmini Süre: {totalEstimatedHours} saat");
                taskDetails.AppendLine($"   • Bekleyen: {activeTasks.Count(t => t.Status == TaskStatusEnum.Pending)}");
                taskDetails.AppendLine($"   • Devam Eden: {activeTasks.Count(t => t.Status == TaskStatusEnum.InProgress)}");

                string userPrompt = $@"
KULLANICI: {emp?.FullName ?? "Çalışan"}
BUGÜNÜN TARİHİ: {DateTime.Now:dd.MM.yyyy dddd}

{taskDetails.ToString()}

Lütfen bu kullanıcıya profesyonel, detaylı, motive edici ve kişiselleştirilmiş bir günlük brifing ver. Yukarıdaki tüm bilgileri kullanarak kapsamlı bir analiz yap. Gecikmiş görevler varsa bunları özellikle vurgula ve öneriler sun.";

                var response = await CallSingleShotAsync(systemPrompt, userPrompt).ConfigureAwait(false);

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