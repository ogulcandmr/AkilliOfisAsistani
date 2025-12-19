using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.Utils;
using DevExpress.XtraBars;

namespace OfisAsistan.Forms
{
    public partial class ManagerDashboard : XtraForm
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        private NotificationService _notificationService;
        
        private GridControl gcTasks;
        private GridView gvTasks;
        private GridControl gcEmployees;
        private GridView gvEmployees;
        
        private LabelControl lblTotalTasks;
        private LabelControl lblPendingTasks;
        private LabelControl lblInProgressTasks;
        private LabelControl lblCompletedTasks;
        private LabelControl lblOverdueTasks;
        
        private SimpleButton btnRefresh;
        private SimpleButton btnCreateTask;
        private SimpleButton btnAIRecommend;
        
        private TextEdit txtTaskTitle;
        private ComboBoxEdit cmbPriority;
        private ComboBoxEdit cmbDepartment;
        private ListBoxControl lstAnomalies;
        
        private LayoutControl layoutControl;

        public ManagerDashboard(DatabaseService databaseService, AIService aiService, NotificationService notificationService)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _notificationService = notificationService;
            InitializeComponent();
            SetupDevExpressUI();
            this.Load += ManagerDashboard_Load;
        }

        private void ManagerDashboard_Load(object sender, EventArgs e)
        {
            LoadData();
        }

        private void SetupDevExpressUI()
        {
            this.Text = "Yönetici Kontrol Paneli";
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
            btnCreateTask = new SimpleButton { Text = "Yeni Görev", ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("actions_add.svg") } };
            btnAIRecommend = new SimpleButton { Text = "AI Öneri", ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("outlook%20inspired/pivottable.svg") } };

            btnRefresh.Size = new Size(100, 40);
            btnRefresh.Location = new Point(10, 10);
            
            btnCreateTask.Size = new Size(120, 40);
            btnCreateTask.Location = new Point(120, 10);
            
            btnAIRecommend.Size = new Size(120, 40);
            btnAIRecommend.Location = new Point(250, 10);

            toolbarPanel.Controls.Add(btnRefresh);
            toolbarPanel.Controls.Add(btnCreateTask);
            toolbarPanel.Controls.Add(btnAIRecommend);

            this.Controls.Add(toolbarPanel);

            // Create main layout control
            layoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(layoutControl);

            // Grids
            gcTasks = new GridControl { Name = "gcTasks", MinimumSize = new Size(200, 200) };
            gvTasks = new GridView(gcTasks) { Name = "gvTasks" };
            gcTasks.MainView = gvTasks;
            gvTasks.OptionsView.ShowGroupPanel = false;
            gvTasks.OptionsBehavior.Editable = false;

            gcEmployees = new GridControl { Name = "gcEmployees", MinimumSize = new Size(200, 200) };
            gvEmployees = new GridView(gcEmployees) { Name = "gvEmployees" };
            gcEmployees.MainView = gvEmployees;
            gvEmployees.OptionsView.ShowGroupPanel = false;
            gvEmployees.OptionsBehavior.Editable = false;

            // Stats Labels
            lblTotalTasks = CreateStatLabel("Toplam Görev: 0", true);
            lblPendingTasks = CreateStatLabel("Bekleyen: 0");
            lblInProgressTasks = CreateStatLabel("Devam Eden: 0");
            lblCompletedTasks = CreateStatLabel("Tamamlanan: 0");
            lblOverdueTasks = CreateStatLabel("Gecikmiş: 0");

            // Inputs
            txtTaskTitle = new TextEdit { Properties = { NullValuePrompt = "Görev Başlığı..." } };
            cmbPriority = new ComboBoxEdit();
            cmbPriority.Properties.Items.AddRange(new[] { "Düşük", "Normal", "Yüksek", "Kritik" });
            cmbDepartment = new ComboBoxEdit();
            var btnSaveTask = new SimpleButton { Text = "Hızlı Kaydet", Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold) } };

            lstAnomalies = new ListBoxControl { MinimumSize = new Size(200, 200) };

            // Create 4 separate PanelControls for each section
            var panelTasks = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(300, 250)
            };
            var lblTasksHeader = new LabelControl
            {
                Text = "📋 GÖREV LİSTESİ",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Near } },
                Padding = new Padding(5),
                Height = 30
            };
            gcTasks.Dock = DockStyle.Fill;
            panelTasks.Controls.Add(gcTasks);
            panelTasks.Controls.Add(lblTasksHeader);

            var panelEmployees = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(200, 250)
            };
            var lblEmployeesHeader = new LabelControl
            {
                Text = "👥 ÇALIŞAN İŞ YÜKÜ",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Near } },
                Padding = new Padding(5),
                Height = 30
            };
            gcEmployees.Dock = DockStyle.Fill;
            panelEmployees.Controls.Add(gcEmployees);
            panelEmployees.Controls.Add(lblEmployeesHeader);

            var panelAnomalies = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(300, 250)
            };
            var lblAnomaliesHeader = new LabelControl
            {
                Text = "⚠️ ANOMALİ TESPİTLERİ",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Near } },
                Padding = new Padding(5),
                Height = 30
            };
            lstAnomalies.Dock = DockStyle.Fill;
            panelAnomalies.Controls.Add(lstAnomalies);
            panelAnomalies.Controls.Add(lblAnomaliesHeader);

            var panelStats = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(200, 250)
            };
            var lblStatsHeader = new LabelControl
            {
                Text = "📊 İSTATİSTİKLER & HIZLI EKLE",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Near } },
                Padding = new Padding(5),
                Height = 30
            };

            // Stats section - stack labels vertically
            lblTotalTasks.Dock = DockStyle.Top;
            lblPendingTasks.Dock = DockStyle.Top;
            lblInProgressTasks.Dock = DockStyle.Top;
            lblCompletedTasks.Dock = DockStyle.Top;
            lblOverdueTasks.Dock = DockStyle.Top;
            
            var statsContainer = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Top,
                Height = 150,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
            };
            statsContainer.Controls.Add(lblOverdueTasks);
            statsContainer.Controls.Add(lblCompletedTasks);
            statsContainer.Controls.Add(lblInProgressTasks);
            statsContainer.Controls.Add(lblPendingTasks);
            statsContainer.Controls.Add(lblTotalTasks);

            // Quick add section
            var lblQuickAdd = new LabelControl
            {
                Text = "HIZLI GÖREV EKLE",
                Dock = DockStyle.Top,
                Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold) },
                Padding = new Padding(5),
                Height = 25
            };
            
            var lblTitle = new LabelControl { Text = "Başlık:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(5, 5, 0, 0) };
            txtTaskTitle.Dock = DockStyle.Top;
            var lblPriority = new LabelControl { Text = "Öncelik:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(5, 5, 0, 0) };
            cmbPriority.Dock = DockStyle.Top;
            btnSaveTask.Dock = DockStyle.Top;
            btnSaveTask.Height = 30;

            var quickAddContainer = new DevExpress.XtraEditors.PanelControl
            {
                Dock = DockStyle.Fill,
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder
            };
            quickAddContainer.Controls.Add(btnSaveTask);
            quickAddContainer.Controls.Add(cmbPriority);
            quickAddContainer.Controls.Add(lblPriority);
            quickAddContainer.Controls.Add(txtTaskTitle);
            quickAddContainer.Controls.Add(lblTitle);
            quickAddContainer.Controls.Add(lblQuickAdd);

            panelStats.Controls.Add(quickAddContainer);
            panelStats.Controls.Add(statsContainer);
            panelStats.Controls.Add(lblStatsHeader);

            // Setup LayoutControl with proper structure
            var root = layoutControl.Root;
            root.GroupBordersVisible = false;
            root.Padding = new DevExpress.XtraLayout.Utils.Padding(10);

            // Create content group with table layout
            var contentGroup = root.AddGroup();
            contentGroup.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            contentGroup.GroupBordersVisible = false;
            
            contentGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 65 });
            contentGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 35 });
            contentGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.Percent, Height = 50 });
            contentGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.Percent, Height = 50 });

            // Add panels as LayoutControlItems
            var itemTasks = new LayoutControlItem(layoutControl, panelTasks);
            itemTasks.TextVisible = false;
            itemTasks.OptionsTableLayoutItem.RowIndex = 0;
            itemTasks.OptionsTableLayoutItem.ColumnIndex = 0;
            contentGroup.AddItem(itemTasks);

            var itemEmployees = new LayoutControlItem(layoutControl, panelEmployees);
            itemEmployees.TextVisible = false;
            itemEmployees.OptionsTableLayoutItem.RowIndex = 0;
            itemEmployees.OptionsTableLayoutItem.ColumnIndex = 1;
            contentGroup.AddItem(itemEmployees);

            var itemAnomalies = new LayoutControlItem(layoutControl, panelAnomalies);
            itemAnomalies.TextVisible = false;
            itemAnomalies.OptionsTableLayoutItem.RowIndex = 1;
            itemAnomalies.OptionsTableLayoutItem.ColumnIndex = 0;
            contentGroup.AddItem(itemAnomalies);

            var itemStats = new LayoutControlItem(layoutControl, panelStats);
            itemStats.TextVisible = false;
            itemStats.OptionsTableLayoutItem.RowIndex = 1;
            itemStats.OptionsTableLayoutItem.ColumnIndex = 1;
            contentGroup.AddItem(itemStats);

            // Events
            btnRefresh.Click += BtnRefresh_Click;
            btnCreateTask.Click += BtnCreateTask_Click;
            btnAIRecommend.Click += BtnAIRecommend_Click;
            btnSaveTask.Click += BtnSaveTask_Click;
            gvTasks.DoubleClick += GvTasks_DoubleClick;
            gvEmployees.RowStyle += GvEmployees_RowStyle;
        }

        private LabelControl CreateStatLabel(string text, bool isHeader = false)
        {
            return new LabelControl
            {
                Text = text,
                Appearance = { 
                    Font = new Font("Segoe UI", isHeader ? 12 : 10, isHeader ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = isHeader ? Color.FromArgb(0, 120, 215) : Color.FromArgb(60, 60, 60)
                },
                Padding = new Padding(5)
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "ManagerDashboard";
            this.ResumeLayout(false);
        }

        private async void LoadData()
        {
            try
            {
                var employees = await _databaseService.GetEmployeesAsync();
                var employeeLookup = employees.ToDictionary(e => e.Id, e => e.FullName);

                var tasks = await _databaseService.GetTasksAsync();
                var taskList = tasks.Select(t => new
                {
                    t.Id,
                    Başlık = t.Title,
                    Durum = t.Status.ToString(),
                    Öncelik = t.Priority.ToString(),
                    Teslim = t.DueDate?.ToString("dd.MM.yyyy") ?? "-",
                    Atanan = (t.AssignedToId > 0 && employeeLookup.ContainsKey(t.AssignedToId)) ? employeeLookup[t.AssignedToId] : "-"
                }).ToList();
                
                gcTasks.DataSource = taskList;
                gcTasks.RefreshDataSource();

                var employeeList = employees.Select(e => new
                {
                    e.Id,
                    Ad = e.FullName,
                    İşYükü = $"{e.CurrentWorkload}/{e.MaxWorkload}",
                    Yüzde = e.WorkloadPercentage
                }).ToList();
                
                gcEmployees.DataSource = employeeList;
                gcEmployees.RefreshDataSource();

                await LoadAnomalies();

                var stats = await _databaseService.GetTaskStatisticsAsync();
                if (stats != null)
                {
                    lblTotalTasks.Text = $"Toplam Görev: {stats["Total"]}";
                    lblPendingTasks.Text = $"Bekleyen: {stats["Pending"]}";
                    lblInProgressTasks.Text = $"Devam Eden: {stats["InProgress"]}";
                    lblCompletedTasks.Text = $"Tamamlanan: {stats["Completed"]}";
                    lblOverdueTasks.Text = $"Gecikmiş: {stats["Overdue"]}";
                }
                
                layoutControl.PerformLayout();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Veri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadAnomalies()
        {
            try
            {
                var anomalies = await _aiService.DetectAnomaliesAsync();
                lstAnomalies.Items.Clear();
                foreach (var anomaly in anomalies)
                {
                    var taskPrefix = anomaly.Task != null && !string.IsNullOrWhiteSpace(anomaly.Task.Title)
                        ? $"Görev: {anomaly.Task.Title} - "
                        : string.Empty;

                    lstAnomalies.Items.Add($"[{anomaly.Severity}] {taskPrefix}{anomaly.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAnomalies Error: {ex.Message}");
            }
        }

        private void GvEmployees_RowStyle(object sender, RowStyleEventArgs e)
        {
            if (e.RowHandle >= 0)
            {
                var percentage = (double)gvEmployees.GetRowCellValue(e.RowHandle, "Yüzde");
                if (percentage > 80) e.Appearance.BackColor = Color.MistyRose;
                else if (percentage > 60) e.Appearance.BackColor = Color.OldLace;
                else e.Appearance.BackColor = Color.Honeydew;
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            LoadData();
        }

        private void BtnCreateTask_Click(object sender, EventArgs e)
        {
            try
            {
                var createForm = new CreateTaskForm(_databaseService, _aiService);
                if (createForm.ShowDialog() == DialogResult.OK)
                {
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Görev oluşturma formu açılırken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnAIRecommend_Click(object sender, EventArgs e)
        {
            if (gvTasks.FocusedRowHandle < 0)
            {
                XtraMessageBox.Show("Lütfen bir görev seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var taskId = (int)gvTasks.GetFocusedRowCellValue("Id");
            var tasks = await _databaseService.GetTasksAsync();
            var task = tasks.FirstOrDefault(t => t.Id == taskId);

            if (task == null) return;

            var recommendation = await _aiService.RecommendEmployeeForTaskAsync(task);
            if (recommendation != null && recommendation.RecommendedEmployee != null)
            {
                var message = $"Önerilen: {recommendation.RecommendedEmployee.FullName}\n\n{recommendation.Reason}\n\nBu çalışanı göreve atamak ister misiniz?";
                if (XtraMessageBox.Show(message, "AI Önerisi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var previousId = task.AssignedToId;
                    task.AssignedToId = recommendation.RecommendedEmployee.Id;
                    if (await _databaseService.UpdateTaskAsync(task, previousId))
                    {
                        XtraMessageBox.Show("Görev AI önerisine göre güncellendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadData();
                    }
                }
            }
        }

        private async void BtnSaveTask_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTaskTitle.Text))
            {
                XtraMessageBox.Show("Görev başlığı gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var task = new TaskModel
            {
                Title = txtTaskTitle.Text,
                Priority = (TaskPriority)cmbPriority.SelectedIndex,
                DepartmentId = 1, // Default
                CreatedDate = DateTime.Now,
                Status = TaskStatus.Pending
            };

            await _databaseService.CreateTaskAsync(task);
            XtraMessageBox.Show("Görev oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadData();
        }

        private void GvTasks_DoubleClick(object sender, EventArgs e)
        {
            if (gvTasks.FocusedRowHandle >= 0)
            {
                var taskId = (int)gvTasks.GetFocusedRowCellValue("Id");
                var detailForm = new TaskDetailForm(_databaseService, taskId);
                detailForm.ShowDialog();
                LoadData();
            }
        }
    }
}
