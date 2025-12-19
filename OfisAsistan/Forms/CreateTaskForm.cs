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
    public partial class CreateTaskForm : XtraForm
    {
        private DatabaseService _databaseService;
        private AIService _aiService;
        
        private TextEdit txtTitle;
        private MemoEdit txtDescription;
        private LookUpEdit lueEmployee;
        private ComboBoxEdit cmbPriority;
        private ComboBoxEdit cmbDepartment;
        private DateEdit deDueDate;
        private SimpleButton btnSave;
        private SimpleButton btnAIRecommend;
        private LayoutControl layoutControl;

        public CreateTaskForm(DatabaseService databaseService, AIService aiService)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            InitializeComponent();
            SetupDevExpressUI();
            this.Load += CreateTaskForm_Load;
        }

        private void CreateTaskForm_Load(object sender, EventArgs e)
        {
            LoadEmployees();
        }

        private void SetupDevExpressUI()
        {
            this.Text = "Yeni Görev Oluştur";
            this.Size = new Size(500, 500);
            this.StartPosition = FormStartPosition.CenterParent;

            layoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(layoutControl);

            txtTitle = new TextEdit();
            txtDescription = new MemoEdit { Height = 100 };
            
            lueEmployee = new LookUpEdit();
            lueEmployee.Properties.DisplayMember = "FullName";
            lueEmployee.Properties.ValueMember = "Id";
            lueEmployee.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("FullName", "Çalışan Adı"));
            lueEmployee.Properties.NullText = "Çalışan Seçiniz";

            cmbPriority = new ComboBoxEdit();
            cmbPriority.Properties.Items.AddRange(new[] { "Düşük", "Normal", "Yüksek", "Kritik" });
            cmbPriority.SelectedIndex = 1;

            cmbDepartment = new ComboBoxEdit();
            cmbDepartment.Properties.NullText = "Departman Seçiniz";

            deDueDate = new DateEdit();
            deDueDate.Properties.CalendarTimeProperties.Buttons.Add(new DevExpress.XtraEditors.Controls.EditorButton());
            deDueDate.DateTime = DateTime.Now.AddDays(1);

            btnAIRecommend = new SimpleButton { Text = "AI Öneri Al", ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("outlook%20inspired/pivottable.svg") } };
            btnSave = new SimpleButton { Text = "Görevi Oluştur", Appearance = { Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White } };

            var group = layoutControl.Root;
            group.AddItem("Görev Başlığı", txtTitle).TextLocation = Locations.Top;
            group.AddItem("Açıklama", txtDescription).TextLocation = Locations.Top;
            group.AddItem("Atanacak Çalışan", lueEmployee).TextLocation = Locations.Top;
            group.AddItem("Öncelik", cmbPriority).TextLocation = Locations.Top;
            group.AddItem("Departman", cmbDepartment).TextLocation = Locations.Top;
            group.AddItem("Teslim Tarihi", deDueDate).TextLocation = Locations.Top;
            group.AddItem(null, btnAIRecommend).Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 10, 10);
            group.AddItem(null, btnSave);

            btnSave.Click += BtnSave_Click;
            btnAIRecommend.Click += BtnAIRecommend_Click;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "CreateTaskForm";
            this.ResumeLayout(false);
        }

        private async void LoadEmployees()
        {
            try
            {
                var employees = await _databaseService.GetEmployeesForEmployeeRoleAsync();
                lueEmployee.Properties.DataSource = employees;

                var departments = await _databaseService.GetDepartmentsAsync();
                cmbDepartment.Properties.Items.Clear();
                cmbDepartment.Properties.Items.AddRange(departments.Select(d => d.Name).ToArray());
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Veri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    XtraMessageBox.Show("Başlık gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var task = new TaskModel
                {
                    Title = txtTitle.Text,
                    Description = txtDescription.Text,
                    AssignedToId = (int?)lueEmployee.EditValue ?? 0,
                    Priority = (TaskPriority)cmbPriority.SelectedIndex,
                    DueDate = deDueDate.DateTime,
                    CreatedDate = DateTime.Now,
                    Status = TaskStatusModel.Pending
                };

                if (await _databaseService.CreateTaskAsync(task) != null)
                {
                    XtraMessageBox.Show("Görev oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    XtraMessageBox.Show("Görev oluşturulurken hata oluştu.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Görev kaydedilirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnAIRecommend_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    XtraMessageBox.Show("Önce başlığı girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                var recommendation = await _aiService.RecommendEmployeeForTaskAsync(new TaskModel 
                { 
                    Title = txtTitle.Text, 
                    Description = txtDescription.Text, 
                    Priority = (TaskPriority)cmbPriority.SelectedIndex 
                });
                
                if (recommendation?.RecommendedEmployee != null)
                {
                    lueEmployee.EditValue = recommendation.RecommendedEmployee.Id;
                    XtraMessageBox.Show(recommendation.Reason, "AI Önerisi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    XtraMessageBox.Show("AI önerisi alınamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"AI önerisi alınırken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
