using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks; // System.Threading.Tasks.Task için
using System.Windows.Forms;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.Utils;
using DevExpress.XtraLayout;

// İsim çakışmalarını önlemek için alias
using TaskModel = OfisAsistan.Models.Task;
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
        private MemoEdit txtBriefing;     // Günlük Brifing (Salt Okunur)
        private MemoEdit txtQuickNotes;   // Hızlı Notlar (Yazılabilir)
        private Panel leftPanel, rightPanel;
        private LabelControl lblTimerDisplay;

        // Pomodoro
        private Timer focusTimer;
        private int focusSeconds = 0;
        private bool isTimerRunning = false;

        // Drag & Drop
        private bool isDragging = false;
        private Point dragStart;
        private ListBoxControl draggedSourceList;

        // Renkler
        private readonly Color clrPrimary = Color.FromArgb(99, 102, 241);
        private readonly Color clrSecondary = Color.FromArgb(76, 29, 149);
        private readonly Color clrBackground = Color.FromArgb(245, 245, 250);
        private readonly Color clrTextDark = Color.FromArgb(17, 24, 39);
        private readonly Color clrSurface = Color.White;

        public EmployeeWorkspace(DatabaseService databaseService, AIService aiService, int employeeId)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _employeeId = employeeId;

            InitializeComponent();
            SetupModernContainer();
            SetupContentLayout();

            // Form açıldığında verileri yükle (Async)
            this.Shown += async (s, e) => await LoadDataAsync();
        }

        // --- YARDIMCI SINIF ---
        public class TaskDisplayItem
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public TaskModel Task { get; set; }

            // ListBox içinde HTML formatlı gösterim
            public override string ToString()
            {
                // HTML formatı: Kalın başlık, alt satırda ufak gri tarih ve öncelik
                string priorityColor = Task.Priority.ToString() == "High" ? "red" : "gray";
                return $"<size=10><b>{Title}</b></size><br><size=8><color={priorityColor}>🔥 {Task.Priority}</color>  📅 {Task.DueDate:dd.MM}</size>";
            }
        }

        private void SetupContentLayout()
        {
            // Sağ Panel İçeriği (Ana Alan)
            var contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Padding = new Padding(20) };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F)); // Sol: Kanban (Geniş)
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Sağ: Araçlar (Dar)
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));      // Toolbar
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));      // İçerik
            rightPanel.Controls.Add(contentLayout);

            // --- 1. TOOLBAR ---
            var toolbarPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 10, 0, 0) };

            var btnRefresh = CreateModernButton("Yenile", "actions_refresh.svg", Color.FromArgb(0, 120, 212));
            btnRefresh.Click += async (s, e) => await LoadDataAsync();

            var btnAI = CreateModernButton("AI Sihirbazı", "actions_add.svg", Color.FromArgb(34, 139, 34));
            btnAI.Click += BtnBreakDown_Click;

            toolbarPanel.Controls.Add(btnRefresh);
            toolbarPanel.Controls.Add(btnAI);
            contentLayout.Controls.Add(toolbarPanel, 0, 0);
            contentLayout.SetColumnSpan(toolbarPanel, 2); // Toolbar boydan boya uzansın

            // --- 2. SOL TARA (KANBAN) ---
            var kanbanLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 3 };
            kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            kanbanLayout.Padding = new Padding(0, 10, 20, 0); // Sağ tarafa boşluk bırak

            lstPending = CreateKanbanList();
            lstInProgress = CreateKanbanList();
            lstCompleted = CreateKanbanList();

            AddKanbanColumn(kanbanLayout, "⏳ BEKLEYEN", lstPending, 0, Color.FromArgb(245, 158, 11));
            AddKanbanColumn(kanbanLayout, "⚡ ODAK (YAPILIYOR)", lstInProgress, 1, Color.FromArgb(59, 130, 246));
            AddKanbanColumn(kanbanLayout, "✅ TAMAMLANDI", lstCompleted, 2, Color.FromArgb(16, 185, 129));

            contentLayout.Controls.Add(kanbanLayout, 0, 1);

            // --- 3. SAĞ TARAF (SIDEBAR ARAÇLARI) ---
            var sidebarPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };

            // A. Pomodoro Sayacı
            var pnlTimer = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(240, 245, 255), Padding = new Padding(5), Margin = new Padding(0, 0, 0, 20) };
            lblTimerDisplay = new LabelControl { Text = "25:00", Dock = DockStyle.Top, Appearance = { Font = new Font("Consolas", 28, FontStyle.Bold), ForeColor = clrPrimary, TextOptions = { HAlignment = HorzAlignment.Center } } };
            var lblTimerTitle = new LabelControl { Text = "POMODORO SAYAÇ", Dock = DockStyle.Top, Appearance = { Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextOptions = { HAlignment = HorzAlignment.Center } } };
            var btnTimerToggle = new SimpleButton { Text = "⏯ Başlat/Durdur", Dock = DockStyle.Bottom, Height = 35 };
            btnTimerToggle.Click += (s, e) => ToggleTimer();

            pnlTimer.Controls.Add(btnTimerToggle);
            pnlTimer.Controls.Add(lblTimerDisplay);
            pnlTimer.Controls.Add(lblTimerTitle);
            sidebarPanel.Controls.Add(pnlTimer);

            // B. AI Asistan Chat Butonu
            var btnAiChat = new SimpleButton { Text = "🤖 AI Asistan'a Sor", Dock = DockStyle.Bottom, Height = 45, Appearance = { BackColor = clrPrimary, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) } };
            btnAiChat.Click += async (s, e) => await AskAIToHelp();
            sidebarPanel.Controls.Add(btnAiChat);

            // C. Hızlı Notlar
            var lblNotes = new LabelControl { Text = "<br>📝 <b>HIZLI NOTLAR</b>", AllowHtmlString = true, Dock = DockStyle.Top };
            txtQuickNotes = new MemoEdit { Dock = DockStyle.Top, Height = 150, Font = new Font("Segoe UI", 10) };
            txtQuickNotes.Properties.NullText = "Aklına gelenleri not al...";
            sidebarPanel.Controls.Add(txtQuickNotes);
            sidebarPanel.Controls.Add(lblNotes);

            // D. Günlük Brifing (En üstte)
            var lblBriefing = new LabelControl { Text = "<br>💡 <b>GÜNLÜK BRİFİNG</b>", AllowHtmlString = true, Dock = DockStyle.Top };
            txtBriefing = new MemoEdit { Dock = DockStyle.Top, Height = 120, Properties = { ReadOnly = true, ScrollBars = ScrollBars.None }, Font = new Font("Segoe UI", 9), BackColor = Color.FromArgb(250, 250, 250) };
            sidebarPanel.Controls.Add(txtBriefing);
            sidebarPanel.Controls.Add(lblBriefing);

            contentLayout.Controls.Add(sidebarPanel, 1, 1);

            // --- OLAYLARI BAĞLA ---
            AttachListEvents(lstPending);
            AttachListEvents(lstInProgress);
            AttachListEvents(lstCompleted);

            // Timer Başlat
            focusTimer = new Timer { Interval = 1000 };
            focusTimer.Tick += (s, e) => {
                if (focusSeconds > 0)
                {
                    focusSeconds--;
                    TimeSpan t = TimeSpan.FromSeconds(focusSeconds);
                    lblTimerDisplay.Text = t.ToString(@"mm\:ss");
                }
                else
                {
                    focusTimer.Stop(); isTimerRunning = false;
                    XtraMessageBox.Show("Süre doldu! Bir mola ver.", "Pomodoro");
                    focusSeconds = 25 * 60; // Reset
                }
            };
            focusSeconds = 25 * 60; // 25 dakika varsayılan
        }

        // --- MANTIK KISMI ---

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync(_employeeId);

                this.Invoke(new MethodInvoker(() =>
                {
                    lstPending.Items.Clear();
                    lstInProgress.Items.Clear();
                    lstCompleted.Items.Clear();

                    foreach (var task in tasks)
                    {
                        var item = new TaskDisplayItem { Id = task.Id, Title = task.Title, Task = task };

                        if (task.Status == TaskStatusModel.Pending) lstPending.Items.Add(item);
                        else if (task.Status == TaskStatusModel.InProgress) lstInProgress.Items.Add(item);
                        else if (task.Status == TaskStatusModel.Completed) lstCompleted.Items.Add(item);
                    }
                }));

                var briefing = await _aiService.GenerateDailyBriefingAsync(_employeeId);
                txtBriefing.Text = briefing;
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Veri yüklenemedi: " + ex.Message);
            }
        }

        private void AttachListEvents(ListBoxControl list)
        {
            // Çift Tıklama -> Detay
            list.DoubleClick += async (s, e) =>
            {
                if (list.SelectedItem is TaskDisplayItem item)
                {
                    var f = new TaskDetailForm(_databaseService, item.Id, _employeeId, "Çalışan");
                    if (f.ShowDialog() == DialogResult.OK) await LoadDataAsync();
                }
            };

            // Sürükle Bırak (Drag & Drop)
            list.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left && list.SelectedIndex != -1)
                {
                    draggedSourceList = list;
                    list.DoDragDrop(list.SelectedItem, DragDropEffects.Move);
                }
            };

            list.DragOver += (s, e) => e.Effect = DragDropEffects.Move;

            list.DragDrop += async (s, e) => {
                var targetList = s as ListBoxControl;
                if (targetList == draggedSourceList) return;

                var item = e.Data.GetData(typeof(TaskDisplayItem)) as TaskDisplayItem;
                if (item != null)
                {
                    var newStatus = TaskStatusModel.Pending;
                    if (targetList == lstInProgress) newStatus = TaskStatusModel.InProgress;
                    else if (targetList == lstCompleted) newStatus = TaskStatusModel.Completed;

                    item.Task.Status = newStatus;
                    await _databaseService.UpdateTaskAsync(item.Task, item.Task.AssignedToId);
                    await LoadDataAsync();
                }
            };
        }

        private void ToggleTimer()
        {
            isTimerRunning = !isTimerRunning;
            if (isTimerRunning) focusTimer.Start(); else focusTimer.Stop();
        }

        // --- AI FONKSİYONLARI ---

        private async void BtnBreakDown_Click(object sender, EventArgs e)
        {
            string input = XtraInputBox.Show("AI Alt Görev Sihirbazı", "Planlamak istediğiniz görevi açıklayın:", "");
            if (string.IsNullOrWhiteSpace(input)) return;

            XtraMessageBox.Show("AI Analiz Ediyor...", "Bekleyiniz");
            var subTasks = await _aiService.BreakDownTaskAsync(input);

            if (subTasks == null || !subTasks.Any()) return;

            string msg = "AI Önerilen Plan:\n\n" + string.Join("\n", subTasks.Select(s => $"• {s.Title}"));
            if (XtraMessageBox.Show(msg + "\n\nBu planı onaylıyor musunuz?", "AI Planlama", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                foreach (var s in subTasks)
                    await _databaseService.CreateTaskAsync(new TaskModel { Title = s.Title, Description = s.Description, AssignedToId = _employeeId, Status = TaskStatusModel.Pending, CreatedDate = DateTime.Now });

                await LoadDataAsync();
            }
        }

        private async System.Threading.Tasks.Task AskAIToHelp()
        {
            // Yapılıyor listesindeki seçili öğeyi al
            var item = lstInProgress.SelectedItem as TaskDisplayItem;
            if (item == null) { XtraMessageBox.Show("Önce 'Odak (Yapılıyor)' listesinden bir iş seçmelisin.", "Uyarı"); return; }

            // Basit bir simülasyon (Gerçek AI çağrısı eklenebilir)
            await System.Threading.Tasks.Task.Delay(500);
            XtraMessageBox.Show($"AI Asistan '{item.Title}' görevi için teknik dökümanları tarıyor...\n(Bu özellik yakında aktif olacak)", "AI Yardım");
        }

        // --- TASARIM YARDIMCILARI ---

        private void SetupModernContainer()
        {
            this.Controls.Clear();
            this.Text = "Çalışan Dijital Ofis Paneli";
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = clrBackground;

            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < 80) { isDragging = true; dragStart = e.Location; } };
            this.MouseMove += (s, e) => { if (isDragging) { Point p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            this.MouseUp += (s, e) => { isDragging = false; };

            var mainSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350F));
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.Controls.Add(mainSplit);

            leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrPrimary };
            leftPanel.Paint += LeftPanel_Paint;
            mainSplit.Controls.Add(leftPanel, 0, 0);

            // SOL MENÜ İÇERİĞİ
            var titleLabel = new LabelControl { Text = "Çalışan\nPaneli", Appearance = { Font = new Font("Segoe UI", 32, FontStyle.Bold), ForeColor = Color.White }, Location = new Point(40, 60), AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(250, 120), BackColor = Color.Transparent };
            leftPanel.Controls.Add(titleLabel);

            var subTitle = new LabelControl { Text = "Kişisel Görev ve\nPerformans Takibi", Appearance = { Font = new Font("Segoe UI", 12), ForeColor = Color.FromArgb(224, 231, 255) }, Location = new Point(45, 190), AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(250, 50), BackColor = Color.Transparent };
            leftPanel.Controls.Add(subTitle);

            // Durum Seçici (Sol Menüde)
            var cmbStatus = new ComboBoxEdit { Width = 250, Location = new Point(45, 300) };
            cmbStatus.Properties.Items.AddRange(new string[] { "🟢 Müsait", "🔴 Meşgul", "🟡 Toplantıda", "☕ Molada" });
            cmbStatus.SelectedIndex = 0;
            cmbStatus.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            cmbStatus.Properties.Appearance.Font = new Font("Segoe UI", 11);
            leftPanel.Controls.Add(cmbStatus);

            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrBackground };
            mainSplit.Controls.Add(rightPanel, 1, 0);
            CreateCustomWindowControls(rightPanel);
        }

        private SimpleButton CreateModernButton(string text, string svg, Color color)
        {
            var btn = new SimpleButton { Text = text, Size = new Size(200, 45), Appearance = { Font = new Font("Segoe UI Semibold", 10), BackColor = Color.White, ForeColor = color, BorderColor = color }, Cursor = Cursors.Hand, PaintStyle = DevExpress.XtraEditors.Controls.PaintStyles.Light };
            btn.ImageOptions.SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage(svg);
            btn.ImageOptions.SvgImageSize = new Size(20, 20);
            btn.LookAndFeel.UseDefaultLookAndFeel = false;
            btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            return btn;
        }

        private ListBoxControl CreateKanbanList()
        {
            return new ListBoxControl
            {
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                Appearance = { Font = new Font("Segoe UI", 10), BackColor = Color.White, ForeColor = clrTextDark },
                ItemHeight = 60,
                AllowHtmlDraw = DefaultBoolean.True // HTML açık
            };
        }

        private void AddKanbanColumn(TableLayoutPanel parent, string title, Control list, int col, Color headerColor)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), BackColor = Color.White };
            var header = new LabelControl { Text = title, Dock = DockStyle.Top, Height = 45, AutoSizeMode = LabelAutoSizeMode.None, Appearance = { BackColor = headerColor, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center } } };
            list.Dock = DockStyle.Fill;
            pnl.Controls.Add(list);
            pnl.Controls.Add(header);
            parent.Controls.Add(pnl, col, 0);
        }

        private void LeftPanel_Paint(object sender, PaintEventArgs e)
        {
            var p = sender as Panel;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new LinearGradientBrush(p.ClientRectangle, clrPrimary, clrSecondary, 45f)) g.FillRectangle(brush, p.ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 2)) { g.DrawEllipse(pen, -100, -100, 400, 400); g.DrawEllipse(pen, p.Width - 200, p.Height - 200, 500, 500); }
        }

        void CreateCustomWindowControls(Panel parent)
        {
            int btnSize = 40;
            var closeBtn = new SimpleButton { Text = "✕", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - btnSize, 0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            StyleWindowBtn(closeBtn); closeBtn.Click += (s, e) => this.Close(); closeBtn.MouseEnter += (s, e) => closeBtn.Appearance.BackColor = Color.FromArgb(239, 68, 68); closeBtn.MouseLeave += (s, e) => closeBtn.Appearance.BackColor = Color.Transparent; parent.Controls.Add(closeBtn);
            var maxBtn = new SimpleButton { Text = "□", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - (btnSize * 2), 0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            StyleWindowBtn(maxBtn); maxBtn.Click += (s, e) => this.WindowState = (this.WindowState == FormWindowState.Normal) ? FormWindowState.Maximized : FormWindowState.Normal; parent.Controls.Add(maxBtn);
            var minBtn = new SimpleButton { Text = "─", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - (btnSize * 3), 0), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            StyleWindowBtn(minBtn); minBtn.Click += (s, e) => this.WindowState = FormWindowState.Minimized; parent.Controls.Add(minBtn);
        }

        void StyleWindowBtn(SimpleButton btn) { btn.Appearance.BackColor = Color.Transparent; btn.Appearance.ForeColor = Color.Gray; btn.Appearance.Font = new Font("Segoe UI", 12); btn.LookAndFeel.UseDefaultLookAndFeel = false; btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat; btn.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder; }

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "EmployeeWorkspace"; this.ResumeLayout(false); }
    }
}