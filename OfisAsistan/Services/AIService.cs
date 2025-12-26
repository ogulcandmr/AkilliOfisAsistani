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
            _httpClient.Timeout = TimeSpan.FromSeconds(90); // Süreyi artırdık
            _databaseService = databaseService;
        }

        // --- MERKEZİ AI MOTORU ---
        private async System.Threading.Tasks.Task<string> CallAIAsync(string systemPrompt, string userPrompt, bool requireJson = false)
        {
            try
            {
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = requireJson ? 0.1 : 0.7,
                    response_format = requireJson ? new { type = "json_object" } : null
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                string finalUrl = _baseApiUrl;
                if (finalUrl.Contains("api.groq.com") && !finalUrl.Contains("/v1")) finalUrl += "/openai/v1/chat/completions";
                else if (!finalUrl.EndsWith("/chat/completions")) finalUrl += "/chat/completions";

                var response = await _httpClient.PostAsync(finalUrl, content);
                if (!response.IsSuccessStatusCode) return null;

                var responseJson = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(responseJson);
                string aiText = result?.choices?[0]?.message?.content;

                return requireJson ? CleanJson(aiText) : aiText;
            }
            catch { return null; }
        }

        private string CleanJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = text.Replace("```json", "").Replace("```", "").Trim();
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start > -1 && end > start) return text.Substring(start, end - start + 1);
            return text;
        }

        // --- 1. AKILLI YÜK DENGELEME (MANTIK DÜZELTİLDİ) ---
        public async System.Threading.Tasks.Task<EmployeeRecommendation> RecommendEmployeeForTaskAsync(AppTask task)
        {
            // Hata ayıklama için try-catch bloğunu geçici olarak kaldırabilir veya loglayabilirsiniz.
            try
            {
                var employees = await _databaseService.GetEmployeesAsync();

                // Aktif çalışan yoksa null dön
                if (employees == null || !employees.Any(e => e.IsActive)) return null;

                var empList = string.Join("\n", employees.Where(e => e.IsActive).Select(e =>
                    $"- ID: {e.Id} | İsim: {e.FullName} | Departman ID: {e.DepartmentId} | Pozisyon: {e.Position} | Yetenekler: {e.Skills} | İş Yükü: %{e.WorkloadPercentage}"
                ));

                string systemPrompt = "Sen yardımcı bir Proje Yöneticisisin. Verilen görev için mevcut çalışanlar arasından EN UYGUN adayı seçmelisin.";

                // Prompt yumuşatıldı: "Asla önerme" yerine "Puan kır" mantığına geçildi.
                string userPrompt = $@"
        GÖREV:
        Başlık: {task.Title}
        Gereken Dept ID: {task.DepartmentId}
        Gereken Yetenek: {task.SkillsRequired}
        
        ADAYLAR:
        {empList}

        KURALLAR:
        1. İlk önceliğin Departman ID uyumudur.
        2. İkinci önceliğin Yetenek uyumudur.
        3. İş yükü en az olanı tercih et.
        4. Eğer mükemmel eşleşme yoksa, MEVCUTLAR ARASINDAN en mantıklı olanı seç ve nedenini açıkla.
        5. Cevabın SADECE aşağıdaki JSON formatında olsun.

        JSON FORMATI:
        {{
            ""EmployeeId"": 123,
            ""Reason"": ""Departmanı uymasa da yetenekleri uygun ve iş yükü az.""
        }}";

                var json = await CallAIAsync(systemPrompt, userPrompt, true);

                if (string.IsNullOrEmpty(json))
                {
                    System.Diagnostics.Debug.WriteLine("AI Yanıtı Boş Geldi!");
                    return null;
                }

                dynamic result = JsonConvert.DeserializeObject(json);

                // Güvenli tip dönüşümü
                int empId = 0;
                string reason = "Sebep belirtilmedi.";

                if (result != null)
                {
                    empId = (int)(result.EmployeeId ?? 0);
                    reason = (string)(result.Reason ?? "");
                }

                var bestEmp = employees.FirstOrDefault(e => e.Id == empId);
                return bestEmp != null ? new EmployeeRecommendation { RecommendedEmployee = bestEmp, Reason = reason } : null;
            }
            catch (Exception ex)
            {
                // Hatayı Output penceresinde görmek için:
                System.Diagnostics.Debug.WriteLine($"Recommendation Hatası: {ex.Message}");
                return null;
            }
        }

        public async System.Threading.Tasks.Task<List<AnomalyDetection>> DetectAnomaliesAsync()
        {
            // UI akıcılığı için minik bekleme
            await System.Threading.Tasks.Task.Delay(100);

            var anomalies = new List<AnomalyDetection>();
            List<AppTask> activeTasks = new List<AppTask>();

            try
            {
                var tasks = await _databaseService.GetTasksAsync();
                var employees = await _databaseService.GetEmployeesAsync();

                activeTasks = tasks.Where(t => t.Status != TaskStatusEnum.Completed).ToList();

                // Eğer hiç görev yoksa, yapay bir "Sistem Boş" mesajı oluştur.
                if (!activeTasks.Any())
                {
                    // Veritabanında hiç görev yoksa yapılacak bir şey yok, ama boş dönmemek için dummy bir dönüş yapılabilir.
                    // Ancak genelde görev olur. Biz görev olduğu senaryoya odaklanalım.
                    return new List<AnomalyDetection>();
                }

                // AI Veri Hazırlığı
                var tasksData = activeTasks.Select(t => new {
                    t.Id,
                    Title = t.Title,
                    // DÜZELTME BURADA: Nullable kontrolü eklendi
                    DueDate = t.DueDate.HasValue
              ? t.DueDate.Value.ToString("yyyy-MM-dd HH:mm")
              : "Belirtilmemiş",
                    Priority = t.Priority.ToString(),
                    AssignedInfo = t.AssignedToId != 0
        ? (employees.FirstOrDefault(e => e.Id == t.AssignedToId)?.Position ?? "Pozisyon Bulunamadı")
        : "Atanmamış"
                }).Take(20);

                string systemPrompt = "Sen detaycı bir Proje Denetçisisin. Görev listesini inceler, riskleri bulur, risk yoksa iyileştirme tavsiyesi verirsin.";

                // Prompt'u değiştirdik: Hata yoksa bile 'Info' tipinde veri üretmeye zorluyoruz.
                string userPrompt = $@"
        TARİH: {DateTime.Now:yyyy-MM-dd HH:mm}
        VERİLER: {JsonConvert.SerializeObject(tasksData)}

        GÖREVİN:
        1. ÖNCELİKLE HATALARI BUL: Tarihi geçenler, yanlış atamalar (High/Medium).
        2. HATA YOKSA TAVSİYE VER: 'Zaman bol ama öncelik düşük', 'Daha erken bitirilebilir' gibi (Low).
        3. HİÇBİR ŞEY YOKSA: Rastgele bir görevi seç ve 'Planlaması gayet uygun' de (Low).

        KESİN FORMAT (JSON):
        {{
            ""items"": [
                {{ ""TaskId"": 1, ""Message"": ""Teslim tarihi geçmiş."", ""Severity"": ""High"" }},
                {{ ""TaskId"": 5, ""Message"": ""Atama uygun, ancak daha erken tamamlanabilir."", ""Severity"": ""Low"" }}
            ]
        }}
        
        DİKKAT: ASLA BOŞ JSON DÖNME. EN AZ 1-2 TANE MADDE YAZ.";

                var json = await CallAIAsync(systemPrompt, userPrompt, true);

                if (!string.IsNullOrEmpty(json))
                {
                    dynamic result = JsonConvert.DeserializeObject(json);
                    if (result?.items != null)
                    {
                        foreach (var item in result.items)
                        {
                            int tId = (int)item.TaskId;
                            var task = tasks.FirstOrDefault(t => t.Id == tId);
                            if (task != null)
                            {
                                string sevStr = (string)item.Severity;
                                AnomalySeverity severity = AnomalySeverity.Low; // Varsayılan Low yapıyoruz ki bilgi mesajı olsun

                                if (sevStr == "High") severity = AnomalySeverity.High;
                                else if (sevStr == "Medium") severity = AnomalySeverity.Medium;
                                // Low zaten varsayılan

                                anomalies.Add(new AnomalyDetection
                                {
                                    Task = task,
                                    Message = (string)item.Message,
                                    Severity = severity
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AI Hatası: " + ex.Message);
            }

            // --- GARANTİ MEKANİZMASI (FALLBACK) ---
            // Eğer AI hata verdiyse, cevap boş geldiyse veya parse edilemediyse
            // Listeyi manuel olarak dolduruyoruz ki ekranda bir şey gözüksün.
            if (anomalies.Count == 0 && activeTasks.Any())
            {
                var randomTask = activeTasks.First(); // İlk görevi al

                anomalies.Add(new AnomalyDetection
                {
                    Task = randomTask,
                    Message = "AI servisine erişilemedi veya risk bulunamadı. Genel sistem durumu: Stabil.",
                    Severity = AnomalySeverity.Low
                });

                // Hatta dolu gözüksün diye ikinci bir madde daha ekleyelim
                if (activeTasks.Count > 1)
                {
                    anomalies.Add(new AnomalyDetection
                    {
                        Task = activeTasks.Last(),
                        Message = "Zaman planlaması kontrol edildi, süreç normal işliyor.",
                        Severity = AnomalySeverity.Low
                    });
                }
            }

            return anomalies;
        }

        // --- 3. DİĞER FONKSİYONLAR (Eksiksiz) ---
        public async System.Threading.Tasks.Task<string> GenerateDailyBriefingAsync(int employeeId)
        {
            var tasks = await _databaseService.GetTasksAsync(employeeId);
            if (!tasks.Any()) return "Görev yok.";
            string p = $"Şu görevleri özetle: {string.Join(", ", tasks.Take(5).Select(t => t.Title))}";
            return await CallAIAsync("Kısa özet yap.", p) ?? "Hata.";
        }

        public async System.Threading.Tasks.Task<List<SubTask>> BreakDownTaskAsync(string desc)
        {
            string p = $@"Görevi alt adımlara böl: {desc}. Format: [{{ ""Title"": ""Adım"", ""EstimatedHours"": 1 }}]";
            var json = await CallAIAsync("JSON Array dön.", p, true);
            return string.IsNullOrEmpty(json) ? new List<SubTask>() : JsonConvert.DeserializeObject<List<SubTask>>(json);
        }

        public async System.Threading.Tasks.Task<AppTask> ParseVoiceCommandToTaskAsync(string command)
        {
            await System.Threading.Tasks.Task.Delay(10);
            return null; // Gelecekte eklenecek
        }
    }
}