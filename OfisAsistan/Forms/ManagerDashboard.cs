using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using OfisAsistan.Models;
using OfisAsistan.Services;

namespace OfisAsistan.Forms
{
    public partial class ManagerDashboard : Form
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        private NotificationService _notificationService;
        private DataGridView dgvTasks;
        private DataGridView dgvEmployees;
        private Label lblWorkload;
        private Label lblAnomalies;
        private Label lblTotalTasks;
        private Label lblPendingTasks;
        private Label lblInProgressTasks;
        private Label lblCompletedTasks;
        private Label lblOverdueTasks;
        private Button btnRefresh;
        private Button btnCreateTask;
        private Button btnAIRecommend;
        private TextBox txtTaskTitle;
        private ComboBox cmbPriority;
        private ComboBox cmbDepartment;
        private ListBox lstAnomalies;
        private Panel pnlCharts;

        public ManagerDashboard(DatabaseService databaseService, AIService aiService, NotificationService notificationService)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _notificationService = notificationService;
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "Yönetici Paneli";
            this.WindowState = FormWindowState.Maximized;
            this.Size = new Size(1200, 800);

            // Ana panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));

            // Görevler paneli
            var tasksPanel = new Panel { Dock = DockStyle.Fill };
            var tasksLabel = new Label { Text = "Görevler", Font = new Font("Arial", 12, FontStyle.Bold), Dock = DockStyle.Top, Height = 30 };
            dgvTasks = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            tasksPanel.Controls.Add(dgvTasks);
            tasksPanel.Controls.Add(tasksLabel);

            // Çalışanlar paneli
            var employeesPanel = new Panel { Dock = DockStyle.Fill };
            var employeesLabel = new Label { Text = "Çalışanlar ve İş Yükü", Font = new Font("Arial", 12, FontStyle.Bold), Dock = DockStyle.Top, Height = 30 };
            dgvEmployees = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            employeesPanel.Controls.Add(dgvEmployees);
            employeesPanel.Controls.Add(employeesLabel);

            // Anomali paneli
            var anomaliesPanel = new Panel { Dock = DockStyle.Fill };
            var anomaliesLabel = new Label { Text = "Anomali Tespitleri", Font = new Font("Arial", 12, FontStyle.Bold), Dock = DockStyle.Top, Height = 30 };
            lstAnomalies = new ListBox { Dock = DockStyle.Fill };
            anomaliesPanel.Controls.Add(lstAnomalies);
            anomaliesPanel.Controls.Add(anomaliesLabel);

            // Grafikler paneli (İstatistik kartları için placeholder)
            pnlCharts = new Panel { Dock = DockStyle.Fill, BackColor = Color.LightGray };
            var chartsLabel = new Label { Text = "Genel Görev İstatistikleri", Font = new Font("Arial", 12, FontStyle.Bold), Dock = DockStyle.Top, Height = 30 };

            var statsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            statsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

            lblTotalTasks = new Label { Dock = DockStyle.Fill, Font = new Font("Arial", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            lblPendingTasks = new Label { Dock = DockStyle.Fill, Font = new Font("Arial", 10), TextAlign = ContentAlignment.MiddleLeft };
            lblInProgressTasks = new Label { Dock = DockStyle.Fill, Font = new Font("Arial", 10), TextAlign = ContentAlignment.MiddleLeft };
            lblCompletedTasks = new Label { Dock = DockStyle.Fill, Font = new Font("Arial", 10), TextAlign = ContentAlignment.MiddleLeft };
            lblOverdueTasks = new Label { Dock = DockStyle.Fill, Font = new Font("Arial", 10), TextAlign = ContentAlignment.MiddleLeft };

            statsLayout.Controls.Add(lblTotalTasks, 0, 0);
            statsLayout.Controls.Add(lblPendingTasks, 0, 1);
            statsLayout.Controls.Add(lblInProgressTasks, 0, 2);
            statsLayout.Controls.Add(lblCompletedTasks, 0, 3);
            statsLayout.Controls.Add(lblOverdueTasks, 0, 4);

            pnlCharts.Controls.Add(statsLayout);
            pnlCharts.Controls.Add(chartsLabel);

            // Butonlar paneli
            var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50 };
            btnRefresh = new Button { Text = "Yenile", Size = new Size(100, 40) };
            btnCreateTask = new Button { Text = "Yeni Görev", Size = new Size(100, 40) };
            btnAIRecommend = new Button { Text = "AI Öneri", Size = new Size(100, 40) };
            buttonsPanel.Controls.Add(btnRefresh);
            buttonsPanel.Controls.Add(btnCreateTask);
            buttonsPanel.Controls.Add(btnAIRecommend);

            // Görev oluşturma paneli
            var createTaskPanel = new Panel { Dock = DockStyle.Fill };
            var createLabel = new Label { Text = "Yeni Görev Oluştur", Font = new Font("Arial", 10, FontStyle.Bold), Dock = DockStyle.Top, Height = 25 };
            txtTaskTitle = new TextBox { Dock = DockStyle.Top, Height = 30 };
            cmbPriority = new ComboBox { Dock = DockStyle.Top, Height = 30 };
            cmbPriority.Items.AddRange(new[] { "Düşük", "Normal", "Yüksek", "Kritik" });
            cmbDepartment = new ComboBox { Dock = DockStyle.Top, Height = 30 };
            var btnSaveTask = new Button { Text = "Kaydet", Dock = DockStyle.Top, Height = 40 };
            createTaskPanel.Controls.Add(btnSaveTask);
            createTaskPanel.Controls.Add(cmbDepartment);
            createTaskPanel.Controls.Add(cmbPriority);
            createTaskPanel.Controls.Add(txtTaskTitle);
            createTaskPanel.Controls.Add(createLabel);

            mainPanel.Controls.Add(tasksPanel, 0, 0);
            mainPanel.Controls.Add(employeesPanel, 1, 0);
            mainPanel.Controls.Add(anomaliesPanel, 0, 1);
            mainPanel.Controls.Add(pnlCharts, 1, 1);

            this.Controls.Add(mainPanel);
            this.Controls.Add(buttonsPanel);

            // Event handlers
            btnRefresh.Click += BtnRefresh_Click;
            btnCreateTask.Click += BtnCreateTask_Click;
            btnAIRecommend.Click += BtnAIRecommend_Click;
            btnSaveTask.Click += BtnSaveTask_Click;
            dgvTasks.CellDoubleClick += DgvTasks_CellDoubleClick;
        }

        private async void LoadData()
        {
            try
            {
                // Çalışanları yükle (görev tablosunda isim göstermek için önce al)
                var employees = await _databaseService.GetEmployeesAsync();
                var employeeLookup = employees.ToDictionary(e => e.Id, e => e.FullName);

                // Görevleri yükle
                var tasks = await _databaseService.GetTasksAsync();
                dgvTasks.DataSource = tasks.Select(t => new
                {
                    t.Id,
                    Başlık = t.Title,
                    Durum = t.Status.ToString(),
                    Öncelik = t.Priority.ToString(),
                    Teslim = t.DueDate?.ToString("dd.MM.yyyy") ?? "-",
                    Atanan = (t.AssignedToId > 0 && employeeLookup.ContainsKey(t.AssignedToId)) ? employeeLookup[t.AssignedToId] : "-"
                }).ToList();

                // Çalışanları grid’e bağla
                dgvEmployees.DataSource = employees.Select(e => new
                {
                    e.Id,
                    Ad = e.FullName,
                    Departman = e.DepartmentId,
                    İşYükü = $"{e.CurrentWorkload}/{e.MaxWorkload}",
                    Yüzde = $"{e.WorkloadPercentage:F1}%"
                }).ToList();

                // İş yükü renklendirme (Heatmap için placeholder)
                ColorizeWorkload();

                // Anomalileri yükle
                await LoadAnomalies();

                // Genel istatistikleri yükle
                var stats = await _databaseService.GetTaskStatisticsAsync();
                if (stats != null)
                {
                    lblTotalTasks.Text = $"Toplam Görev: {stats["Total"]}";
                    lblPendingTasks.Text = $"Bekleyen: {stats["Pending"]}";
                    lblInProgressTasks.Text = $"Devam Eden: {stats["InProgress"]}";
                    lblCompletedTasks.Text = $"Tamamlanan: {stats["Completed"]}";
                    lblOverdueTasks.Text = $"Gecikmiş: {stats["Overdue"]}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void ColorizeWorkload()
        {
            // DevExpress Heatmap için placeholder
            // Gerçek implementasyonda DevExpress Heatmap kontrolü kullanılacak
            foreach (DataGridViewRow row in dgvEmployees.Rows)
            {
                if (row.Cells["Yüzde"].Value != null)
                {
                    var percentage = double.Parse(row.Cells["Yüzde"].Value.ToString().Replace("%", ""));
                    if (percentage > 80)
                        row.DefaultCellStyle.BackColor = Color.Red;
                    else if (percentage > 60)
                        row.DefaultCellStyle.BackColor = Color.Orange;
                    else if (percentage > 40)
                        row.DefaultCellStyle.BackColor = Color.Yellow;
                    else
                        row.DefaultCellStyle.BackColor = Color.LightGreen;
                }
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            LoadData();
        }

        private void BtnCreateTask_Click(object sender, EventArgs e)
        {
            // Görev oluşturma formunu göster
            var createForm = new CreateTaskForm(_databaseService, _aiService);
            createForm.ShowDialog();
            LoadData();
        }

        private async void BtnAIRecommend_Click(object sender, EventArgs e)
        {
            if (dgvTasks.SelectedRows.Count == 0)
            {
                MessageBox.Show("Lütfen bir görev seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var taskId = (int)dgvTasks.SelectedRows[0].Cells["Id"].Value;
            var tasks = await _databaseService.GetTasksAsync();
            var task = tasks.FirstOrDefault(t => t.Id == taskId);

            if (task == null)
            {
                MessageBox.Show("Görev bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var recommendation = await _aiService.RecommendEmployeeForTaskAsync(task);
            if (recommendation != null && recommendation.RecommendedEmployee != null)
            {
                var message = $"Önerilen: {recommendation.RecommendedEmployee.FullName}\n\n{recommendation.Reason}\n\n" +
                              "Bu çalışanı göreve atamak ister misiniz?";

                var result = MessageBox.Show(message, "AI Önerisi", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    var previousAssignedToId = task.AssignedToId;
                    task.AssignedToId = recommendation.RecommendedEmployee.Id;
                    var updated = await _databaseService.UpdateTaskAsync(task, previousAssignedToId);
                    if (updated)
                    {
                        MessageBox.Show("Görev AI önerisine göre güncellendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadData();
                    }
                    else
                    {
                        MessageBox.Show("Görev güncellenemedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void BtnSaveTask_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTaskTitle.Text))
            {
                MessageBox.Show("Görev başlığı gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var task = new TaskModel
            {
                Title = txtTaskTitle.Text,
                Priority = (TaskPriority)cmbPriority.SelectedIndex,
                DepartmentId = cmbDepartment.SelectedIndex + 1,
                CreatedDate = DateTime.Now,
                Status = TaskStatus.Pending
            };

            await _databaseService.CreateTaskAsync(task);
            MessageBox.Show("Görev oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadData();
        }

        private void DgvTasks_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // Görev detayını göster
            if (e.RowIndex >= 0)
            {
                var taskId = (int)dgvTasks.Rows[e.RowIndex].Cells["Id"].Value;
                var detailForm = new TaskDetailForm(_databaseService, taskId);
                detailForm.ShowDialog();
                LoadData();
            }
        }
    }
}

