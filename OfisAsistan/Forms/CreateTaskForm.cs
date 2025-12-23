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
        private ComboBoxEdit cmbDepartment; // Eğer modelde DepartmentId varsa kullanacağız
        private DateEdit deDueDate;
        private SimpleButton btnSave;
        private SimpleButton btnAIRecommend;
        private LayoutControl mainLayoutControl;

        // Renk Paleti
        private readonly Color clrPrimary = Color.FromArgb(99, 102, 241); // İndigo

        public CreateTaskForm(DatabaseService databaseService, AIService aiService)
        {
            _databaseService = databaseService;
            _aiService = aiService;

            InitializeComponent();
            SetupModernUI(); // Tasarımı yükle

            // Form açıldığında verileri asenkron çek
            this.Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void SetupModernUI()
        {
            this.Text = "Yeni Görev Oluştur";
            this.Size = new Size(550, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            mainLayoutControl = new LayoutControl { Dock = DockStyle.Fill };
            mainLayoutControl.LookAndFeel.UseDefaultLookAndFeel = false;
            mainLayoutControl.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            this.Controls.Add(mainLayoutControl);

            // Kontrolleri Başlat
            txtTitle = CreateTextEdit();
            txtDescription = new MemoEdit { Height = 120 };

            lueEmployee = new LookUpEdit();
            StyleLookUpEdit(lueEmployee, "Çalışan Seçiniz...");

            cmbPriority = new ComboBoxEdit();
            cmbPriority.Properties.Items.AddRange(Enum.GetNames(typeof(TaskPriority)));
            cmbPriority.SelectedIndex = 1; // Default: Normal

            cmbDepartment = new ComboBoxEdit(); // Eğer departman listesi varsa doldurulacak

            deDueDate = new DateEdit { DateTime = DateTime.Now.AddDays(3) };

            // Butonlar
            btnAIRecommend = new SimpleButton { Text = "✨ AI İle Çalışan Önerisi Al", Height = 40 };
            btnAIRecommend.Appearance.BackColor = Color.FromArgb(243, 244, 246);
            btnAIRecommend.Appearance.ForeColor = clrPrimary;
            btnAIRecommend.Appearance.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnAIRecommend.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btnAIRecommend.LookAndFeel.UseDefaultLookAndFeel = false;

            btnSave = new SimpleButton { Text = "GÖREVİ OLUŞTUR", Height = 55 };
            btnSave.Appearance.BackColor = clrPrimary;
            btnSave.Appearance.ForeColor = Color.White;
            btnSave.Appearance.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnSave.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btnSave.LookAndFeel.UseDefaultLookAndFeel = false;

            // Layout İnşası
            mainLayoutControl.BeginUpdate();
            var root = mainLayoutControl.Root;
            root.GroupBordersVisible = false;
            root.Padding = new DevExpress.XtraLayout.Utils.Padding(20);

            // Başlık
            root.AddItem("Görev Başlığı", txtTitle).TextLocation = Locations.Top;

            // Açıklama
            root.AddItem("Detaylı Açıklama", txtDescription).TextLocation = Locations.Top;

            // AI Butonu (Açıklamanın hemen altına)
            var itemAI = root.AddItem("", btnAIRecommend);
            itemAI.TextVisible = false;
            itemAI.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 10, 20);

            // Yan Yana Alanlar (Öncelik ve Tarih)
            var groupRow1 = root.AddGroup();
            groupRow1.GroupBordersVisible = false;
            groupRow1.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            groupRow1.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });
            groupRow1.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });

            var itemPrio = groupRow1.AddItem("Öncelik", cmbPriority);
            itemPrio.TextLocation = Locations.Top;

            var itemDate = groupRow1.AddItem("Teslim Tarihi", deDueDate);
            itemDate.TextLocation = Locations.Top;
            itemDate.OptionsTableLayoutItem.ColumnIndex = 1;

            // Çalışan Seçimi
            root.AddItem("Sorumlu Kişi", lueEmployee).TextLocation = Locations.Top;

            // Kaydet Butonu (En altta)
            var itemSave = root.AddItem("", btnSave);
            itemSave.TextVisible = false;
            itemSave.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 30, 0);

            mainLayoutControl.EndUpdate();

            // Olaylar
            btnSave.Click += BtnSave_Click;
            btnAIRecommend.Click += BtnAIRecommend_Click;
        }

        // --- YARDIMCI METOTLAR ---
        private TextEdit CreateTextEdit()
        {
            var txt = new TextEdit();
            txt.Properties.Appearance.Font = new Font("Segoe UI", 10);
            return txt;
        }

        private void StyleLookUpEdit(LookUpEdit lue, string nullText)
        {
            lue.Properties.NullText = nullText;
            lue.Properties.DisplayMember = "FullName";
            lue.Properties.ValueMember = "Id";
            lue.Properties.Columns.Clear();
            lue.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("FullName", "Ad Soyad"));
            lue.Properties.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoSuggest;
        }

        // --- İŞ MANTIĞI ---
        private async System.Threading.Tasks.Task LoadDataSafeAsync()
        {
            try
            {
                var employees = await _databaseService.GetEmployeesForEmployeeRoleAsync();
                var departments = await _databaseService.GetDepartmentsAsync();

                this.Invoke(new MethodInvoker(() => {
                    lueEmployee.Properties.DataSource = employees;

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
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                XtraMessageBox.Show("Lütfen bir başlık girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnSave.Enabled = false;

                var newTask = new TaskModel
                {
                    Title = txtTitle.Text.Trim(),
                    Description = txtDescription.Text?.Trim(),
                    AssignedToId = (lueEmployee.EditValue != null) ? Convert.ToInt32(lueEmployee.EditValue) : 0,
                    Priority = (TaskPriority)cmbPriority.SelectedIndex,
                    DueDate = deDueDate.DateTime,
                    Status = TaskStatusModel.Pending,
                    CreatedDate = DateTime.Now,
                    // Eğer giriş yapan kullanıcıyı biliyorsak buraya CreatedById de ekleyebiliriz
                };

                var result = await _databaseService.CreateTaskAsync(newTask);

                if (result != null)
                {
                    XtraMessageBox.Show("Görev başarıyla oluşturuldu.", "Ofis Asistan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK; // Ana formun Grid'i yenilemesini sağlar
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
            }
        }

        private async void BtnAIRecommend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                XtraMessageBox.Show("Öneri almak için önce bir görev başlığı ve (isteğe bağlı) açıklama girin.", "Uyarı");
                return;
            }

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
                else
                {
                    XtraMessageBox.Show("Uygun bir çalışan önerilemedi.", "Bilgi");
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("AI servisine ulaşılamadı: " + ex.Message, "Hata");
            }
            finally
            {
                btnAIRecommend.Enabled = true;
                btnAIRecommend.Text = "✨ AI İle Çalışan Önerisi Al";
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(550, 650);
            this.Name = "CreateTaskForm";
            this.ResumeLayout(false);
        }
    }
}