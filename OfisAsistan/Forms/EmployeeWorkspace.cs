using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Alerter;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using DevExpress.XtraSplashScreen;
using OfisAsistan.Models;
using OfisAsistan.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
// ALIASLAR
using AppTask = OfisAsistan.Models.Task;
using SysTask = System.Threading.Tasks.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;

namespace OfisAsistan.Forms
{
    public partial class EmployeeWorkspace : XtraForm
    {
        private readonly DatabaseService _databaseService;
        private readonly AIService _aiService;
        private readonly int _employeeId;

        // UI Bileşenleri
        private ListBoxControl lstPending, lstInProgress, lstCompleted;
        private MemoEdit txtChatHistory, txtQuickNotes, txtBriefing;
        private TextEdit txtChatInput;
        private AlertControl alertControl;
        private PopupMenu taskPopupMenu;
        private BarManager barManager;
        private ListBoxControl draggedSourceList;

        // --- RENK PALETİ ---
        private readonly Color clrSidebar = Color.FromArgb(99, 102, 241);     // İndigo
        private readonly Color clrSidebarDark = Color.FromArgb(67, 56, 202);
        private readonly Color clrBackground = Color.FromArgb(240, 242, 245);

        public EmployeeWorkspace(DatabaseService databaseService, AIService aiService, int employeeId)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _employeeId = employeeId;

            InitializeComponent();

            // --- KRİTİK KOMUT: ESKİ HER ŞEYİ SİL ---
            this.Controls.Clear();
            // ---------------------------------------

            InitializeCustomUI();
            InitializeContextMenu();

            alertControl = new AlertControl();
            alertControl.AutoFormDelay = 4000;

            this.Shown += async (s, e) => {
                await LoadDataAsync();
                await LoadDailyBriefing();
            };
        }

        // --- 1. KART GÖRÜNÜMÜ (HTML) ---
        public class TaskDisplayItem
        {
            public AppTask Task { get; set; }

            // Metni paragraf gibi formatla - TAM GÖSTER
            private string FormatDescription(string text)
            {
                if (string.IsNullOrEmpty(text)) return "<color=#999999>Açıklama yok.</color>";
                
                // Tüm açıklamayı göster, kesme
                string clean = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
                
                // Çok uzunsa sadece sonuna ... ekle
                int maxLength = 300; // Daha uzun göster
                if (clean.Length > maxLength)
                {
                    clean = clean.Substring(0, maxLength) + "...";
                }
                
                // Kelimeleri bölmeden, doğal satır sonları ekle
                StringBuilder sb = new StringBuilder();
                string[] words = clean.Split(' ');
                int lineLength = 0;
                int maxLineLength = 60; // Satır başına maksimum karakter
                
                foreach (string word in words)
                {
                    if (lineLength + word.Length + 1 > maxLineLength && lineLength > 0)
                    {
                        sb.Append("<br>");
                        lineLength = 0;
                    }
                    if (lineLength > 0)
                    {
                        sb.Append(" ");
                        lineLength++;
                    }
                    sb.Append(word);
                    lineLength += word.Length;
                }
                
                return sb.ToString();
            }

            public override string ToString()
            {
                if (Task == null) return "Geçersiz görev";
                
                string taskTitle = string.IsNullOrEmpty(Task.Title) ? "İsimsiz Görev" : Task.Title;
                string pColor = Task.Priority.ToString() == "High" || Task.Priority.ToString() == "Critical" ? "red" : 
                               (Task.Priority.ToString() == "Normal" ? "#E67E22" : "green");
                string dateStr = Task.DueDate.HasValue ? Task.DueDate.Value.ToString("dd.MM.yyyy") : "-";
                string desc = FormatDescription(Task.Description);
                string priorityText = Task.Priority.ToString();

                // AÇIKLAMA PARAGRAF GİBİ, TAM GÖSTER
                return $"<size=13><b><color=#1F2937>{taskTitle}</color></b></size><br><br>" +
                       $"<size=10><color=#4B5563>{desc}</color></size><br><br>" +
                       $"<size=9><color={pColor}><b>● {priorityText}</b></color>  <color=#6B7280>📅 {dateStr}</color></size>";
            }
        }

        // --- 2. UI TASARIMI (GroupControl Sistemine Geçildi) ---
        private void InitializeCustomUI()
        {
            this.Text = "Ofis Asistanı";
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(240, 242, 245); // Daha yumuşak gri arka plan

            // ANA TABLO (Sol Menü | Sağ İçerik)
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.Controls.Add(mainLayout);

            // -- SOL PANEL --
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Paint += (s, e) => {
                using (LinearGradientBrush brush = new LinearGradientBrush(leftPanel.ClientRectangle, clrSidebar, clrSidebarDark, 45F))
                    e.Graphics.FillRectangle(brush, leftPanel.ClientRectangle);
            };
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var lblLogo = new LabelControl { Text = "OFİS\nASİSTANI", Appearance = { Font = new Font("Segoe UI", 28, FontStyle.Bold), ForeColor = Color.White, TextOptions = { HAlignment = HorzAlignment.Center } }, Dock = DockStyle.Top, Padding = new Padding(0, 60, 0, 0), AutoSizeMode = LabelAutoSizeMode.None, Height = 200, BackColor = Color.Transparent };
            leftPanel.Controls.Add(lblLogo);

            // -- SAĞ İÇERİK --
            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            mainLayout.Controls.Add(contentPanel, 1, 0);

            // ÜST BAR
            var topBar = new Panel { Dock = DockStyle.Top, Height = 50, Margin = new Padding(0, 0, 0, 10) };

            // Pencere Kontrolleri (X, Kare, Alt Tire)
            var pnlWinControls = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, Width = 120 };
            var btnMin = CreateWindowBtn("_", (s, e) => this.WindowState = FormWindowState.Minimized);
            var btnMax = CreateWindowBtn("□", (s, e) => this.WindowState = (this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized));
            var btnClose = CreateWindowBtn("✕", (s, e) => this.Close(), true);
            pnlWinControls.Controls.AddRange(new Control[] { btnMin, btnMax, btnClose });
            topBar.Controls.Add(pnlWinControls);

            // Sol Butonlar (Yenile, AI)
            var pnlTools = new FlowLayoutPanel { Dock = DockStyle.Left, FlowDirection = FlowDirection.LeftToRight, Width = 600 };
            var btnRefresh = CreateModernButton("🔄 Yenile", Color.FromArgb(33, 150, 243), async (s, e) => await LoadDataAsync());
            var btnAiWizard = CreateModernButton("✨ AI Görev Parçalama", Color.FromArgb(156, 39, 176), async (s, e) => await OpenAiWizard());
            pnlTools.Controls.AddRange(new Control[] { btnRefresh, btnAiWizard });
            topBar.Controls.Add(pnlTools);

            contentPanel.Controls.Add(topBar);

            // İÇERİK BÖLÜNMESİ
            var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 10, 0, 0) };
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F)); // Kanban %55
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F)); // Araçlar %45
            contentPanel.Controls.Add(splitLayout);

            // A. KANBAN ALANI - Modern tasarım
            var kanbanGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 15) };
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.Padding = new Padding(5, 0, 5, 0);

            lstPending = CreateKanbanList();
            lstInProgress = CreateKanbanList();
            lstCompleted = CreateKanbanList();

            // Sütunları GroupControl ile ekliyoruz (Başlık sorunu kesin çözüm)
            AddGroupColumn(kanbanGrid, "📋 BEKLEYENLER", lstPending, 0, Color.Orange);
            AddGroupColumn(kanbanGrid, "💻 YÜRÜTÜLEN", lstInProgress, 1, Color.DodgerBlue);
            AddGroupColumn(kanbanGrid, "✅ TAMAMLANAN", lstCompleted, 2, Color.SeaGreen);

            splitLayout.Controls.Add(kanbanGrid, 0, 0);

            // B. ARAÇLAR ALANI
            var toolsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            toolsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            toolsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            toolsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            // B1. Brifing - PADDING DÜZELTİLDİ
            var grpBrief = CreateGroupPanel("📢 Günlük Brifing");
            var briefContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 45, 10, 10) }; // Başlık için üstten 45px
            txtBriefing = new MemoEdit 
            { 
                Dock = DockStyle.Fill, 
                Properties = { 
                    ReadOnly = true, 
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                    ScrollBars = System.Windows.Forms.ScrollBars.Vertical
                } 
            };
            briefContainer.Controls.Add(txtBriefing);
            grpBrief.Controls.Add(briefContainer);
            toolsGrid.Controls.Add(grpBrief, 0, 0);

            // B2. Notlar - PADDING DÜZELTİLDİ
            var grpNotes = CreateGroupPanel("📝 Hızlı Notlar");
            var notesContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 45, 10, 10) };
            txtQuickNotes = new MemoEdit 
            { 
                Dock = DockStyle.Fill, 
                Properties = { 
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder, 
                    NullText = "Not almak için buraya yazın...",
                    ScrollBars = System.Windows.Forms.ScrollBars.Vertical
                } 
            };
            notesContainer.Controls.Add(txtQuickNotes);
            grpNotes.Controls.Add(notesContainer);
            toolsGrid.Controls.Add(grpNotes, 1, 0);

            // B3. AI Chat - PADDING DÜZELTİLDİ
            var grpChat = CreateGroupPanel("🤖 Asistan");
            var chatContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 45, 10, 10) };
            var chatLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            chatLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            txtChatHistory = new MemoEdit 
            { 
                Dock = DockStyle.Fill, 
                Properties = { 
                    ReadOnly = true, 
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                    ScrollBars = System.Windows.Forms.ScrollBars.Vertical
                } 
            };
            txtChatInput = new TextEdit { Dock = DockStyle.Fill, Properties = { NullText = "Bir şeyler sor..." } };
            txtChatInput.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await SendMessageToAI(); };

            chatLayout.Controls.Add(txtChatHistory, 0, 0);
            chatLayout.Controls.Add(txtChatInput, 0, 1);
            chatContainer.Controls.Add(chatLayout);
            grpChat.Controls.Add(chatContainer);
            toolsGrid.Controls.Add(grpChat, 2, 0);

            splitLayout.Controls.Add(toolsGrid, 0, 1);

            // Eventler
            AttachListEvents(lstPending);
            AttachListEvents(lstInProgress);
            AttachListEvents(lstCompleted);
        }

        // --- 3. İŞLEVLER ---
        private async SysTask LoadDailyBriefing()
        {
            txtBriefing.Text = "AI verilerinizi analiz ediyor...";
            try { txtBriefing.Text = await _aiService.GenerateDailyBriefingAsync(_employeeId); }
            catch { txtBriefing.Text = "Brifing yüklenemedi."; }
        }

        private async SysTask OpenAiWizard()
        {
            var item = GetSelectedTaskItem();
            if (item == null || item.Task == null) 
            { 
                alertControl.Show(this, "Uyarı", "Lütfen parçalamak istediğiniz görevi seçin.", "", (Image)null); 
                return; 
            }

            IOverlaySplashScreenHandle overlay = SplashScreenManager.ShowOverlayForm(this);
            try
            {
                // Görev bilgilerini daha detaylı hazırla
                string taskInfo = $"{item.Task.Title}";
                if (!string.IsNullOrEmpty(item.Task.Description))
                {
                    taskInfo += $"\n\nAçıklama: {item.Task.Description}";
                }
                if (item.Task.DueDate.HasValue)
                {
                    taskInfo += $"\n\nTeslim Tarihi: {item.Task.DueDate.Value:dd.MM.yyyy}";
                }
                if (item.Task.EstimatedHours > 0)
                {
                    taskInfo += $"\n\nTahmini Süre: {item.Task.EstimatedHours} saat";
                }

                var subs = await _aiService.BreakDownTaskAsync(taskInfo);
                SplashScreenManager.CloseOverlayForm(overlay);
                
                if (subs != null && subs.Any())
                {
                    string message = $"'{item.Task.Title}' görevi için {subs.Count} alt adım önerildi:\n\n";
                    foreach (var s in subs.Take(5)) // İlk 5'ini göster
                    {
                        message += $"• {s.Title} ({s.EstimatedHours} saat)\n";
                    }
                    if (subs.Count > 5)
                    {
                        message += $"\n... ve {subs.Count - 5} adım daha";
                    }
                    message += "\n\nNotlara eklensin mi?";

                    if (XtraMessageBox.Show(message, "AI Görev Parçalama", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"\r\n=== AI PLAN: {item.Task.Title.ToUpper()} ===");
                        sb.AppendLine($"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}");
                        sb.AppendLine($"Toplam Alt Adım: {subs.Count}");
                        sb.AppendLine("─────────────────────────────────────");
                        foreach (var s in subs) 
                        {
                            sb.AppendLine($"[ ] {s.Title} ({s.EstimatedHours} saat)");
                        }
                        sb.AppendLine("─────────────────────────────────────\r\n");
                        txtQuickNotes.Text += sb.ToString();
                        alertControl.Show(this, "Başarılı", "Alt görevler notlara eklendi.", "", (Image)null);
                    }
                }
                else 
                {
                    XtraMessageBox.Show("AI bu görev için alt adım öneremedi. Lütfen görev açıklamasını daha detaylı yazın.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (overlay != null) SplashScreenManager.CloseOverlayForm(overlay);
                XtraMessageBox.Show($"Hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async SysTask LoadDataAsync()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync(_employeeId);
                if (this.IsHandleCreated) this.Invoke(new MethodInvoker(() => {
                    lstPending.Items.Clear(); lstInProgress.Items.Clear(); lstCompleted.Items.Clear();
                    foreach (var t in tasks)
                    {
                        var item = new TaskDisplayItem { Task = t };
                        if (t.Status == TaskStatusModel.Pending) lstPending.Items.Add(item);
                        else if (t.Status == TaskStatusModel.InProgress) lstInProgress.Items.Add(item);
                        else if (t.Status == TaskStatusModel.Completed) lstCompleted.Items.Add(item);
                    }
                }));
            }
            catch { }
        }

        private void AttachListEvents(ListBoxControl list)
        {
            list.MouseDown += (s, e) => {
                var index = list.IndexFromPoint(e.Location);
                if (index != -1 && index < list.ItemCount)
                {
                    list.SelectedIndex = index;
                    list.Focus(); // ÖNEMLİ: Listeyi focus'la
                    this.ActiveControl = list; // ActiveControl'ü güncelle
                    if (e.Button == MouseButtons.Right) taskPopupMenu.ShowPopup(list.PointToScreen(e.Location));
                    else if (e.Button == MouseButtons.Left) { draggedSourceList = list; list.DoDragDrop(list.SelectedItem, DragDropEffects.Move); }
                }
            };
            
            // Click event'i - seçimi garantilemek için
            list.Click += (s, e) => {
                if (list.SelectedIndex >= 0)
                {
                    list.Focus();
                    this.ActiveControl = list;
                }
            };
            
            // SelectedIndexChanged - seçim değiştiğinde focus'u güncelle
            list.SelectedIndexChanged += (s, e) => {
                if (list.SelectedIndex >= 0)
                {
                    list.Focus();
                    this.ActiveControl = list;
                }
            };
            list.DoubleClick += (s, e) => {
                var item = GetSelectedTaskItem();
                if (item != null) XtraMessageBox.Show(item.Task.Description, item.Task.Title);
            };
            list.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            list.DragDrop += async (s, e) => {
                var target = s as ListBoxControl; 
                var item = e.Data.GetData(typeof(TaskDisplayItem)) as TaskDisplayItem;
                if (item != null && item.Task != null && target != draggedSourceList)
                {
                    var ns = target == lstInProgress ? TaskStatusModel.InProgress : (target == lstCompleted ? TaskStatusModel.Completed : TaskStatusModel.Pending);
                    if (draggedSourceList == lstCompleted && ns != TaskStatusModel.Completed) return;
                    
                    // Eski durumu kaydet
                    var oldStatus = item.Task.Status;
                    item.Task.Status = ns;
                    
                    // UI'yi güncelle
                    target.Items.Add(item); 
                    draggedSourceList.Items.Remove(item);
                    
                    // Veritabanını güncelle (previousAssignedToId null geçiyoruz çünkü sadece status değişiyor, atama değişmiyor)
                    await _databaseService.UpdateTaskAsync(item.Task, null);
                }
            };
        }

        private TaskDisplayItem GetSelectedTaskItem()
        {
            // Hangi liste FOCUS'ta kontrol et - bu en önemli
            Control focusedControl = this.ActiveControl;
            
            // Focus kontrolü
            if (focusedControl == lstPending || (lstPending.Focused && lstPending.SelectedIndex >= 0))
            {
                if (lstPending.SelectedIndex >= 0 && lstPending.SelectedIndex < lstPending.ItemCount)
                {
                    var item = lstPending.SelectedItem as TaskDisplayItem;
                    if (item != null) return item;
                }
            }
            
            if (focusedControl == lstInProgress || (lstInProgress.Focused && lstInProgress.SelectedIndex >= 0))
            {
                if (lstInProgress.SelectedIndex >= 0 && lstInProgress.SelectedIndex < lstInProgress.ItemCount)
                {
                    var item = lstInProgress.SelectedItem as TaskDisplayItem;
                    if (item != null) return item;
                }
            }
            
            if (focusedControl == lstCompleted || (lstCompleted.Focused && lstCompleted.SelectedIndex >= 0))
            {
                if (lstCompleted.SelectedIndex >= 0 && lstCompleted.SelectedIndex < lstCompleted.ItemCount)
                {
                    var item = lstCompleted.SelectedItem as TaskDisplayItem;
                    if (item != null) return item;
                }
            }
            
            // Eğer focus yoksa, seçili olanı al
            if (lstPending.SelectedIndex >= 0 && lstPending.SelectedIndex < lstPending.ItemCount)
            {
                var item = lstPending.SelectedItem as TaskDisplayItem;
                if (item != null) return item;
            }
            
            if (lstInProgress.SelectedIndex >= 0 && lstInProgress.SelectedIndex < lstInProgress.ItemCount)
            {
                var item = lstInProgress.SelectedItem as TaskDisplayItem;
                if (item != null) return item;
            }
            
            if (lstCompleted.SelectedIndex >= 0 && lstCompleted.SelectedIndex < lstCompleted.ItemCount)
            {
                var item = lstCompleted.SelectedItem as TaskDisplayItem;
                if (item != null) return item;
            }
            
            return null;
        }

        private void InitializeContextMenu()
        {
            barManager = new BarManager { Form = this }; taskPopupMenu = new PopupMenu(barManager);
            var btn = new BarButtonItem(barManager, "Notlara Kopyala");
            btn.ItemClick += (s, e) => { var i = GetSelectedTaskItem(); if (i != null) txtQuickNotes.Text += $"\n📌 {i.Task.Title}\n{i.Task.Description}\n"; };
            taskPopupMenu.AddItem(btn);
        }

        private async SysTask SendMessageToAI()
        {
            string t = txtChatInput.Text.Trim(); 
            if (string.IsNullOrEmpty(t)) return;
            
            txtChatInput.Text = ""; 
            txtChatInput.Enabled = false;
            
            // Mesajı ekle
            txtChatHistory.Text += $"Ben: {t}\n\n";
            
            // Scroll'u en alta al
            txtChatHistory.SelectionStart = txtChatHistory.Text.Length;
            txtChatHistory.ScrollToCaret();
            
            try 
            { 
                string aiResponse = await _aiService.ChatWithAssistantAsync(t);
                txtChatHistory.Text += $"AI: {aiResponse}\n\n";
                
                // Tekrar scroll'u en alta al
                txtChatHistory.SelectionStart = txtChatHistory.Text.Length;
                txtChatHistory.ScrollToCaret();
            }
            catch (Exception ex)
            {
                txtChatHistory.Text += $"Hata: {ex.Message}\n\n";
                txtChatHistory.SelectionStart = txtChatHistory.Text.Length;
                txtChatHistory.ScrollToCaret();
            }
            finally 
            { 
                txtChatInput.Enabled = true; 
                txtChatInput.Focus(); 
            }
        }

        // --- TASARIM YARDIMCILARI ---
        private ListBoxControl CreateKanbanList() => new ListBoxControl
        {
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
            Appearance = { 
                Font = new Font("Segoe UI", 9), 
                BackColor = Color.White, 
                ForeColor = Color.Black
            },
            ItemHeight = 150, // Kart Yüksekliği (daha uzun açıklamalar için)
            AllowHtmlDraw = DefaultBoolean.True,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 5, 8, 5)
        };

        // Modern Kanban kolonu tasarımı - BAŞLIKLAR GÖRÜNÜR
        private void AddGroupColumn(TableLayoutPanel parent, string title, Control list, int col, Color headerColor)
        {
            // Ana container
            var outerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 8, 0),
                BackColor = Color.Transparent
            };

            // İç container - beyaz kart
            var containerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // BAŞLIK PANELİ - BÜYÜK VE BELİRGİN
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50, // Daha büyük
                BackColor = headerColor,
                Padding = new Padding(0)
            };

            // Başlık label'ı - BÜYÜK YAZI
            var titleLabel = new LabelControl
            {
                Text = title,
                Dock = DockStyle.Fill,
                Appearance = 
                {
                    Font = new Font("Segoe UI", 12, FontStyle.Bold), // Daha büyük font
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    TextOptions = 
                    {
                        HAlignment = HorzAlignment.Center,
                        VAlignment = VertAlignment.Center
                    }
                },
                AutoSizeMode = LabelAutoSizeMode.None
            };

            // Liste kontrolü
            list.Dock = DockStyle.Fill;
            list.Margin = new Padding(0);

            // EKLEME SIRASI: Önce liste, sonra başlık (Dock.Top üstte görünür)
            headerPanel.Controls.Add(titleLabel);
            containerPanel.Controls.Add(list);
            containerPanel.Controls.Add(headerPanel); // En son eklenen üstte
            outerPanel.Controls.Add(containerPanel);

            parent.Controls.Add(outerPanel, col, 0);
        }

        private Panel CreateGroupPanel(string title)
        {
            // Ana container - beyaz kart
            var containerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(5)
            };

            // Başlık paneli
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.FromArgb(99, 102, 241) // İndigo
            };

            var titleLabel = new LabelControl
            {
                Text = title,
                Dock = DockStyle.Fill,
                Appearance = 
                {
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    TextOptions = 
                    {
                        HAlignment = HorzAlignment.Center,
                        VAlignment = VertAlignment.Center
                    }
                },
                AutoSizeMode = LabelAutoSizeMode.None
            };

            // Başlığı ekle (en son eklenen üstte görünür - Dock.Top)
            headerPanel.Controls.Add(titleLabel);
            containerPanel.Controls.Add(headerPanel);
            
            return containerPanel;
        }

        private SimpleButton CreateModernButton(string text, Color color, EventHandler onClick)
        {
            var btn = new SimpleButton { Text = text, Size = new Size(160, 35), Cursor = Cursors.Hand };
            btn.Appearance.BackColor = color; btn.Appearance.ForeColor = Color.White; btn.Appearance.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
            btn.Click += onClick; return btn;
        }

        private SimpleButton CreateWindowBtn(string text, EventHandler onClick, bool isClose = false)
        {
            var btn = new SimpleButton { Text = text, Size = new Size(40, 35), Cursor = Cursors.Hand };
            btn.Appearance.BackColor = Color.FromArgb(220, 220, 225);
            btn.Appearance.ForeColor = Color.Black;
            btn.Appearance.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
            if (isClose)
            {
                btn.MouseEnter += (s, e) => { btn.Appearance.BackColor = Color.Crimson; btn.Appearance.ForeColor = Color.White; };
                btn.MouseLeave += (s, e) => { btn.Appearance.BackColor = Color.FromArgb(220, 220, 225); btn.Appearance.ForeColor = Color.Black; };
            }
            btn.Click += onClick; return btn;
        }

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "EmployeeWorkspace"; this.ResumeLayout(false); }
    }
}