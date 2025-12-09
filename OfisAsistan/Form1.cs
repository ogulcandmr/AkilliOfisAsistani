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

namespace OfisAsistan
{
    public partial class Form1 : Form
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        private VoiceService _voiceService;
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
            // App.config'den ayarları oku
            var supabaseUrl = System.Configuration.ConfigurationManager.AppSettings["SupabaseUrl"] ?? "https://your-project.supabase.co";
            var supabaseKey = System.Configuration.ConfigurationManager.AppSettings["SupabaseKey"] ?? "your-key";
            var openAIApiKey = System.Configuration.ConfigurationManager.AppSettings["OpenAIApiKey"] ?? "your-key";
            var openAIUrl = System.Configuration.ConfigurationManager.AppSettings["OpenAIUrl"] ?? "https://api.openai.com";

            _databaseService = new DatabaseService(supabaseUrl, supabaseKey);
            _aiService = new AIService(openAIApiKey, openAIUrl, _databaseService);
            _voiceService = new VoiceService();
            _notificationService = new NotificationService(_databaseService);
        }

        private void InitializeCustomComponents()
        {
            // Event handlers ekle
            this.mnuManager.Click += MnuManager_Click;
            this.mnuEmployee.Click += MnuEmployee_Click;
            this.mnuVoice.Click += MnuVoice_Click;
            this.mnuExit.Click += MnuExit_Click;

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
            if (_currentUser == null)
            {
                mnuManager.Enabled = false;
                mnuEmployee.Enabled = false;
                mnuVoice.Enabled = false;
                return;
            }

            mnuManager.Enabled = _currentUser.Role == UserRole.Manager || _currentUser.Role == UserRole.Admin;
            mnuEmployee.Enabled = true;
            mnuVoice.Enabled = _currentUser.Role == UserRole.Manager || _currentUser.Role == UserRole.Admin;
        }

        private void MnuManager_Click(object sender, EventArgs e)
        {
            var dashboard = new ManagerDashboard(_databaseService, _aiService, _notificationService);
            dashboard.MdiParent = this;
            dashboard.Show();
        }

        private void MnuEmployee_Click(object sender, EventArgs e)
        {
            var workspace = new EmployeeWorkspace(_databaseService, _aiService, _currentUser.EmployeeId);
            workspace.MdiParent = this;
            workspace.Show();
        }

        private void MnuVoice_Click(object sender, EventArgs e)
        {
            var voiceForm = new VoiceManagerForm(_voiceService, _aiService, _databaseService);
            voiceForm.MdiParent = this;
            voiceForm.Show();
        }

        private void MnuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _voiceService?.Dispose();
            _notificationService?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
