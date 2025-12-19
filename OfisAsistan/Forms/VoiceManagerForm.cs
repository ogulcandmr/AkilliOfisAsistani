using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using DevExpress.Utils;

namespace OfisAsistan.Forms
{
    public partial class VoiceManagerForm : XtraForm
    {
        private VoiceService _voiceService;
        private AIService _aiService;
        private DatabaseService _databaseService;
        
        private SimpleButton btnStartListening;
        private SimpleButton btnStopListening;
        private TextEdit txtVoiceCommand;
        private MemoEdit txtResult;
        private LabelControl lblStatus;
        private LabelControl lblTitle;
        private LayoutControl layoutControl;
        private bool _isListening;

        public VoiceManagerForm(VoiceService voiceService, AIService aiService, DatabaseService databaseService)
        {
            _voiceService = voiceService;
            _aiService = aiService;
            _databaseService = databaseService;
            InitializeComponent();
            SetupDevExpressUI();
            SetupVoiceEvents();
        }

        private void SetupDevExpressUI()
        {
            this.Text = "Sesli Yönetici Asistanı";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            layoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(layoutControl);

            lblTitle = new LabelControl
            {
                Text = "🎤 SESLİ YÖNETİCİ MODÜLÜ",
                Appearance = { Font = new Font("Segoe UI", 18, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center } },
                AutoSizeMode = LabelAutoSizeMode.None,
                Height = 50
            };

            btnStartListening = new SimpleButton { Text = "Dinlemeyi Başlat", ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("actions_play.svg") }, Appearance = { BackColor = Color.FromArgb(76, 175, 80), ForeColor = Color.White } };
            btnStopListening = new SimpleButton { Text = "Dinlemeyi Durdur", ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("actions_pause.svg") }, Enabled = false, Appearance = { BackColor = Color.FromArgb(244, 67, 54), ForeColor = Color.White } };

            txtVoiceCommand = new TextEdit { Properties = { ReadOnly = true }, Font = new Font("Segoe UI", 12) };
            txtResult = new MemoEdit { Properties = { ReadOnly = true }, Font = new Font("Consolas", 10) };
            
            lblStatus = new LabelControl
            {
                Text = "HAZIR",
                Appearance = { Font = new Font("Segoe UI", 10, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center }, BackColor = Color.LightGray },
                AutoSizeMode = LabelAutoSizeMode.None,
                Height = 30
            };

            var group = layoutControl.Root;
            group.AddItem(null, lblTitle).TextVisible = false;
            
            var btnGroup = group.AddGroup();
            btnGroup.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            btnGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });
            btnGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });
            btnGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.AutoSize });
            btnGroup.AddItem(null, btnStartListening).OptionsTableLayoutItem.ColumnIndex = 0;
            btnGroup.AddItem(null, btnStopListening).OptionsTableLayoutItem.ColumnIndex = 1;

            group.AddItem("Algılanan Komut", txtVoiceCommand).TextLocation = Locations.Top;
            group.AddItem("İşlem Sonucu", txtResult).TextLocation = Locations.Top;
            group.AddItem(null, lblStatus).TextVisible = false;

            btnStartListening.Click += BtnStartListening_Click;
            btnStopListening.Click += BtnStopListening_Click;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "VoiceManagerForm";
            this.ResumeLayout(false);
        }

        private void SetupVoiceEvents()
        {
            if (_voiceService != null)
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
            lblStatus.Text = "İŞLENİYOR...";
            lblStatus.Appearance.BackColor = Color.Orange;

            try
            {
                if (command.ToLower().Contains("görev ata") || command.ToLower().Contains("yeni görev"))
                {
                    var task = await _aiService.ParseVoiceCommandToTaskAsync(command);
                    if (task != null)
                    {
                        var created = await _databaseService.CreateTaskAsync(task);
                        if (created != null)
                        {
                            txtResult.Text = $"✅ Görev oluşturuldu:\nBaşlık: {created.Title}\nAtanan: {created.AssignedToId}\nTeslim: {created.DueDate?.ToShortDateString()}";
                            _voiceService?.Speak("Görev başarıyla oluşturuldu.");
                        }
                    }
                }
                else if (command.ToLower().Contains("rapor") || command.ToLower().Contains("listele"))
                {
                    var tasks = await _databaseService.GetTasksAsync();
                    var report = $"📊 Rapor:\n\nToplam Bitmeyen İş: {tasks.Count(t => t.Status != TaskStatusModel.Completed)}\n\n";
                    foreach (var t in tasks.Where(t => t.Status != TaskStatusModel.Completed).Take(5))
                        report += $"• {t.Title} ({t.Priority})\n";
                    txtResult.Text = report;
                    _voiceService?.Speak("Rapor hazırlandı.");
                }
                lblStatus.Text = "TAMAMLANDI";
                lblStatus.Appearance.BackColor = Color.LightGreen;
            }
            catch (Exception ex)
            {
                txtResult.Text = "Hata: " + ex.Message;
                lblStatus.Text = "HATA";
                lblStatus.Appearance.BackColor = Color.Red;
            }
        }

        private void BtnStartListening_Click(object sender, EventArgs e)
        {
            _voiceService?.StartListening();
            _isListening = true;
            btnStartListening.Enabled = false;
            btnStopListening.Enabled = true;
            lblStatus.Text = "DİNLENİYOR...";
            lblStatus.Appearance.BackColor = Color.LimeGreen;
        }

        private void BtnStopListening_Click(object sender, EventArgs e)
        {
            _voiceService?.StopListening();
            _isListening = false;
            btnStartListening.Enabled = true;
            btnStopListening.Enabled = false;
            lblStatus.Text = "DURDURULDU";
            lblStatus.Appearance.BackColor = Color.LightGray;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isListening) _voiceService?.StopListening();
            base.OnFormClosing(e);
        }
    }
}
