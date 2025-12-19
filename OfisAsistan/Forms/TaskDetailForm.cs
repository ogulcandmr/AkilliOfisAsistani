using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using DevExpress.Utils;

namespace OfisAsistan.Forms
{
    public partial class TaskDetailForm : XtraForm
    {
        private DatabaseService _databaseService;
        private int _taskId;
        private TaskModel _task;
        
        private TextEdit txtTitle;
        private MemoEdit txtDescription;
        private ComboBoxEdit cmbStatus;
        private ComboBoxEdit cmbPriority;
        private DateEdit deDueDate;
        private LookUpEdit lueEmployee;
        private SimpleButton btnSave;
        private LayoutControl layoutControl;

        public TaskDetailForm(DatabaseService databaseService, int taskId)
        {
            _databaseService = databaseService;
            _taskId = taskId;
            InitializeComponent();
            SetupDevExpressUI();
            this.Load += TaskDetailForm_Load;
        }

        private void TaskDetailForm_Load(object sender, EventArgs e)
        {
            LoadTask();
        }

        private void SetupDevExpressUI()
        {
            this.Text = "Görev Detay Bilgileri";
            this.Size = new Size(600, 550);
            this.StartPosition = FormStartPosition.CenterParent;

            layoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(layoutControl);

            txtTitle = new TextEdit();
            txtDescription = new MemoEdit { Height = 100 };
            
            cmbStatus = new ComboBoxEdit();
            cmbStatus.Properties.Items.AddRange(Enum.GetNames(typeof(OfisAsistan.Models.TaskStatus)));

            cmbPriority = new ComboBoxEdit();
            cmbPriority.Properties.Items.AddRange(Enum.GetNames(typeof(TaskPriority)));

            deDueDate = new DateEdit();
            deDueDate.Properties.CalendarTimeProperties.Buttons.Add(new DevExpress.XtraEditors.Controls.EditorButton());

            lueEmployee = new LookUpEdit();
            lueEmployee.Properties.DisplayMember = "FullName";
            lueEmployee.Properties.ValueMember = "Id";
            lueEmployee.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("FullName", "Çalışan Adı"));
            lueEmployee.Properties.NullText = "Çalışan Seçiniz";

            btnSave = new SimpleButton { Text = "Değişiklikleri Kaydet", Appearance = { Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White } };

            var group = layoutControl.Root;
            group.AddItem("Görev Başlığı", txtTitle).TextLocation = Locations.Top;
            group.AddItem("Açıklama", txtDescription).TextLocation = Locations.Top;
            group.AddItem("Durum", cmbStatus).TextLocation = Locations.Top;
            group.AddItem("Öncelik", cmbPriority).TextLocation = Locations.Top;
            group.AddItem("Teslim Tarihi", deDueDate).TextLocation = Locations.Top;
            group.AddItem("Sorumlu Çalışan", lueEmployee).TextLocation = Locations.Top;
            group.AddItem(null, btnSave).Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 20, 0);

            btnSave.Click += BtnSave_Click;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "TaskDetailForm";
            this.ResumeLayout(false);
        }

        private async void LoadTask()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync();
                _task = tasks.FirstOrDefault(t => t.Id == _taskId);

                if (_task == null)
                {
                    XtraMessageBox.Show("Görev bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }

                txtTitle.Text = _task.Title;
                txtDescription.Text = _task.Description;
                cmbStatus.SelectedItem = _task.Status.ToString();
                cmbPriority.SelectedItem = _task.Priority.ToString();
                deDueDate.DateTime = _task.DueDate ?? DateTime.Now;

                var employees = await _databaseService.GetEmployeesForEmployeeRoleAsync();
                lueEmployee.Properties.DataSource = employees;
                lueEmployee.EditValue = _task.AssignedToId;
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Hata: {ex.Message}");
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (_task == null)
                {
                    XtraMessageBox.Show("Görev verisi yüklenemedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    XtraMessageBox.Show("Görev başlığı gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _task.Title = txtTitle.Text;
                _task.Description = txtDescription.Text;
                if (Enum.TryParse<OfisAsistan.Models.TaskStatus>(cmbStatus.SelectedItem?.ToString(), out var status)) 
                    _task.Status = status;
                if (Enum.TryParse<TaskPriority>(cmbPriority.SelectedItem?.ToString(), out var priority)) 
                    _task.Priority = priority;
                _task.DueDate = deDueDate.DateTime;
                _task.AssignedToId = (int?)lueEmployee.EditValue ?? 0;

                if (await _databaseService.UpdateTaskAsync(_task))
                {
                    XtraMessageBox.Show("Görev güncellendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    XtraMessageBox.Show("Görev güncellenirken hata oluştu.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

