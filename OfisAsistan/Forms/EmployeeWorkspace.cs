using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using OfisAsistan.Models;
using OfisAsistan.Services;

namespace OfisAsistan.Forms
{
    public partial class EmployeeWorkspace : Form
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        private int _employeeId;
        private ListBox lstPending;
        private ListBox lstInProgress;
        private ListBox lstCompleted;
        private Label lblBriefing;
        private Button btnRefresh;
        private Button btnBreakDown;
        private TextBox txtBriefing;
        private ContextMenuStrip pendingMenu;
        private ContextMenuStrip inProgressMenu;
        private ContextMenuStrip completedMenu;

        public EmployeeWorkspace(DatabaseService databaseService, AIService aiService, int employeeId)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _employeeId = employeeId;
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "Çalışan Paneli";
            this.WindowState = FormWindowState.Maximized;
            this.Size = new Size(1200, 800);

            // Ana panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            // Kanban paneli
            var kanbanPanel = new Panel { Dock = DockStyle.Fill };
            var kanbanLabel = new Label { Text = "Kanban Panosu", Font = new Font("Arial", 12, FontStyle.Bold), Dock = DockStyle.Top, Height = 30 };

            var kanbanTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            kanbanTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            kanbanTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            kanbanTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            // Bekliyor sütunu
            var pendingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.LightGray };
            var pendingLabel = new Label { Text = "Bekliyor", Font = new Font("Arial", 10, FontStyle.Bold), Dock = DockStyle.Top, Height = 25 };
            lstPending = new ListBox { Dock = DockStyle.Fill };
            pendingPanel.Controls.Add(lstPending);
            pendingPanel.Controls.Add(pendingLabel);

            // Yapılıyor sütunu
            var inProgressPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.LightYellow };
            var inProgressLabel = new Label { Text = "Yapılıyor", Font = new Font("Arial", 10, FontStyle.Bold), Dock = DockStyle.Top, Height = 25 };
            lstInProgress = new ListBox { Dock = DockStyle.Fill };
            inProgressPanel.Controls.Add(lstInProgress);
            inProgressPanel.Controls.Add(inProgressLabel);

            // Tamamlandı sütunu
            var completedPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.LightGreen };
            var completedLabel = new Label { Text = "Tamamlandı", Font = new Font("Arial", 10, FontStyle.Bold), Dock = DockStyle.Top, Height = 25 };
            lstCompleted = new ListBox { Dock = DockStyle.Fill };
            completedPanel.Controls.Add(lstCompleted);
            completedPanel.Controls.Add(completedLabel);

            kanbanTable.Controls.Add(pendingPanel, 0, 0);
            kanbanTable.Controls.Add(inProgressPanel, 1, 0);
            kanbanTable.Controls.Add(completedPanel, 2, 0);

            kanbanPanel.Controls.Add(kanbanTable);
            kanbanPanel.Controls.Add(kanbanLabel);

            // Brifing paneli
            var briefingPanel = new Panel { Dock = DockStyle.Fill };
            var briefingLabel = new Label { Text = "Günlük Brifing", Font = new Font("Arial", 12, FontStyle.Bold), Dock = DockStyle.Top, Height = 30 };
            txtBriefing = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            briefingPanel.Controls.Add(txtBriefing);
            briefingPanel.Controls.Add(briefingLabel);

            // Butonlar
            var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50 };
            btnRefresh = new Button { Text = "Yenile", Size = new Size(100, 40) };
            btnBreakDown = new Button { Text = "AI Alt Görev", Size = new Size(120, 40) };
            buttonsPanel.Controls.Add(btnRefresh);
            buttonsPanel.Controls.Add(btnBreakDown);

            mainPanel.Controls.Add(kanbanPanel, 0, 0);
            mainPanel.Controls.Add(briefingPanel, 1, 0);

            this.Controls.Add(mainPanel);
            this.Controls.Add(buttonsPanel);

            // Context menüler
            pendingMenu = new ContextMenuStrip();
            var miStart = new ToolStripMenuItem("Başlat");
            miStart.Click += async (s, e) => await ChangeStatusFromMenuAsync(lstPending, TaskStatusModel.InProgress);
            pendingMenu.Items.Add(miStart);

            inProgressMenu = new ContextMenuStrip();
            var miCompleteFromInProgress = new ToolStripMenuItem("Tamamla");
            miCompleteFromInProgress.Click += async (s, e) => await ChangeStatusFromMenuAsync(lstInProgress, TaskStatusModel.Completed);
            inProgressMenu.Items.Add(miCompleteFromInProgress);

            completedMenu = new ContextMenuStrip();
            var miCompleteFromCompleted = new ToolStripMenuItem("Tamamla");
            miCompleteFromCompleted.Click += async (s, e) => await ChangeStatusFromMenuAsync(lstCompleted, TaskStatusModel.Completed);
            completedMenu.Items.Add(miCompleteFromCompleted);

            lstPending.ContextMenuStrip = pendingMenu;
            lstInProgress.ContextMenuStrip = inProgressMenu;
            lstCompleted.ContextMenuStrip = completedMenu;

            // Event handlers
            btnRefresh.Click += BtnRefresh_Click;
            btnBreakDown.Click += BtnBreakDown_Click;
            lstPending.MouseDown += ListBox_MouseDown;
            lstInProgress.MouseDown += ListBox_MouseDown;
            lstCompleted.MouseDown += ListBox_MouseDown;
            lstPending.DragOver += ListBox_DragOver;
            lstInProgress.DragOver += ListBox_DragOver;
            lstCompleted.DragOver += ListBox_DragOver;
            lstPending.DragDrop += ListBox_DragDrop;
            lstInProgress.DragDrop += ListBox_DragDrop;
            lstCompleted.DragDrop += ListBox_DragDrop;
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
                    var displayText = $"{task.Id}: {task.Title}";
                    switch (task.Status)
                    {
                        case TaskStatusModel.Pending:
                            lstPending.Items.Add(new TaskItem { Task = task, DisplayText = displayText });
                            break;
                        case TaskStatusModel.InProgress:
                            lstInProgress.Items.Add(new TaskItem { Task = task, DisplayText = displayText });
                            break;
                        case TaskStatusModel.Completed:
                            lstCompleted.Items.Add(new TaskItem { Task = task, DisplayText = displayText });
                            break;
                    }
                }

                // Brifing yükle
                await LoadBriefing();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadBriefing()
        {
            try
            {
                var briefing = await _aiService.GenerateDailyBriefingAsync(_employeeId);
                txtBriefing.Text = briefing;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadBriefing Error: {ex.Message}");
                txtBriefing.Text = "Brifing yüklenemedi.";
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            LoadData();
        }

        private async void BtnBreakDown_Click(object sender, EventArgs e)
        {
            var inputForm = new Form
            {
                Text = "AI Alt Görev Sihirbazı",
                Size = new Size(400, 150),
                StartPosition = FormStartPosition.CenterParent
            };
            var txtInput = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60 };
            var btnOK = new Button { Text = "Tamam", Dock = DockStyle.Bottom, Height = 30, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "İptal", Dock = DockStyle.Bottom, Height = 30, DialogResult = DialogResult.Cancel };
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
            panel.Controls.Add(btnOK);
            panel.Controls.Add(btnCancel);
            inputForm.Controls.Add(txtInput);
            inputForm.Controls.Add(panel);
            inputForm.AcceptButton = btnOK;
            inputForm.CancelButton = btnCancel;

            if (inputForm.ShowDialog() != DialogResult.OK)
                return;

            var input = txtInput.Text;
            if (string.IsNullOrWhiteSpace(input))
                return;

            try
            {
                var subTasks = await _aiService.BreakDownTaskAsync(input);
                var message = "Oluşturulan alt görevler:\n\n";
                foreach (var subTask in subTasks)
                {
                    message += $"{subTask.Order}. {subTask.Title} ({subTask.EstimatedHours} saat)\n";
                }
                MessageBox.Show(message, "Alt Görevler", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ListBox_MouseDown(object sender, MouseEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox.SelectedItem != null)
            {
                listBox.DoDragDrop(listBox.SelectedItem, DragDropEffects.Move);
            }
        }

        private void ListBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private async void ListBox_DragDrop(object sender, DragEventArgs e)
        {
            var targetListBox = sender as ListBox;
            var taskItem = e.Data.GetData(typeof(TaskItem)) as TaskItem;

            if (taskItem == null) return;

            TaskStatusModel newStatus;
            if (targetListBox == lstPending)
                newStatus = TaskStatusModel.Pending;
            else if (targetListBox == lstInProgress)
                newStatus = TaskStatusModel.InProgress;
            else if (targetListBox == lstCompleted)
                newStatus = TaskStatusModel.Completed;
            else
                return;

            // Eski listeden kaldır
            if (lstPending.Items.Contains(taskItem))
                lstPending.Items.Remove(taskItem);
            else if (lstInProgress.Items.Contains(taskItem))
                lstInProgress.Items.Remove(taskItem);
            else if (lstCompleted.Items.Contains(taskItem))
                lstCompleted.Items.Remove(taskItem);

            // Yeni listeye ekle
            taskItem.Task.Status = newStatus;
            if (newStatus == TaskStatusModel.Completed)
                taskItem.Task.CompletedDate = DateTime.Now;

            await _databaseService.UpdateTaskAsync(taskItem.Task);
            targetListBox.Items.Add(taskItem);

            LoadData();
        }

        private async System.Threading.Tasks.Task ChangeStatusFromMenuAsync(ListBox listBox, TaskStatusModel newStatus)
        {
            var taskItem = listBox.SelectedItem as TaskItem;
            if (taskItem == null)
                return;

            taskItem.Task.Status = newStatus;
            if (newStatus == TaskStatusModel.Completed)
                taskItem.Task.CompletedDate = DateTime.Now;

            await _databaseService.UpdateTaskAsync(taskItem.Task);
            LoadData();
        }

        private class TaskItem
        {
            public TaskModel Task { get; set; }
            public string DisplayText { get; set; }
            public override string ToString() => DisplayText;
        }
    }
}

