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

            // Metni belirli uzunlukta kesip alt satıra atan fonksiyon
            private string FormatDescription(string text)
            {
                if (string.IsNullOrEmpty(text)) return "Açıklama yok.";
                string clean = text.Replace("\n", " ").Replace("\r", "");

                // Çok uzunsa kısalt
                if (clean.Length > 120) clean = clean.Substring(0, 117) + "...";

                // HTML içinde düzgün görünmesi için her 40 karakterde bir <br> ekle
                StringBuilder sb = new StringBuilder();
                int counter = 0;
                foreach (char c in clean)
                {
                    sb.Append(c);
                    counter++;
                    if (counter >= 40 && c == ' ') // Kelime bölmemek için boşlukta kes
                    {
                        sb.Append("<br>");
                        counter = 0;
                    }
                }
                return sb.ToString();
            }

            public override string ToString()
            {
                string pColor = Task.Priority.ToString() == "High" ? "red" : (Task.Priority.ToString() == "Medium" ? "#E67E22" : "green");
                string dateStr = Task.DueDate.HasValue ? Task.DueDate.Value.ToString("dd.MM") : "-";
                string desc = FormatDescription(Task.Description);

                return $"<size=11><b>{Task.Title}</b></size><br>" +
                       $"<size=9><color=#666666>{desc}</color></size><br><br>" +
                       $"<size=8><color={pColor}><b>● {Task.Priority}</b></color>      <color=gray>📅 {dateStr}</color></size>";
            }
        }

        // --- 2. UI TASARIMI (GroupControl Sistemine Geçildi) ---
        private void InitializeCustomUI()
        {
            this.Text = "Ofis Asistanı";
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = clrBackground;

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
            var btnAiWizard = CreateModernButton("✨ Alt Görev Sihirbazı", Color.FromArgb(156, 39, 176), async (s, e) => await OpenAiWizard());
            pnlTools.Controls.AddRange(new Control[] { btnRefresh, btnAiWizard });
            topBar.Controls.Add(pnlTools);

            contentPanel.Controls.Add(topBar);

            // İÇERİK BÖLÜNMESİ
            var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 10, 0, 0) };
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F)); // Kanban %55
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F)); // Araçlar %45
            contentPanel.Controls.Add(splitLayout);

            // A. KANBAN ALANI (GroupControl Kullanarak)
            var kanbanGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 15) };
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));

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

            // B1. Brifing
            var grpBrief = CreateGroupPanel("📢 Günlük Brifing");
            txtBriefing = new MemoEdit { Dock = DockStyle.Fill, Properties = { ReadOnly = true, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder } };
            grpBrief.Controls.Add(txtBriefing);
            toolsGrid.Controls.Add(grpBrief, 0, 0);

            // B2. Notlar
            var grpNotes = CreateGroupPanel("📝 Hızlı Notlar");
            txtQuickNotes = new MemoEdit { Dock = DockStyle.Fill, Properties = { BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder, NullText = "Not almak için buraya yazın..." } };
            grpNotes.Controls.Add(txtQuickNotes);
            toolsGrid.Controls.Add(grpNotes, 1, 0);

            // B3. AI Chat
            var grpChat = CreateGroupPanel("🤖 Asistan");
            var chatLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            chatLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            txtChatHistory = new MemoEdit { Dock = DockStyle.Fill, Properties = { ReadOnly = true, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder } };
            txtChatInput = new TextEdit { Dock = DockStyle.Fill, Properties = { NullText = "Bir şeyler sor..." } };
            txtChatInput.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await SendMessageToAI(); };

            chatLayout.Controls.Add(txtChatHistory, 0, 0);
            chatLayout.Controls.Add(txtChatInput, 0, 1);
            grpChat.Controls.Add(chatLayout);
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
            if (item == null) { alertControl.Show(this, "Uyarı", "Lütfen bir görev seçin.", "", (Image)null); return; }

            IOverlaySplashScreenHandle overlay = SplashScreenManager.ShowOverlayForm(this);
            try
            {
                var subs = await _aiService.BreakDownTaskAsync(item.Task.Title + " - " + item.Task.Description);
                SplashScreenManager.CloseOverlayForm(overlay);
                if (subs != null && subs.Any())
                {
                    if (XtraMessageBox.Show($"{subs.Count} alt adım önerildi. Notlara eklensin mi?", "AI Planlama", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"\r\n=== PLAN: {item.Task.Title.ToUpper()} ===");
                        foreach (var s in subs) sb.AppendLine($"[ ] {s.Title} ({s.EstimatedHours}s)");
                        sb.AppendLine("================================\r\n");
                        txtQuickNotes.Text += sb.ToString();
                        alertControl.Show(this, "Başarılı", "Notlara eklendi.", "", (Image)null);
                    }
                }
                else XtraMessageBox.Show("AI yanıt veremedi.", "Hata");
            }
            catch { if (overlay != null) SplashScreenManager.CloseOverlayForm(overlay); }
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
                if (index != -1)
                {
                    list.SelectedIndex = index;
                    if (e.Button == MouseButtons.Right) taskPopupMenu.ShowPopup(list.PointToScreen(e.Location));
                    else if (e.Button == MouseButtons.Left) { draggedSourceList = list; list.DoDragDrop(list.SelectedItem, DragDropEffects.Move); }
                }
            };
            list.DoubleClick += (s, e) => {
                var item = GetSelectedTaskItem();
                if (item != null) XtraMessageBox.Show(item.Task.Description, item.Task.Title);
            };
            list.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            list.DragDrop += async (s, e) => {
                var target = s as ListBoxControl; var item = e.Data.GetData(typeof(TaskDisplayItem)) as TaskDisplayItem;
                if (item != null && target != draggedSourceList)
                {
                    var ns = target == lstInProgress ? TaskStatusModel.InProgress : (target == lstCompleted ? TaskStatusModel.Completed : TaskStatusModel.Pending);
                    if (draggedSourceList == lstCompleted && ns != TaskStatusModel.Completed) return;
                    target.Items.Add(item); draggedSourceList.Items.Remove(item);
                    item.Task.Status = ns; await _databaseService.UpdateTaskAsync(item.Task, _employeeId);
                }
            };
        }

        private TaskDisplayItem GetSelectedTaskItem()
        {
            if (lstPending.SelectedIndex != -1) return lstPending.SelectedItem as TaskDisplayItem;
            if (lstInProgress.SelectedIndex != -1) return lstInProgress.SelectedItem as TaskDisplayItem;
            if (lstCompleted.SelectedIndex != -1) return lstCompleted.SelectedItem as TaskDisplayItem;
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
            string t = txtChatInput.Text.Trim(); if (string.IsNullOrEmpty(t)) return;
            txtChatHistory.Text += $"Ben: {t}\n"; txtChatInput.Text = ""; txtChatInput.Enabled = false;
            try { txtChatHistory.Text += $"AI: {await _aiService.ChatWithAssistantAsync(t)}\n\n"; }
            catch { }
            finally { txtChatInput.Enabled = true; txtChatInput.Focus(); }
        }

        // --- TASARIM YARDIMCILARI ---
        private ListBoxControl CreateKanbanList() => new ListBoxControl
        {
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
            Appearance = { Font = new Font("Segoe UI", 9), BackColor = Color.White, ForeColor = Color.Black },
            ItemHeight = 120, // Kart Yüksekliği (Açıklama sığsın diye)
            AllowHtmlDraw = DefaultBoolean.True,
            Dock = DockStyle.Fill
        };

        // GroupControl Kullanarak Başlık Sorununu Çözdük
        private void AddGroupColumn(TableLayoutPanel parent, string title, Control list, int col, Color headerColor)
        {
            var group = new GroupControl
            {
                Text = title,
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 0, 5, 0)
            };
            // Başlık stilini özelleştirme (Caption Image vs eklenebilir)
            group.AppearanceCaption.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            group.AppearanceCaption.ForeColor = headerColor; // Başlık rengi

            list.Dock = DockStyle.Fill;
            group.Controls.Add(list);
            parent.Controls.Add(group, col, 0);
        }

        private GroupControl CreateGroupPanel(string title)
        {
            var group = new GroupControl
            {
                Text = title,
                Dock = DockStyle.Fill,
                Margin = new Padding(5)
            };
            group.AppearanceCaption.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            return group;
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