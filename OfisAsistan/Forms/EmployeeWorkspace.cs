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

        // --- 1. KART GÖRÜNÜMÜ (SADECE BAŞLIK - AÇIKLAMA AYRI) ---
        public class TaskDisplayItem
        {
            public AppTask Task { get; set; }

            public override string ToString()
            {
                if (Task == null) return "Geçersiz görev";
                
                // SADECE BAŞLIK GÖSTER - Açıklama DrawItem event'inde çizilecek
                string taskTitle = string.IsNullOrEmpty(Task.Title) ? "İsimsiz Görev" : Task.Title;
                return taskTitle;
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
            var topBar = new Panel { Dock = DockStyle.Top, Height = 60, Margin = new Padding(0, 0, 0, 15) };

            // Pencere Kontrolleri (X, Kare, Alt Tire)
            var pnlWinControls = new FlowLayoutPanel { 
                Dock = DockStyle.Right, 
                FlowDirection = FlowDirection.LeftToRight, 
                Width = 140, // Genişlik artırıldı
                AutoSize = false,
                WrapContents = false
            };
            var btnMin = CreateWindowBtn("─", (s, e) => this.WindowState = FormWindowState.Minimized);
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
            var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 50, 0, 10) };
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F)); // Kanban %55
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F)); // Araçlar %45
            contentPanel.Controls.Add(splitLayout);

            // A. KANBAN ALANI - SIFIRDAN YENİDEN YAZILDI
            var kanbanGrid = new TableLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                ColumnCount = 3, 
                RowCount = 1, 
                Margin = new Padding(0, 20, 0, 15),
                Padding = new Padding(8, 0, 8, 0)
            };
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));

            // Listeleri oluştur
            lstPending = CreateKanbanList();
            lstInProgress = CreateKanbanList();
            lstCompleted = CreateKanbanList();

            // Kolon 1: BEKLEYENLER
            var col1 = CreateKanbanColumn("BEKLEYENLER", lstPending, Color.Orange);
            kanbanGrid.Controls.Add(col1, 0, 0);

            // Kolon 2: YÜRÜTÜLEN
            var col2 = CreateKanbanColumn("YÜRÜTÜLEN", lstInProgress, Color.DodgerBlue);
            kanbanGrid.Controls.Add(col2, 1, 0);

            // Kolon 3: TAMAMLANAN (son kolon, margin yok)
            var col3 = CreateKanbanColumn("TAMAMLANAN", lstCompleted, Color.SeaGreen, false);
            kanbanGrid.Controls.Add(col3, 2, 0);

            splitLayout.Controls.Add(kanbanGrid, 0, 0);

            // B. ARAÇLAR ALANI
            var toolsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 10, 0, 0) };
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
                if (item.Task.EstimatedHours.GetValueOrDefault() > 0)
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

        // Son seçilen görev - AI parçalama için kritik
        private TaskDisplayItem _lastSelectedItem = null;

        private void AttachListEvents(ListBoxControl list)
        {
            list.MouseDown += (s, e) => {
                var index = list.IndexFromPoint(e.Location);
                if (index != -1 && index < list.ItemCount)
                {
                    // DİĞER LİSTELERİN SEÇİMİNİ TEMİZLE
                    ClearOtherListSelections(list);
                    
                    list.SelectedIndex = index;
                    list.Focus();
                    this.ActiveControl = list;
                    
                    // Son seçilen görevi kaydet
                    _lastSelectedItem = list.SelectedItem as TaskDisplayItem;
                    
                    if (e.Button == MouseButtons.Right) taskPopupMenu.ShowPopup(list.PointToScreen(e.Location));
                    else if (e.Button == MouseButtons.Left) { draggedSourceList = list; list.DoDragDrop(list.SelectedItem, DragDropEffects.Move); }
                }
            };
            
            // Click event'i - seçimi garantilemek için
            list.Click += (s, e) => {
                if (list.SelectedIndex >= 0)
                {
                    ClearOtherListSelections(list);
                    list.Focus();
                    this.ActiveControl = list;
                    _lastSelectedItem = list.SelectedItem as TaskDisplayItem;
                }
            };
            
            // SelectedIndexChanged - seçim değiştiğinde
            list.SelectedIndexChanged += (s, e) => {
                if (list.SelectedIndex >= 0)
                {
                    ClearOtherListSelections(list);
                    list.Focus();
                    this.ActiveControl = list;
                    _lastSelectedItem = list.SelectedItem as TaskDisplayItem;
                }
            };
            list.DoubleClick += (s, e) => {
                var item = GetSelectedTaskItem();
                if (item != null && item.Task != null) 
                {
                    var detailForm = new TaskDetailForm(_databaseService, item.Task.Id, _employeeId, "Çalışan");
                    if (detailForm.ShowDialog() == DialogResult.OK)
                    {
                        // Görev güncellendiyse listeyi yenile
                        _ = LoadDataAsync(); // Fire and forget
                    }
                }
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

        // Diğer listelerin seçimini temizle
        private void ClearOtherListSelections(ListBoxControl currentList)
        {
            if (currentList != lstPending && lstPending != null)
                lstPending.SelectedIndex = -1;
            if (currentList != lstInProgress && lstInProgress != null)
                lstInProgress.SelectedIndex = -1;
            if (currentList != lstCompleted && lstCompleted != null)
                lstCompleted.SelectedIndex = -1;
        }

        private TaskDisplayItem GetSelectedTaskItem()
        {
            // Önce son seçilen görevi kontrol et - EN GÜVENİLİR
            if (_lastSelectedItem != null && _lastSelectedItem.Task != null)
            {
                return _lastSelectedItem;
            }
            
            // Hangi liste FOCUS'ta kontrol et
            Control focusedControl = this.ActiveControl;
            
            if (focusedControl == lstPending && lstPending.SelectedIndex >= 0)
            {
                return lstPending.SelectedItem as TaskDisplayItem;
            }
            
            if (focusedControl == lstInProgress && lstInProgress.SelectedIndex >= 0)
            {
                return lstInProgress.SelectedItem as TaskDisplayItem;
            }
            
            if (focusedControl == lstCompleted && lstCompleted.SelectedIndex >= 0)
            {
                return lstCompleted.SelectedItem as TaskDisplayItem;
            }
            
            // Herhangi bir seçili olan
            if (lstPending.SelectedIndex >= 0)
                return lstPending.SelectedItem as TaskDisplayItem;
            if (lstInProgress.SelectedIndex >= 0)
                return lstInProgress.SelectedItem as TaskDisplayItem;
            if (lstCompleted.SelectedIndex >= 0)
                return lstCompleted.SelectedItem as TaskDisplayItem;
            
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
            
            // Mesajı ekle - her mesaj ayrı satırda, aralarında boşluk
            txtChatHistory.Text += $"Ben: {t}\r\n";
            txtChatHistory.Text += "─────────────────────────\r\n";
            
            // Scroll'u en alta al
            txtChatHistory.SelectionStart = txtChatHistory.Text.Length;
            txtChatHistory.ScrollToCaret();
            
            try 
            { 
                string aiResponse = await _aiService.ChatWithAssistantAsync(t);
                txtChatHistory.Text += $"AI: {aiResponse}\r\n";
                txtChatHistory.Text += "═════════════════════════\r\n\r\n";
                
                // Tekrar scroll'u en alta al
                txtChatHistory.SelectionStart = txtChatHistory.Text.Length;
                txtChatHistory.ScrollToCaret();
            }
            catch (Exception ex)
            {
                txtChatHistory.Text += $"Hata: {ex.Message}\r\n";
                txtChatHistory.Text += "═════════════════════════\r\n\r\n";
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
        private ListBoxControl CreateKanbanList()
        {
            var list = new ListBoxControl
            {
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                Appearance = { 
                    Font = new Font("Segoe UI", 10), 
                    BackColor = Color.White, 
                    ForeColor = Color.Black
                },
                ItemHeight = 85, // Kart yüksekliği
                AllowHtmlDraw = DefaultBoolean.False, // HTML kapatıldı - custom paint kullanacağız
                Dock = DockStyle.Fill,
                HotTrackItems = true
            };
            
            // CUSTOM DRAW - Her kartı elle çiz
            list.DrawItem += (sender, e) => 
            {
                var item = e.Item as TaskDisplayItem;
                if (item == null || item.Task == null) return;

                var task = item.Task;
                var bounds = e.Bounds;
                var g = e.Graphics;
                var listBox = sender as ListBoxControl;
                
                // Anti-aliasing
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                
                // Arka plan - seçili mi kontrol et
                bool isSelected = listBox != null && listBox.SelectedIndex == e.Index;
                Color bgColor = isSelected 
                    ? Color.FromArgb(238, 242, 255) 
                    : Color.White;
                using (var bgBrush = new SolidBrush(bgColor))
                    g.FillRectangle(bgBrush, bounds);
                
                // Sol kenarda öncelik çizgisi
                Color priorityColor = task.Priority == TaskPriority.High || task.Priority == TaskPriority.Critical 
                    ? Color.FromArgb(239, 68, 68)  // Kırmızı
                    : (task.Priority == TaskPriority.Normal ? Color.FromArgb(245, 158, 11) : Color.FromArgb(34, 197, 94)); // Turuncu / Yeşil
                using (var priorityBrush = new SolidBrush(priorityColor))
                    g.FillRectangle(priorityBrush, bounds.X, bounds.Y + 5, 4, bounds.Height - 10);
                
                int leftPadding = bounds.X + 12;
                int textWidth = bounds.Width - 20;
                
                // BAŞLIK (Bold, büyük)
                using (var titleFont = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(Color.FromArgb(31, 41, 55)))
                {
                    string title = task.Title ?? "İsimsiz Görev";
                    if (title.Length > 35) title = title.Substring(0, 35) + "...";
                    g.DrawString(title, titleFont, titleBrush, new RectangleF(leftPadding, bounds.Y + 8, textWidth, 20));
                }
                
                // AÇIKLAMA (Normal, küçük, gri - BAŞLIĞIN ALTINDA)
                using (var descFont = new Font("Segoe UI", 9))
                using (var descBrush = new SolidBrush(Color.FromArgb(107, 114, 128)))
                {
                    string desc = task.Description ?? "Açıklama yok.";
                    desc = desc.Replace("\r\n", " ").Replace("\n", " ").Trim();
                    if (desc.Length > 60) desc = desc.Substring(0, 60) + "...";
                    g.DrawString(desc, descFont, descBrush, new RectangleF(leftPadding, bounds.Y + 32, textWidth, 20));
                }
                
                // ALT BİLGİ: Öncelik ve Tarih
                using (var infoFont = new Font("Segoe UI", 8))
                {
                    // Öncelik badge
                    string priorityText = $"● {task.Priority}";
                    using (var priBrush = new SolidBrush(priorityColor))
                        g.DrawString(priorityText, infoFont, priBrush, leftPadding, bounds.Y + 58);
                    
                    // Tarih
                    string dateStr = task.DueDate.HasValue ? $"📅 {task.DueDate.Value:dd.MM.yyyy}" : "";
                    using (var dateBrush = new SolidBrush(Color.FromArgb(156, 163, 175)))
                        g.DrawString(dateStr, infoFont, dateBrush, leftPadding + 80, bounds.Y + 58);
                }
                
                // Alt çizgi (ayırıcı)
                using (var linePen = new Pen(Color.FromArgb(229, 231, 235), 1))
                    g.DrawLine(linePen, bounds.X + 10, bounds.Bottom - 1, bounds.Right - 10, bounds.Bottom - 1);
                
                e.Handled = true; // Varsayılan çizimi engelle
            };
            
            return list;
        }

        // Kanban kolonu oluştur - BASİT VE GARANTİLİ
        private Panel CreateKanbanColumn(string title, ListBoxControl list, Color headerColor, bool hasRightMargin = true)
        {
            // Ana panel
            var columnPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = hasRightMargin ? new Padding(0, 0, 8, 0) : new Padding(0)
            };

            // İç layout: Başlık + Liste
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F)); // Başlık - daha kalın
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Liste

            // BAŞLIK - NORMAL LABEL, KESİNLİKLE GÖRÜNÜR
            var headerLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                BackColor = headerColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Height = 55
            };

            // Liste
            list.Dock = DockStyle.Fill;

            // Ekle
            layout.Controls.Add(headerLabel, 0, 0);
            layout.Controls.Add(list, 0, 1);
            
            columnPanel.Controls.Add(layout);
            return columnPanel;
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