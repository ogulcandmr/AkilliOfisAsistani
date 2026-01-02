namespace OfisAsistan
{
    /// <summary>
    /// Uygulama genelinde kullanılan sabit değerler
    /// </summary>
    public static class Constants
    {
        // Ortam Değişkeni İsimleri
        public const string ENV_SUPABASE_URL = "SUPABASE_URL";
        public const string ENV_SUPABASE_KEY = "SUPABASE_KEY";
        public const string ENV_GROQ_API_KEY = "GROQ_API_KEY";
        public const string ENV_GROQ_API_URL = "GROQ_API_URL";

        // Varsayılan Değerler
        public const string DEFAULT_GROQ_API_URL = "https://api.groq.com/openai/v1";

        // Timer Ayarları
        public const int NOTIFICATION_CHECK_INTERVAL_MS = 60000; // 1 dakika

        // İş Yükü Threshold'ları
        public const double WORKLOAD_OVERLOAD_THRESHOLD = 70.0; // %70 üzeri aşırı yüklü
        public const double WORKLOAD_AVAILABLE_THRESHOLD = 50.0; // %50 altı müsait

        // Bildirim Zamanları
        public const int DEADLINE_WARNING_HOURS = 2; // 2 saat kala uyarı
        public const int MEETING_REMINDER_MINUTES = 15; // 15 dakika kala toplantı hatırlatması

        // Chat Ayarları
        public const int MAX_CHAT_HISTORY = 50; // Maksimum chat geçmişi sayısı

        // AI Ayarları
        public const int AI_MAX_RETRIES = 3; // AI API için maksimum deneme sayısı
        public const int AI_INITIAL_DELAY_MS = 1000; // İlk bekleme süresi (ms)
        public const int AI_TIMEOUT_SECONDS = 120; // AI API timeout (saniye)
        public const int AI_MAX_TASKS_FOR_ANALYSIS = 15; // Anomali analizi için maksimum görev sayısı

        // UI Ayarları
        public const int MAX_LOG_ITEMS = 50; // Canlı log için maksimum item sayısı
    }
}

