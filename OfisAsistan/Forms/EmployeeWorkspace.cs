using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
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
        private LayoutControl mainLayoutControl;

        public EmployeeWorkspace(DatabaseService databaseService, AIService aiService, int employeeId)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _employeeId = employeeId;

            InitializeComponent();
            SetupModernUI();

            this.Load += async (s, e) => await LoadDataAsync();
        }

        private void SetupModernUI()
        {
            this.Text = "Çalışan Dijital Ofis Paneli";
            this.WindowState = FormWindowState.Maximized;
            this.Appearance.BackColor = Color.FromArgb(242, 245, 250);

            // 1. ÜST ARAÇ ÇUBUĞU
            var toolbar = new PanelControl
            {
                Height = 60,
                Dock = DockStyle.Top,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                BackColor = Color.White
            };

            var btnRefresh = CreateToolbarButton("Yenile", "actions_refresh.svg", Color.FromArgb(0, 120, 212));
            var btnBreakDown = CreateToolbarButton("AI Alt Görev Sihirbazı", "actions_add.svg", Color.FromArgb(34, 139, 34));

            btnRefresh.Location = new Point(15, 10);
            btnBreakDown.Location = new Point(145, 10);

            btnRefresh.Click += (s, e) => _ = LoadDataAsync();
            btnBreakDown.Click += BtnBreakDown_Click;

            toolbar.Controls.Add(btnRefresh);
            toolbar.Controls.Add(btnBreakDown);
            this.Controls.Add(toolbar);

            // 2. ANA LAYOUT
            mainLayoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(mainLayoutControl);
            mainLayoutControl.BeginUpdate();

            var root = mainLayoutControl.Root;
            root.GroupBordersVisible = false;
            root.Padding = new DevExpress.XtraLayout.Utils.Padding(15);

            // KANBAN SİSTEMİ (Table Layout)
            var kanbanGroup = root.AddGroup();
            kanbanGroup.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            kanbanGroup.GroupBordersVisible = false;

            // 4 Kolon: %23, %23, %23 (Kanban) ve %31 (Briefing)
            kanbanGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 23 });
            kanbanGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 23 });
            kanbanGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 23 });
            kanbanGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 31 });
            kanbanGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.Percent, Height = 100 });

            // Kontrollerin Hazırlanması
            lstPending = CreateKanbanList();
            lstInProgress = CreateKanbanList();
            lstCompleted = CreateKanbanList();
            txtBriefing = new MemoEdit { Properties = { ReadOnly = true }, Font = new Font("Segoe UI", 10.5f) };

            // Kolonları Yerleştir
            AddKanbanColumn(kanbanGroup, "⏳ BEKLEYEN", lstPending, 0, Color.FromArgb(255, 193, 7));
            AddKanbanColumn(kanbanGroup, "⏱️ YAPILIYOR", lstInProgress, 1, Color.FromArgb(0, 123, 255));
            AddKanbanColumn(kanbanGroup, "✅ TAMAMLANDI", lstCompleted, 2, Color.FromArgb(40, 167, 69));
            AddKanbanColumn(kanbanGroup, "💡 GÜNLÜK AKILLI BRİFİNG", txtBriefing, 3, Color.FromArgb(108, 117, 125));

            mainLayoutControl.EndUpdate();
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
                        var displayItem = new { Id = task.Id, Text = $"{task.Title}", Task = task };
                        if (task.Status == TaskStatusModel.Pending) lstPending.Items.Add(displayItem);
                        else if (task.Status == TaskStatusModel.InProgress) lstInProgress.Items.Add(displayItem);
                        else if (task.Status == TaskStatusModel.Completed) lstCompleted.Items.Add(displayItem);
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

        private SimpleButton CreateToolbarButton(string text, string svg, Color accent) => new SimpleButton
        {
            Text = text,
            Size = new Size(120, 40),
            ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage(svg), SvgImageSize = new Size(20, 20) },
            Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = accent },
            PaintStyle = DevExpress.XtraEditors.Controls.PaintStyles.Light
        };

        private ListBoxControl CreateKanbanList() => new ListBoxControl
        {
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
            Appearance = { Font = new Font("Segoe UI", 10), BackColor = Color.White },
            ItemHeight = 45,
            DisplayMember = "Text",
            ValueMember = "Id"
        };

        private void AddKanbanColumn(LayoutControlGroup group, string title, Control ctrl, int col, Color headerColor)
        {
            var panel = new PanelControl { BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple };
            var header = new LabelControl
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 40,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = {
                    BackColor = headerColor,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    TextOptions = { HAlignment = HorzAlignment.Center }
                }
            };
            ctrl.Dock = DockStyle.Fill;
            panel.Controls.Add(ctrl);
            panel.Controls.Add(header);

            var item = group.AddItem("", panel);
            item.TextVisible = false;
            item.OptionsTableLayoutItem.ColumnIndex = col;
            item.Padding = new DevExpress.XtraLayout.Utils.Padding(5);
        }

        private async void BtnBreakDown_Click(object sender, EventArgs e)
        {
            string input = XtraInputBox.Show("AI Alt Görev Sihirbazı", "Planlamak istediğiniz görevi açıklayın:", "");
            if (string.IsNullOrWhiteSpace(input)) return;

            var subTasks = await _aiService.BreakDownTaskAsync(input);
            if (subTasks == null) return;

            string msg = "AI Önerilen Plan:\n\n" + string.Join("\n", subTasks.Select(s => $"• {s.Title}"));
            if (XtraMessageBox.Show(msg + "\n\nBu planı onaylıyor musunuz?", "AI Planlama", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                foreach (var s in subTasks)
                {
                    await _databaseService.CreateTaskAsync(new TaskModel
                    {
                        Title = s.Title,
                        Description = s.Description,
                        AssignedToId = _employeeId,
                        Status = TaskStatusModel.Pending,
                        CreatedDate = DateTime.Now
                    });
                }
                await LoadDataAsync();
            }
        }

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "EmployeeWorkspace"; this.Size = new Size(1200, 800); this.ResumeLayout(false); }
    }
}