using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using OfisAsistan.Models;
using OfisAsistan.Services;

namespace OfisAsistan.Forms
{
    public partial class TaskDetailForm : Form
    {
        private DatabaseService _databaseService;
        private int _taskId;
        private TaskModel _task;

        public TaskDetailForm(DatabaseService databaseService, int taskId)
        {
            _databaseService = databaseService;
            _taskId = taskId;
            InitializeComponent();
            LoadTask();
        }

        private void InitializeComponent()
        {
            this.Text = "Görev Detayı";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private async void LoadTask()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync();
                _task = tasks.FirstOrDefault(t => t.Id == _taskId);

                if (_task == null)
                {
                    MessageBox.Show("Görev bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }

                var mainPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 10
                };

                mainPanel.Controls.Add(new Label { Text = "Başlık:", Dock = DockStyle.Fill }, 0, 0);
                mainPanel.Controls.Add(new Label { Text = _task.Title, Dock = DockStyle.Fill, Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold) }, 1, 0);

                mainPanel.Controls.Add(new Label { Text = "Açıklama:", Dock = DockStyle.Fill }, 0, 1);
                mainPanel.Controls.Add(new Label { Text = _task.Description ?? "-", Dock = DockStyle.Fill }, 1, 1);

                mainPanel.Controls.Add(new Label { Text = "Durum:", Dock = DockStyle.Fill }, 0, 2);
                mainPanel.Controls.Add(new Label { Text = _task.Status.ToString(), Dock = DockStyle.Fill }, 1, 2);

                mainPanel.Controls.Add(new Label { Text = "Öncelik:", Dock = DockStyle.Fill }, 0, 3);
                mainPanel.Controls.Add(new Label { Text = _task.Priority.ToString(), Dock = DockStyle.Fill }, 1, 3);

                mainPanel.Controls.Add(new Label { Text = "Teslim Tarihi:", Dock = DockStyle.Fill }, 0, 4);
                mainPanel.Controls.Add(new Label { Text = _task.DueDate?.ToString("dd.MM.yyyy HH:mm") ?? "-", Dock = DockStyle.Fill }, 1, 4);

                mainPanel.Controls.Add(new Label { Text = "Oluşturulma:", Dock = DockStyle.Fill }, 0, 5);
                mainPanel.Controls.Add(new Label { Text = _task.CreatedDate.ToString("dd.MM.yyyy HH:mm"), Dock = DockStyle.Fill }, 1, 5);

                if (_task.CompletedDate.HasValue)
                {
                    mainPanel.Controls.Add(new Label { Text = "Tamamlanma:", Dock = DockStyle.Fill }, 0, 6);
                    mainPanel.Controls.Add(new Label { Text = _task.CompletedDate.Value.ToString("dd.MM.yyyy HH:mm"), Dock = DockStyle.Fill }, 1, 6);
                }

                var btnClose = new Button { Text = "Kapat", Dock = DockStyle.Fill };
                mainPanel.Controls.Add(btnClose, 0, 9);
                mainPanel.SetColumnSpan(btnClose, 2);

                btnClose.Click += (s, e) => this.Close();

                this.Controls.Add(mainPanel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

