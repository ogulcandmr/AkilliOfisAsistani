using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using OfisAsistan.Models;
using OfisAsistan.Services;

namespace OfisAsistan.Forms
{
    public partial class CreateTaskForm : Form
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        private TextBox txtTitle;
        private TextBox txtDescription;
        private ComboBox cmbEmployee;
        private ComboBox cmbPriority;
        private ComboBox cmbDepartment;
        private DateTimePicker dtpDueDate;
        private Button btnSave;
        private Button btnAIRecommend;

        public CreateTaskForm(DatabaseService databaseService, AIService aiService)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            InitializeComponent();
            LoadEmployees();
        }

        private void InitializeComponent()
        {
            this.Text = "Yeni Görev Oluştur";
            this.Size = new Size(500, 400);
            this.StartPosition = FormStartPosition.CenterScreen;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8
            };

            mainPanel.Controls.Add(new Label { Text = "Başlık:", Dock = DockStyle.Fill }, 0, 0);
            txtTitle = new TextBox { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(txtTitle, 1, 0);

            mainPanel.Controls.Add(new Label { Text = "Açıklama:", Dock = DockStyle.Fill }, 0, 1);
            txtDescription = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60 };
            mainPanel.Controls.Add(txtDescription, 1, 1);

            mainPanel.Controls.Add(new Label { Text = "Çalışan:", Dock = DockStyle.Fill }, 0, 2);
            cmbEmployee = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
            mainPanel.Controls.Add(cmbEmployee, 1, 2);

            mainPanel.Controls.Add(new Label { Text = "Öncelik:", Dock = DockStyle.Fill }, 0, 3);
            cmbPriority = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPriority.Items.AddRange(new[] { "Düşük", "Normal", "Yüksek", "Kritik" });
            cmbPriority.SelectedIndex = 1;
            mainPanel.Controls.Add(cmbPriority, 1, 3);

            mainPanel.Controls.Add(new Label { Text = "Departman:", Dock = DockStyle.Fill }, 0, 4);
            cmbDepartment = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            mainPanel.Controls.Add(cmbDepartment, 1, 4);

            mainPanel.Controls.Add(new Label { Text = "Teslim Tarihi:", Dock = DockStyle.Fill }, 0, 5);
            dtpDueDate = new DateTimePicker { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(dtpDueDate, 1, 5);

            btnAIRecommend = new Button { Text = "AI Öneri Al", Dock = DockStyle.Fill };
            mainPanel.Controls.Add(btnAIRecommend, 0, 6);
            mainPanel.SetColumnSpan(btnAIRecommend, 2);

            btnSave = new Button { Text = "Kaydet", Dock = DockStyle.Fill, BackColor = Color.Green, ForeColor = Color.White };
            mainPanel.Controls.Add(btnSave, 0, 7);
            mainPanel.SetColumnSpan(btnSave, 2);

            this.Controls.Add(mainPanel);

            btnSave.Click += BtnSave_Click;
            btnAIRecommend.Click += BtnAIRecommend_Click;
        }

        private async void LoadEmployees()
        {
            try
            {
                var employees = await _databaseService.GetEmployeesForEmployeeRoleAsync();
                cmbEmployee.DataSource = null;
                cmbEmployee.Items.Clear();
                cmbEmployee.DataSource = employees;

                var departments = await _databaseService.GetDepartmentsAsync();
                cmbDepartment.Items.Clear();
                cmbDepartment.Items.AddRange(departments.Select(d => d.Name).ToArray());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                MessageBox.Show("Başlık gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedEmployee = cmbEmployee.SelectedItem as Employee;

            var task = new TaskModel
            {
                Title = txtTitle.Text,
                Description = txtDescription.Text,
                AssignedToId = selectedEmployee?.Id ?? 0,
                Priority = (TaskPriority)cmbPriority.SelectedIndex,
                DueDate = dtpDueDate.Value,
                CreatedDate = DateTime.Now,
                Status = TaskStatusModel.Pending
            };

            var result = await _databaseService.CreateTaskAsync(task);
            if (result != null)
            {
                MessageBox.Show("Görev oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Görev oluşturulamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnAIRecommend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                MessageBox.Show("Önce görev başlığını girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var task = new TaskModel
            {
                Title = txtTitle.Text,
                Description = txtDescription.Text,
                Priority = (TaskPriority)cmbPriority.SelectedIndex
            };

            var recommendation = await _aiService.RecommendEmployeeForTaskAsync(task);
            if (recommendation != null && recommendation.RecommendedEmployee != null)
            {
                // DataSource içindeki gerçek Employee nesnesini bul
                var employees = cmbEmployee.DataSource as System.Collections.IEnumerable;
                Employee recommended = null;
                if (employees != null)
                {
                    foreach (var item in employees)
                    {
                        if (item is Employee emp && emp.Id == recommendation.RecommendedEmployee.Id)
                        {
                            recommended = emp;
                            break;
                        }
                    }
                }

                if (recommended != null)
                    cmbEmployee.SelectedItem = recommended;

                MessageBox.Show($"AI Önerisi: {recommendation.RecommendedEmployee.FullName}\n\n{recommendation.Reason}",
                    "AI Önerisi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}

