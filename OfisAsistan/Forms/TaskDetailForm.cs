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
        private TextBox txtTitle;
        private TextBox txtDescription;
        private ComboBox cmbStatus;
        private ComboBox cmbPriority;
        private DateTimePicker dtpDueDate;
        private ComboBox cmbEmployee;
        private Button btnSave;

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
                    RowCount = 8
                };

                mainPanel.Controls.Add(new Label { Text = "Başlık:", Dock = DockStyle.Fill }, 0, 0);
                txtTitle = new TextBox { Dock = DockStyle.Fill, Text = _task.Title };
                mainPanel.Controls.Add(txtTitle, 1, 0);

                mainPanel.Controls.Add(new Label { Text = "Açıklama:", Dock = DockStyle.Fill }, 0, 1);
                txtDescription = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 80, Text = _task.Description ?? string.Empty };
                mainPanel.Controls.Add(txtDescription, 1, 1);

                mainPanel.Controls.Add(new Label { Text = "Durum:", Dock = DockStyle.Fill }, 0, 2);
                cmbStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbStatus.Items.AddRange(Enum.GetNames(typeof(TaskStatus)));
                cmbStatus.SelectedItem = _task.Status.ToString();
                mainPanel.Controls.Add(cmbStatus, 1, 2);

                mainPanel.Controls.Add(new Label { Text = "Öncelik:", Dock = DockStyle.Fill }, 0, 3);
                cmbPriority = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbPriority.Items.AddRange(Enum.GetNames(typeof(TaskPriority)));
                cmbPriority.SelectedItem = _task.Priority.ToString();
                mainPanel.Controls.Add(cmbPriority, 1, 3);

                mainPanel.Controls.Add(new Label { Text = "Teslim Tarihi:", Dock = DockStyle.Fill }, 0, 4);
                dtpDueDate = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm" };
                dtpDueDate.Value = _task.DueDate ?? DateTime.Now;
                mainPanel.Controls.Add(dtpDueDate, 1, 4);

                mainPanel.Controls.Add(new Label { Text = "Çalışan:", Dock = DockStyle.Fill }, 0, 5);
                cmbEmployee = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
                mainPanel.Controls.Add(cmbEmployee, 1, 5);

                btnSave = new Button { Text = "Kaydet", Dock = DockStyle.Fill, BackColor = Color.SteelBlue, ForeColor = Color.White };
                mainPanel.Controls.Add(btnSave, 0, 7);
                mainPanel.SetColumnSpan(btnSave, 2);

                btnSave.Click += async (s, e) => await SaveTaskAsync();

                this.Controls.Add(mainPanel);

                var employees = await _databaseService.GetEmployeesForEmployeeRoleAsync();
                cmbEmployee.DataSource = employees;
                var currentEmployee = employees.FirstOrDefault(emp => emp.Id == _task.AssignedToId);
                if (currentEmployee != null)
                    cmbEmployee.SelectedItem = currentEmployee;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task SaveTaskAsync()
        {
            if (_task == null)
                return;

            _task.Title = txtTitle.Text;
            _task.Description = txtDescription.Text;

            if (Enum.TryParse<TaskStatus>(cmbStatus.SelectedItem?.ToString(), out var status))
                _task.Status = status;

            if (Enum.TryParse<TaskPriority>(cmbPriority.SelectedItem?.ToString(), out var priority))
                _task.Priority = priority;

            _task.DueDate = dtpDueDate.Value;

            var selectedEmployee = cmbEmployee.SelectedItem as Employee;
            if (selectedEmployee != null)
                _task.AssignedToId = selectedEmployee.Id;

            var success = await _databaseService.UpdateTaskAsync(_task);
            if (success)
            {
                MessageBox.Show("Görev güncellendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Görev güncellenemedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

