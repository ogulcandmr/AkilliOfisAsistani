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
        private readonly DatabaseService _databaseService;
        private readonly AIService _aiService;

        // Kontroller
        private TextEdit txtTitle;
        private MemoEdit txtDescription;
        private LookUpEdit lueEmployee;
        private ComboBoxEdit cmbPriority;
        private ComboBoxEdit cmbDepartment;
        private DateEdit deDueDate;
        private SimpleButton btnSave;
        private SimpleButton btnAIRecommend;
        private LayoutControl mainLayoutControl;

        public CreateTaskForm(DatabaseService databaseService, AIService aiService)
        {
            _databaseService = databaseService;
            _aiService = aiService;

            InitializeComponent();
            SetupSimpleUI();

            // Form açıldığında verileri asenkron çek
            this.Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void SetupSimpleUI()
        {
            this.Text = "Yeni Görev Planlama";
            this.Size = new Size(500, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            mainLayoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(mainLayoutControl);

            // Kontrolleri Başlat
            txtTitle = new TextEdit();
            txtDescription = new MemoEdit { Height = 100 };
            lueEmployee = new LookUpEdit();
            cmbPriority = new ComboBoxEdit();
            cmbDepartment = new ComboBoxEdit();
            deDueDate = new DateEdit { DateTime = DateTime.Now.AddDays(3) };

            btnAIRecommend = new SimpleButton { Text = "AI Önerisi Al" };
            btnSave = new SimpleButton
            {
                Text = "GÖREVİ KAYDET",
                Height = 50,
                Appearance = { Font = new Font("Segoe UI", 11, FontStyle.Bold), BackColor = Color.DodgerBlue }
            };

            // Özellikler
            lueEmployee.Properties.DisplayMember = "FullName";
            lueEmployee.Properties.ValueMember = "Id";
            lueEmployee.Properties.NullText = "Yükleniyor...";
            lueEmployee.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("FullName", "Çalışan Adı"));

            cmbPriority.Properties.Items.AddRange(Enum.GetNames(typeof(TaskPriority)));
            cmbPriority.SelectedIndex = 1;

            // Layout İnşası
            mainLayoutControl.BeginUpdate();
            var root = mainLayoutControl.Root;
            root.GroupBordersVisible = false;

            root.AddItem("Görev Başlığı", txtTitle).TextLocation = Locations.Top;
            root.AddItem("Detaylı Açıklama", txtDescription).TextLocation = Locations.Top;
            root.AddItem("Sorumlu Kişi", lueEmployee).TextLocation = Locations.Top;
            root.AddItem("Departman", cmbDepartment).TextLocation = Locations.Top;
            root.AddItem("Öncelik", cmbPriority).TextLocation = Locations.Top;
            root.AddItem("Teslim Tarihi", deDueDate).TextLocation = Locations.Top;
            root.AddItem("", btnAIRecommend).Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 20, 10);
            root.AddItem("", btnSave);
            mainLayoutControl.EndUpdate();

            // Olaylar (Tekil Bağlantı)
            btnSave.Click += BtnSave_Click;
            btnAIRecommend.Click += BtnAIRecommend_Click;
        }

        private async System.Threading.Tasks.Task LoadDataSafeAsync()
        {
            try
            {
                var employees = await _databaseService.GetEmployeesForEmployeeRoleAsync();
                var departments = await _databaseService.GetDepartmentsAsync();

                this.Invoke(new MethodInvoker(() => {
                    lueEmployee.Properties.DataSource = employees;
                    lueEmployee.Properties.NullText = "Seçiniz...";

                    if (departments != null)
                    {
                        cmbDepartment.Properties.Items.Clear();
                        cmbDepartment.Properties.Items.AddRange(departments.Select(d => d.Name).ToArray());
                    }
                }));
            }
            catch { /* Sessiz hata yönetimi */ }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            // 1. Basit Doğrulama
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                XtraMessageBox.Show("Lütfen bir başlık girin.", "Uyarı");
                return;
            }

            try
            {
                btnSave.Enabled = false;

                // 2. Veriyi Kontrollerden Topla
                var newTask = new TaskModel
                {
                    Title = txtTitle.Text.Trim(),
                    Description = txtDescription.Text?.Trim(),
                    AssignedToId = (lueEmployee.EditValue != null) ? Convert.ToInt32(lueEmployee.EditValue) : 0,
                    Priority = (TaskPriority)cmbPriority.SelectedIndex,
                    DueDate = deDueDate.DateTime,
                    Status = TaskStatusModel.Pending,
                    CreatedDate = DateTime.Now
                };

                // 3. Kaydet
                var result = await _databaseService.CreateTaskAsync(newTask);

                if (result != null)
                {
                    XtraMessageBox.Show("Görev başarıyla kaydedildi.", "Bilgi");
                    this.DialogResult = DialogResult.OK; // Ana formun Grid'i yenilemesini sağlar
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Hata oluştu: " + ex.Message);
            }
            finally
            {
                btnSave.Enabled = true;
            }
        }

        private async void BtnAIRecommend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text)) return;

            try
            {
                btnAIRecommend.Enabled = false;
                btnAIRecommend.Text = "AI Analiz Ediyor...";

                var rec = await _aiService.RecommendEmployeeForTaskAsync(new TaskModel
                {
                    Title = txtTitle.Text,
                    Description = txtDescription.Text
                });

                if (rec?.RecommendedEmployee != null)
                {
                    lueEmployee.EditValue = rec.RecommendedEmployee.Id;
                    XtraMessageBox.Show($"AI Önerisi: {rec.RecommendedEmployee.FullName}\n\nNeden: {rec.Reason}", "Zeki Atama");
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("AI hatası: " + ex.Message);
            }
            finally
            {
                btnAIRecommend.Enabled = true;
                btnAIRecommend.Text = "AI Önerisi Al";
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(480, 580);
            this.Name = "CreateTaskForm";
            this.ResumeLayout(false);
        }
    }
}