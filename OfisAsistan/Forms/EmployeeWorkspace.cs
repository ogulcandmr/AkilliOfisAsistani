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
using DevExpress.XtraBars;

namespace OfisAsistan.Forms
{
    public partial class EmployeeWorkspace : XtraForm
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        private int _employeeId;

        private ListBoxControl lstPending;
        private ListBoxControl lstInProgress;
        private ListBoxControl lstCompleted;
        private MemoEdit txtBriefing;
        private SimpleButton btnRefresh;
        private SimpleButton btnBreakDown;

        private LayoutControl layoutControl;

        public EmployeeWorkspace(DatabaseService databaseService, AIService aiService, int employeeId)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _employeeId = employeeId;
            InitializeComponent();
            SetupDevExpressUI();
            this.Load += EmployeeWorkspace_Load;
        }

        private void EmployeeWorkspace_Load(object sender, EventArgs e)
        {
            LoadData();
        }

        private void SetupDevExpressUI()
        {
            this.Text = "Çalışan Çalışma Alanı";
            this.WindowState = FormWindowState.Maximized;

            // Create toolbar panel first
            var toolbarPanel = new DevExpress.XtraEditors.PanelControl
            {
                Height = 60,
                Dock = DockStyle.Top,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
            };

            // Buttons
            btnRefresh = new SimpleButton { Text = "Yenile", ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("actions_refresh.svg") } };
            btnBreakDown = new SimpleButton { Text = "AI Alt Görev", ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("actions_add.svg") } };

            btnRefresh.Size = new Size(120, 40);
            btnRefresh.Location = new Point(10, 10);
            
            btnBreakDown.Size = new Size(150, 40);
            btnBreakDown.Location = new Point(140, 10);

            toolbarPanel.Controls.Add(btnRefresh);
            toolbarPanel.Controls.Add(btnBreakDown);

            this.Controls.Add(toolbarPanel);

            // Create main layout control
            layoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(layoutControl);

            // Lists
            lstPending = CreateKanbanList("lstPending");
            lstInProgress = CreateKanbanList("lstInProgress");
            lstCompleted = CreateKanbanList("lstCompleted");

            // Briefing
            txtBriefing = new MemoEdit
            {
                Properties = { ReadOnly = true },
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(250, 250, 250),
                MinimumSize = new Size(250, 200)
            };

            // Create 4 PanelControls for each kanban column + briefing
            var panelPending = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(200, 300)
            };
            var lblPendingHeader = new LabelControl
            {
                Text = "⏳ BEKLİYOR",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center } },
                Padding = new Padding(5),
                Height = 35,
                BackColor = Color.FromArgb(255, 243, 205)
            };
            lstPending.Dock = DockStyle.Fill;
            panelPending.Controls.Add(lstPending);
            panelPending.Controls.Add(lblPendingHeader);

            var panelInProgress = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(200, 300)
            };
            var lblInProgressHeader = new LabelControl
            {
                Text = "⏱️ YAPILIYOR",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center } },
                Padding = new Padding(5),
                Height = 35,
                BackColor = Color.FromArgb(173, 216, 230)
            };
            lstInProgress.Dock = DockStyle.Fill;
            panelInProgress.Controls.Add(lstInProgress);
            panelInProgress.Controls.Add(lblInProgressHeader);

            var panelCompleted = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(200, 300)
            };
            var lblCompletedHeader = new LabelControl
            {
                Text = "✅ TAMAMLANDI",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center } },
                Padding = new Padding(5),
                Height = 35,
                BackColor = Color.FromArgb(144, 238, 144)
            };
            lstCompleted.Dock = DockStyle.Fill;
            panelCompleted.Controls.Add(lstCompleted);
            panelCompleted.Controls.Add(lblCompletedHeader);

            var panelBriefing = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(250, 300)
            };
            var lblBriefingHeader = new LabelControl
            {
                Text = "💡 GÜNLÜK BRİFİNG",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Near } },
                Padding = new Padding(5),
                Height = 35
            };
            txtBriefing.Dock = DockStyle.Fill;
            panelBriefing.Controls.Add(txtBriefing);
            panelBriefing.Controls.Add(lblBriefingHeader);

            // Layout Construction using LayoutControl
            var root = layoutControl.Root;
            root.GroupBordersVisible = false;
            root.Padding = new DevExpress.XtraLayout.Utils.Padding(10);

            // Create table layout group
            var mainGroup = root.AddGroup();
            mainGroup.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            mainGroup.GroupBordersVisible = false;
            
            mainGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 25 });
            mainGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 25 });
            mainGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 25 });
            mainGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 25 });
            mainGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.Percent, Height = 100 });

            // Add panels as LayoutControlItems
            var itemPending = new LayoutControlItem(layoutControl, panelPending);
            itemPending.TextVisible = false;
            itemPending.OptionsTableLayoutItem.RowIndex = 0;
            itemPending.OptionsTableLayoutItem.ColumnIndex = 0;
            mainGroup.AddItem(itemPending);

            var itemInProgress = new LayoutControlItem(layoutControl, panelInProgress);
            itemInProgress.TextVisible = false;
            itemInProgress.OptionsTableLayoutItem.RowIndex = 0;
            itemInProgress.OptionsTableLayoutItem.ColumnIndex = 1;
            mainGroup.AddItem(itemInProgress);

            var itemCompleted = new LayoutControlItem(layoutControl, panelCompleted);
            itemCompleted.TextVisible = false;
            itemCompleted.OptionsTableLayoutItem.RowIndex = 0;
            itemCompleted.OptionsTableLayoutItem.ColumnIndex = 2;
            mainGroup.AddItem(itemCompleted);

            var itemBriefing = new LayoutControlItem(layoutControl, panelBriefing);
            itemBriefing.TextVisible = false;
            itemBriefing.OptionsTableLayoutItem.RowIndex = 0;
            itemBriefing.OptionsTableLayoutItem.ColumnIndex = 3;
            mainGroup.AddItem(itemBriefing);

            // Events
            btnRefresh.Click += BtnRefresh_Click;
            btnBreakDown.Click += BtnBreakDown_Click;
            
            lstPending.DoubleClick += ListBox_DoubleClick;
            lstInProgress.DoubleClick += ListBox_DoubleClick;
            lstCompleted.DoubleClick += ListBox_DoubleClick;
        }

        private ListBoxControl CreateKanbanList(string name)
        {
            var list = new ListBoxControl
            {
                Name = name,
                AllowDrop = true,
                Appearance = { Font = new Font("Segoe UI", 10) },
                ItemHeight = 40
            };
            return list;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "EmployeeWorkspace";
            this.ResumeLayout(false);
        }

        private async void LoadData()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync(_employeeId);
                lstPending.Items.Clear();
                lstInProgress.Items.Clear();
                lstCompleted.Items.Clear();

                foreach (var task in tasks)
                {
                    var item = new TaskItem { Task = task, DisplayText = $"{task.Id}: {task.Title}" };
                    if (task.Status == TaskStatusModel.Pending) lstPending.Items.Add(item);
                    else if (task.Status == TaskStatusModel.InProgress) lstInProgress.Items.Add(item);
                    else if (task.Status == TaskStatusModel.Completed) lstCompleted.Items.Add(item);
                }
                
                lstPending.Refresh();
                lstInProgress.Refresh();
                lstCompleted.Refresh();
                
                await LoadBriefing(tasks);
                
                layoutControl.PerformLayout();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadBriefing(List<TaskModel> tasks)
        {
            try
            {
                var briefing = await _aiService.GenerateDailyBriefingAsync(_employeeId);
                txtBriefing.Text = (tasks == null || tasks.Count == 0)
                    ? "Not: Atanmış görev bulunmuyor.\r\n\r\n" + briefing
                    : briefing;
            }
            catch { txtBriefing.Text = "Brifing yüklenemedi."; }
        }

        private void BtnRefresh_Click(object sender, EventArgs e) => LoadData();

        private void ListBox_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                var lb = sender as ListBoxControl;
                if (lb?.SelectedItem is TaskItem item)
                {
                    var detailForm = new TaskDetailForm(_databaseService, item.Task.Id);
                    if (detailForm.ShowDialog() == DialogResult.OK)
                    {
                        LoadData();
                    }
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Görev detayı açılırken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnBreakDown_Click(object sender, EventArgs e)
        {
            try
            {
                string input = XtraInputBox.Show("AI Alt Görev Sihirbazı", "Görev Açıklaması Giriniz:", "");
                if (string.IsNullOrWhiteSpace(input)) return;

                var subTasks = await _aiService.BreakDownTaskAsync(input);
                if (subTasks == null || subTasks.Count == 0)
                {
                    XtraMessageBox.Show("Alt görev oluşturulamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string msg = "Oluşturulan Alt Görevler:\n\n" + string.Join("\n", subTasks.Select(s => $"{s.Order}. {s.Title} ({s.EstimatedHours} saat)"));
                if (XtraMessageBox.Show(msg + "\n\nOluşturulsun mu?", "AI Önerisi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (var s in subTasks)
                    {
                        await _databaseService.CreateTaskAsync(new TaskModel {
                            Title = s.Title, Description = s.Description, EstimatedHours = s.EstimatedHours,
                            AssignedToId = _employeeId, Status = TaskStatusModel.Pending, CreatedDate = DateTime.Now, Priority = TaskPriority.Normal
                        });
                    }
                    XtraMessageBox.Show("Alt görevler oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Alt görev oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class TaskItem
        {
            public TaskModel Task { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() => DisplayText;
        }
    }
}
