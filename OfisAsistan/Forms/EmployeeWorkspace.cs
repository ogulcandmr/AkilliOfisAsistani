using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.Utils;

namespace OfisAsistan.Forms
{
    public partial class EmployeeWorkspace : XtraForm
    {
        private readonly DatabaseService _databaseService;
        private readonly AIService _aiService;
        private readonly int _employeeId;

        private ListBoxControl lstPending, lstInProgress, lstCompleted;
        private MemoEdit txtBriefing;
        private Panel leftPanel, rightPanel;
        private bool isDragging = false;
        private Point dragStart;

        // Renkler
        private readonly Color clrPrimary = Color.FromArgb(99, 102, 241);
        private readonly Color clrSecondary = Color.FromArgb(76, 29, 149);
        private readonly Color clrBackground = Color.FromArgb(245, 245, 250);
        private readonly Color clrTextDark = Color.FromArgb(17, 24, 39);

        public EmployeeWorkspace(DatabaseService databaseService, AIService aiService, int employeeId)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _employeeId = employeeId;

            InitializeComponent();
            SetupModernContainer();
            SetupContentLayout(); // İçerik yerleşimi ve tıklama olayları burada

            this.Load += async (s, e) => await LoadDataAsync();
        }

        // --- YENİ EKLENEN YARDIMCI SINIF (JSON GÖRÜNTÜSÜNÜ DÜZELTİR) ---
        // Bu sınıf sayesinde ListBox'ta saçma yazılar çıkmaz, sadece başlık çıkar.
        public class TaskDisplayItem
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public TaskModel Task { get; set; }

            // ToString metodunu eziyoruz (Override)
            public override string ToString()
            {
                return $"• {Title}";
            }
        }

        private void SetupContentLayout()
        {
            // ... (Layout kodları aynen kalıyor, sadece en alta ekleme yaptım) ...
            var contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rightPanel.Controls.Add(contentLayout);

            // Toolbar
            var toolbarPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var btnRefresh = CreateModernButton("Yenile", "actions_refresh.svg", Color.FromArgb(0, 120, 212));
            var btnAI = CreateModernButton("AI Alt Görev Sihirbazı", "actions_add.svg", Color.FromArgb(34, 139, 34));
            btnRefresh.Margin = new Padding(0, 0, 15, 0);
            btnRefresh.Click += (s, e) => _ = LoadDataAsync();
            btnAI.Click += BtnBreakDown_Click;
            toolbarPanel.Controls.Add(btnRefresh);
            toolbarPanel.Controls.Add(btnAI);
            contentLayout.Controls.Add(toolbarPanel, 0, 0);

            // Kanban
            var kanbanLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 4 };
            kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kanbanLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kanbanLayout.Padding = new Padding(0, 10, 0, 0);

            lstPending = CreateKanbanList();
            lstInProgress = CreateKanbanList();
            lstCompleted = CreateKanbanList();
            txtBriefing = new MemoEdit { Properties = { ReadOnly = true }, Font = new Font("Segoe UI", 10), BackColor = Color.White };

            AddKanbanColumn(kanbanLayout, "⏳ BEKLEYEN", lstPending, 0, Color.FromArgb(245, 158, 11));
            AddKanbanColumn(kanbanLayout, "⏱️ YAPILIYOR", lstInProgress, 1, Color.FromArgb(59, 130, 246));
            AddKanbanColumn(kanbanLayout, "✅ TAMAMLANDI", lstCompleted, 2, Color.FromArgb(16, 185, 129));
            AddKanbanColumn(kanbanLayout, "💡 GÜNLÜK AKILLI BRİFİNG", txtBriefing, 3, Color.FromArgb(107, 114, 128));

            contentLayout.Controls.Add(kanbanLayout, 0, 1);

            // --- KRİTİK DÜZELTME: TIKLAMA OLAYLARINI BURADA BAĞLIYORUZ ---
            // Önceki kodda bu satırlar eksikti, o yüzden tıklayınca bir şey olmuyordu.
            AttachDoubleClickEvent(lstPending);
            AttachDoubleClickEvent(lstInProgress);
            AttachDoubleClickEvent(lstCompleted);
        }

        // ListBox'a çift tıklama özelliği kazandıran metod
        private void AttachDoubleClickEvent(ListBoxControl list)
        {
            list.DoubleClick += async (s, e) =>
            {
                var box = s as ListBoxControl;
                if (box.SelectedItem == null) return;

                // Seçilen öğeyi TaskDisplayItem olarak alıyoruz
                if (box.SelectedItem is TaskDisplayItem selectedItem)
                {
                    // Detay formunu aç
                    var detailForm = new TaskDetailForm(_databaseService, selectedItem.Id, _employeeId, "Çalışan");

                    if (detailForm.ShowDialog() == DialogResult.OK)
                    {
                        await LoadDataAsync(); // Listeleri yenile
                    }
                }
            };
        }

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
                        // JSON sorunu çözümü: TaskDisplayItem kullanıyoruz
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

        // --- DİĞER TASARIM METODLARI (Aynen Kalsın) ---

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

            var titleLabel = new LabelControl { Text = "Çalışan\nPaneli", Appearance = { Font = new Font("Segoe UI", 32, FontStyle.Bold), ForeColor = Color.White }, Location = new Point(40, 60), AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(250, 120), BackColor = Color.Transparent };
            leftPanel.Controls.Add(titleLabel);

            var subTitle = new LabelControl { Text = "Kişisel Görev ve\nPerformans Takibi", Appearance = { Font = new Font("Segoe UI", 12), ForeColor = Color.FromArgb(224, 231, 255) }, Location = new Point(45, 190), AutoSizeMode = LabelAutoSizeMode.None, Size = new Size(250, 50), BackColor = Color.Transparent };
            leftPanel.Controls.Add(subTitle);

            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrBackground, Padding = new Padding(30) };
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

        private ListBoxControl CreateKanbanList() => new ListBoxControl { BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple, Appearance = { Font = new Font("Segoe UI", 10), BackColor = Color.White, ForeColor = clrTextDark }, ItemHeight = 50 };

        private void AddKanbanColumn(TableLayoutPanel parent, string title, Control ctrl, int col, Color headerColor)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 15, 0) };
            if (col == 3) panel.Margin = new Padding(0);
            var header = new LabelControl { Text = title, Dock = DockStyle.Top, Height = 45, AutoSizeMode = LabelAutoSizeMode.None, Appearance = { BackColor = headerColor, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center } } };
            ctrl.Dock = DockStyle.Fill;
            panel.Controls.Add(ctrl);
            panel.Controls.Add(header);
            parent.Controls.Add(panel, col, 0);
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

        private async void BtnBreakDown_Click(object sender, EventArgs e)
        {
            string input = XtraInputBox.Show("AI Alt Görev Sihirbazı", "Planlamak istediğiniz görevi açıklayın:", "");
            if (string.IsNullOrWhiteSpace(input)) return;
            var subTasks = await _aiService.BreakDownTaskAsync(input);
            if (subTasks == null || !subTasks.Any()) return;
            string msg = "AI Önerilen Plan:\n\n" + string.Join("\n", subTasks.Select(s => $"• {s.Title}"));
            if (XtraMessageBox.Show(msg + "\n\nBu planı onaylıyor musunuz?", "AI Planlama", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                foreach (var s in subTasks) await _databaseService.CreateTaskAsync(new TaskModel { Title = s.Title, Description = s.Description, AssignedToId = _employeeId, Status = TaskStatusModel.Pending, CreatedDate = DateTime.Now });
                await LoadDataAsync();
            }
        }

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "EmployeeWorkspace"; this.ResumeLayout(false); }
    }
}