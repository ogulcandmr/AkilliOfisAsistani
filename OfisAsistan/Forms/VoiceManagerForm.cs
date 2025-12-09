using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using OfisAsistan.Services;

namespace OfisAsistan.Forms
{
    public partial class VoiceManagerForm : Form
    {
        private VoiceService _voiceService;
        private AIService _aiService;
        private DatabaseService _databaseService;
        private Button btnStartListening;
        private Button btnStopListening;
        private TextBox txtVoiceCommand;
        private TextBox txtResult;
        private Label lblStatus;
        private bool _isListening;

        public VoiceManagerForm(VoiceService voiceService, AIService aiService, DatabaseService databaseService)
        {
            _voiceService = voiceService;
            _aiService = aiService;
            _databaseService = databaseService;
            InitializeComponent();
            SetupVoiceEvents();
        }

        private void InitializeComponent()
        {
            this.Text = "Sesli Yönetici";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Ana panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            // Başlık
            var titleLabel = new Label
            {
                Text = "🎤 Sesli Yönetici Modülü",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Butonlar
            var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            btnStartListening = new Button
            {
                Text = "▶ Dinlemeyi Başlat",
                Size = new Size(150, 40),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            btnStopListening = new Button
            {
                Text = "⏹ Dinlemeyi Durdur",
                Size = new Size(150, 40),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Enabled = false
            };
            buttonsPanel.Controls.Add(btnStartListening);
            buttonsPanel.Controls.Add(btnStopListening);

            // Komut girişi
            var commandLabel = new Label { Text = "Tanınan Komut:", Dock = DockStyle.Fill, Height = 25 };
            txtVoiceCommand = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Arial", 11) };

            // Sonuç
            var resultLabel = new Label { Text = "İşlem Sonucu:", Dock = DockStyle.Fill, Height = 25 };
            txtResult = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Arial", 10) };

            // Durum
            lblStatus = new Label
            {
                Text = "Hazır",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGray,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            mainPanel.Controls.Add(titleLabel, 0, 0);
            mainPanel.Controls.Add(buttonsPanel, 0, 1);
            mainPanel.Controls.Add(commandLabel, 0, 2);
            mainPanel.Controls.Add(txtVoiceCommand, 0, 2);
            mainPanel.Controls.Add(resultLabel, 0, 3);
            mainPanel.Controls.Add(txtResult, 0, 3);
            mainPanel.Controls.Add(lblStatus, 0, 4);

            this.Controls.Add(mainPanel);

            // Event handlers
            btnStartListening.Click += BtnStartListening_Click;
            btnStopListening.Click += BtnStopListening_Click;
        }

        private void SetupVoiceEvents()
        {
            _voiceService.VoiceCommandReceived += VoiceService_VoiceCommandReceived;
        }

        private async void VoiceService_VoiceCommandReceived(object sender, string command)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => VoiceService_VoiceCommandReceived(sender, command)));
                return;
            }

            txtVoiceCommand.Text = command;
            lblStatus.Text = "Komut işleniyor...";
            lblStatus.BackColor = Color.Yellow;

            try
            {
                // Komut analizi
                if (command.ToLower().Contains("görev ata") || command.ToLower().Contains("yeni görev"))
                {
                    // Sesli görev atama
                    var task = await _aiService.ParseVoiceCommandToTaskAsync(command);
                    if (task != null)
                    {
                        var createdTask = await _databaseService.CreateTaskAsync(task);
                        if (createdTask != null)
                        {
                            txtResult.Text = $"✅ Görev oluşturuldu:\nBaşlık: {createdTask.Title}\nAtanan: {createdTask.AssignedToId}\nTeslim: {createdTask.DueDate?.ToString("dd.MM.yyyy") ?? "Belirtilmemiş"}";
                            _voiceService.Speak($"Görev başarıyla oluşturuldu. {createdTask.Title}");
                        }
                        else
                        {
                            txtResult.Text = "❌ Görev oluşturulamadı.";
                            _voiceService.Speak("Görev oluşturulamadı.");
                        }
                    }
                }
                else if (command.ToLower().Contains("rapor") || command.ToLower().Contains("listele") || command.ToLower().Contains("bitmeyen"))
                {
                    // Sesli rapor sorgulama
                    var tasks = await _databaseService.GetTasksAsync();
                    var incompleteTasks = tasks.FindAll(t => t.Status != TaskStatusModel.Completed);
                    
                    var report = $"📊 Rapor:\n\n";
                    report += $"Toplam Bitmeyen İş: {incompleteTasks.Count}\n\n";
                    
                    foreach (var task in incompleteTasks.Take(10))
                    {
                        report += $"• {task.Title} (Öncelik: {task.Priority}, Teslim: {task.DueDate?.ToString("dd.MM.yyyy") ?? "Belirtilmemiş"})\n";
                    }

                    txtResult.Text = report;
                    _voiceService.Speak($"Toplam {incompleteTasks.Count} bitmeyen iş var.");
                }
                else
                {
                    txtResult.Text = $"ℹ️ Komut tanındı ancak işlenemedi: {command}";
                    _voiceService.Speak("Komut anlaşılamadı. Lütfen tekrar deneyin.");
                }

                lblStatus.Text = "Hazır";
                lblStatus.BackColor = Color.LightGreen;
            }
            catch (Exception ex)
            {
                txtResult.Text = $"❌ Hata: {ex.Message}";
                lblStatus.Text = "Hata";
                lblStatus.BackColor = Color.Red;
                _voiceService.Speak("Bir hata oluştu.");
            }
        }

        private void BtnStartListening_Click(object sender, EventArgs e)
        {
            _voiceService.StartListening();
            _isListening = true;
            btnStartListening.Enabled = false;
            btnStopListening.Enabled = true;
            lblStatus.Text = "Dinleniyor...";
            lblStatus.BackColor = Color.Green;
        }

        private void BtnStopListening_Click(object sender, EventArgs e)
        {
            _voiceService.StopListening();
            _isListening = false;
            btnStartListening.Enabled = true;
            btnStopListening.Enabled = false;
            lblStatus.Text = "Durduruldu";
            lblStatus.BackColor = Color.LightGray;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isListening)
                _voiceService.StopListening();
            base.OnFormClosing(e);
        }
    }
}

