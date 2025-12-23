using System;
using System.Windows.Forms;
using DevExpress.UserSkins;
using DevExpress.LookAndFeel;
using OfisAsistan.Forms;
using OfisAsistan.Services;
using OfisAsistan.Models;

namespace OfisAsistan
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            UserLookAndFeel.Default.SetSkinStyle("Office 2019 Colorful");

            // =========================================================================
            // 1. ADIM: ORTAM DEĞİŞKENLERİNDEN VERİ ÇEKME
            // =========================================================================

            // Not: Bilgisayarında "Sistem Ortam Değişkenleri"nde bu isimlerin birebir aynısı olmalı.

            // Supabase
            string supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
            string supabaseKey = Environment.GetEnvironmentVariable("SupabaseKey"); // Senin kodunda böyleydi, kontrol et

            // Groq AI
            string aiApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            // Eğer ortam değişkeninde URL yoksa varsayılanı kullan
            string aiEndpoint = Environment.GetEnvironmentVariable("GROQ_API_URL") ?? "https://api.groq.com/openai/v1";

            // --- GÜVENLİK KONTROLÜ (Kankam burası hayat kurtarır) ---
            if (string.IsNullOrEmpty(aiApiKey) || aiApiKey.StartsWith("BURAYA"))
            {
                // Eğer key çekilemediyse elle girebileceğin bir alan veya uyarı:
                // Test için buraya elle de yazabilirsin ama ortam değişkeni istiyorsan Visual Studio'yu yeniden başlatman gerekebilir.
                // Şimdilik boşsa uyarı verelim ama devam edelim (uygulama çökmesin)
                MessageBox.Show("DİKKAT: 'GROQ_API_KEY' ortam değişkeni okunamadı!\nAI özellikleri çalışmayabilir.\n\nÇözüm: Visual Studio'yu kapatıp açmayı dene.", "Anahtar Eksik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // =========================================================================
            // 2. SERVİSLERİ BAŞLAT
            // =========================================================================

            // Keyler boş olsa bile servisleri başlatıyoruz (Program çökmesin diye)
            // Servis içinde null kontrolü yapacağız.
            DatabaseService db = new DatabaseService(supabaseUrl ?? "", supabaseKey ?? "");

            // AI Servisine url ve key gönderiliyor
            AIService ai = new AIService(aiApiKey ?? "", aiEndpoint, db);

            NotificationService ns = new NotificationService(db);

            // =========================================================================
            // 3. UYGULAMA BAŞLAT
            // =========================================================================

            LoginForm loginForm = new LoginForm(db);

            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                User currentUser = loginForm.LoggedInUser;

                if (currentUser != null)
                {
                    if (currentUser.Role == UserRole.Manager)
                        Application.Run(new ManagerDashboard(db, ai, ns));
                    else
                        Application.Run(new EmployeeWorkspace(db, ai, currentUser.EmployeeId));
                }
            }
            else
            {
                Application.Exit();
            }
        }
    }
}