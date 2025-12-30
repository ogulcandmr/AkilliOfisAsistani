using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OfisAsistan.Forms;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraBars;

namespace OfisAsistan
{
    public partial class Form1 : XtraForm
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        // VoiceService kaldırıldı
        private NotificationService _notificationService;
        private User _currentUser;

        public Form1()
        {
            InitializeServices();
            InitializeComponent();
            InitializeCustomComponents();
            ShowLogin();
        }

        private void InitializeServices()
        {
            // Çevresel değişkenleri veya varsayılanları al
            var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? System.Configuration.ConfigurationManager.AppSettings["SupabaseUrl"] ?? "https://your-project.supabase.co";
            var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY") ?? System.Configuration.ConfigurationManager.AppSettings["SupabaseKey"] ?? "your-key";
            var openAIApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? System.Configuration.ConfigurationManager.AppSettings["OpenAIApiKey"] ?? "your-key";
            var openAIUrl = Environment.GetEnvironmentVariable("GROQ_API_URL") ?? System.Configuration.ConfigurationManager.AppSettings["OpenAIUrl"] ?? "https://api.openai.com";

            // Servisleri başlat
            _databaseService = new DatabaseService(supabaseUrl, supabaseKey);
            _aiService = new AIService(openAIApiKey, openAIUrl, _databaseService);

            // VoiceService başlatma kodu kaldırıldı.

            _notificationService = new NotificationService(_databaseService);
        }

        private void InitializeCustomComponents()
        {
            // Menü olaylarını bağla
            if (this.mnuManager != null) this.mnuManager.ItemClick += MnuManager_Click;
            if (this.mnuEmployee != null) this.mnuEmployee.ItemClick += MnuEmployee_Click;
            if (this.mnuExit != null) this.mnuExit.ItemClick += MnuExit_Click;

            // Ses menüsü olay bağlama (Click) kaldırıldı çünkü form silindi.

            UpdateMenuVisibility();
        }

        private void ShowLogin()
        {
            var loginForm = new LoginForm(_databaseService);
            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                _currentUser = loginForm.LoggedInUser;
                UpdateMenuVisibility();
                this.Text = $"Ofis Asistan - Hoş geldiniz, {_currentUser.Username}";
            }
            else
            {
                Application.Exit();
            }
        }

        private void UpdateMenuVisibility()
        {
            // Eğer giriş yapılmadıysa her şeyi kapat
            if (_currentUser == null)
            {
                if (mnuManager != null) mnuManager.Enabled = false;
                if (mnuEmployee != null) mnuEmployee.Enabled = false;
                return;
            }

            // Role göre yetkilendirme
            if (mnuManager != null)
                mnuManager.Enabled = _currentUser.Role == UserRole.Manager || _currentUser.Role == UserRole.Admin;

            if (mnuEmployee != null)
                mnuEmployee.Enabled = true;

            // Ses menüsü varsa gizle (Kullanılmıyor)
            if (mnuVoice != null)
            {
                mnuVoice.Visibility = BarItemVisibility.Never;
            }
        }

        private void MnuManager_Click(object sender, ItemClickEventArgs e)
        {
            var dashboard = new ManagerDashboard(_databaseService, _aiService, _notificationService);
            dashboard.Show();
        }

        private void MnuEmployee_Click(object sender, ItemClickEventArgs e)
        {
            var workspace = new EmployeeWorkspace(_databaseService, _aiService, _currentUser.EmployeeId);
            workspace.Show();
        }

        // MnuVoice_Click Metodu TAMAMEN SİLİNDİ (VoiceManagerForm olmadığı için)

        private void MnuExit_Click(object sender, ItemClickEventArgs e)
        {
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // VoiceService dispose kaldırıldı
            _notificationService?.Dispose();
            base.OnFormClosing(e);
        }
    }
}