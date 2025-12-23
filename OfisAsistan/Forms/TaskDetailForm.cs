using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using DevExpress.XtraTab;
using DevExpress.Utils;
using OfisAsistan.Services;
using OfisAsistan.Models;

// ÇAKIŞMAYI ÖNLEYEN ALIAS TANIMLARI
using AppTask = OfisAsistan.Models.Task; // Senin Modelin
using SysTask = System.Threading.Tasks.Task; // Sistem Task'ı
using TaskStatusEnum = OfisAsistan.Models.TaskStatus; // Senin Enum'ın

namespace OfisAsistan.Forms
{
    public partial class TaskDetailForm : XtraForm
    {
        // --- DEĞİŞKENLER ---
        private readonly DatabaseService _databaseService;
        private readonly int _taskId;
        private readonly int _currentUserId;
        private readonly string _currentUserName;

        // Model olarak "AppTask" kullanıyoruz
        private AppTask _task;

        // --- UI KONTROLLERİ ---
        private XtraTabControl mainTabControl;

        // Tab 1: Detaylar
        private TextEdit txtTitle;
        private MemoEdit txtDescription;
        private ComboBoxEdit cmbStatus, cmbPriority;
        private DateEdit deDueDate;
        private LookUpEdit lueEmployee;
        private SimpleButton btnSave;

        // Tab 2: Yorumlar
        private ListBoxControl lstComments;
        private MemoEdit txtNewComment;
        private SimpleButton btnSendComment;

        public TaskDetailForm(DatabaseService databaseService, int taskId, int currentUserId, string currentUserName)
        {
            _databaseService = databaseService;
            _taskId = taskId;
            _currentUserId = currentUserId;
            _currentUserName = currentUserName;

            InitializeComponent();
            SetupModernUI();

            // Load olayında asenkron veri çekme
            this.Load += async (s, e) =>
            {
                await LoadTaskDataAsync();
                await LoadCommentsAsync();
            };
        }

        private void SetupModernUI()
        {
            this.Text = $"Görev Detayı #{_taskId}";
            this.Size = new Size(800, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // 1. TAB KONTROL
            mainTabControl = new XtraTabControl { Dock = DockStyle.Fill };
            mainTabControl.LookAndFeel.UseDefaultLookAndFeel = false;
            mainTabControl.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;

            // Tab Header Stili
            mainTabControl.AppearancePage.Header.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            mainTabControl.AppearancePage.HeaderActive.ForeColor = Color.FromArgb(99, 102, 241); // İndigo
            this.Controls.Add(mainTabControl);

            // --- SEKME 1: GENEL BİLGİLER ---
            var tabDetails = mainTabControl.TabPages.Add("📋 Genel Bilgiler");
            SetupDetailsTab(tabDetails);

            // --- SEKME 2: YORUMLAR & TARTIŞMA ---
            var tabComments = mainTabControl.TabPages.Add("💬 Yorumlar ve Tartışma");
            SetupCommentsTab(tabComments);

            // --- SEKME 3: DOSYALAR ---
            var tabFiles = mainTabControl.TabPages.Add("📎 Dosyalar");
            var lblFiles = new LabelControl { Text = "Dosya yükleme alanı yakında eklenecek...", AutoSizeMode = LabelAutoSizeMode.None, Dock = DockStyle.Fill, Appearance = { TextOptions = { HAlignment = HorzAlignment.Center, VAlignment = VertAlignment.Center }, ForeColor = Color.Gray } };
            tabFiles.Controls.Add(lblFiles);
        }

        private void SetupDetailsTab(XtraTabPage page)
        {
            var lc = new LayoutControl { Dock = DockStyle.Fill };
            lc.LookAndFeel.UseDefaultLookAndFeel = false;
            lc.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            page.Controls.Add(lc);

            txtTitle = new TextEdit { Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            txtDescription = new MemoEdit { Font = new Font("Segoe UI", 10) };

            cmbStatus = new ComboBoxEdit();
            cmbStatus.Properties.Items.AddRange(Enum.GetNames(typeof(TaskStatusEnum)));

            cmbPriority = new ComboBoxEdit();
            cmbPriority.Properties.Items.AddRange(Enum.GetNames(typeof(TaskPriority)));

            deDueDate = new DateEdit();

            lueEmployee = new LookUpEdit();
            lueEmployee.Properties.NullText = "Çalışan Seçiniz";
            lueEmployee.Properties.Columns.Clear();
            lueEmployee.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("FullName", "Ad Soyad"));
            lueEmployee.Properties.DisplayMember = "FullName";
            lueEmployee.Properties.ValueMember = "Id";

            btnSave = new SimpleButton { Text = "DEĞİŞİKLİKLERİ KAYDET", Height = 50 };
            btnSave.Appearance.BackColor = Color.FromArgb(99, 102, 241);
            btnSave.Appearance.ForeColor = Color.White;
            btnSave.Appearance.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnSave.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btnSave.LookAndFeel.UseDefaultLookAndFeel = false;
            btnSave.Click += BtnSave_Click;

            // Layout Yerleşimi
            var root = lc.Root;
            root.GroupBordersVisible = false;
            root.Padding = new DevExpress.XtraLayout.Utils.Padding(20);

            root.AddItem("Görev Başlığı", txtTitle).TextLocation = Locations.Top;

            // Yan Yana Durum ve Öncelik
            var groupStatus = root.AddGroup();
            groupStatus.GroupBordersVisible = false;
            groupStatus.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            groupStatus.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });
            groupStatus.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });

            var itemStatus = groupStatus.AddItem("Durum", cmbStatus);
            itemStatus.TextLocation = Locations.Top;

            var itemPriority = groupStatus.AddItem("Öncelik", cmbPriority);
            itemPriority.TextLocation = Locations.Top;
            itemPriority.OptionsTableLayoutItem.ColumnIndex = 1;

            root.AddItem("Açıklama", txtDescription).TextLocation = Locations.Top;

            // Yan Yana Tarih ve Çalışan
            var groupAssign = root.AddGroup();
            groupAssign.GroupBordersVisible = false;
            groupAssign.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            groupAssign.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });
            groupAssign.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });

            var itemDate = groupAssign.AddItem("Teslim Tarihi", deDueDate);
            itemDate.TextLocation = Locations.Top;

            var itemEmp = groupAssign.AddItem("Sorumlu Çalışan", lueEmployee);
            itemEmp.TextLocation = Locations.Top;
            itemEmp.OptionsTableLayoutItem.ColumnIndex = 1;

            var itemBtn = root.AddItem("", btnSave);
            itemBtn.TextVisible = false;
            itemBtn.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 20, 20, 0);
        }

        private void SetupCommentsTab(XtraTabPage page)
        {
            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Liste
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F)); // Input alanı
            page.Controls.Add(pnlMain);

            // 1. Yorum Listesi
            lstComments = new ListBoxControl
            {
                Dock = DockStyle.Fill,
                ItemHeight = 60,
                HorizontalScrollbar = false
            };
            lstComments.Appearance.Font = new Font("Segoe UI", 10);
            pnlMain.Controls.Add(lstComments, 0, 0);

            // 2. Alt Input Alanı
            var pnlInput = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlInput.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            pnlInput.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            txtNewComment = new MemoEdit { Dock = DockStyle.Fill, Properties = { NullValuePrompt = "Bir yorum yazın..." } };

            btnSendComment = new SimpleButton { Text = "GÖNDER", Dock = DockStyle.Fill };
            btnSendComment.Appearance.BackColor = Color.FromArgb(34, 197, 94); // Yeşil
            btnSendComment.Appearance.ForeColor = Color.White;
            btnSendComment.Appearance.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnSendComment.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btnSendComment.LookAndFeel.UseDefaultLookAndFeel = false;
            btnSendComment.Click += BtnSendComment_Click;

            pnlInput.Controls.Add(txtNewComment, 0, 0);
            pnlInput.Controls.Add(btnSendComment, 1, 0);
            pnlMain.Controls.Add(pnlInput, 0, 1);
        }

        // --- İŞ MANTIĞI ---

        // SysTask (System.Threading.Tasks.Task) kullanarak asenkron metot tanımlıyoruz
        private async SysTask LoadTaskDataAsync()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync();
                _task = tasks.FirstOrDefault(t => t.Id == _taskId);

                if (_task == null) { Close(); return; }

                // UI Güncelleme (Invoke gerekebilir eğer thread farklıysa ama Load eventinde genelde gerekmez)
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
                XtraMessageBox.Show("Veri yüklenirken hata: " + ex.Message);
            }
        }

        private async SysTask LoadCommentsAsync()
        {
            try
            {
                lstComments.Items.Clear();
                var comments = await _databaseService.GetCommentsAsync(_taskId);

                foreach (var c in comments)
                {
                    string display = $"{c.UserName} ({c.CreatedAt:dd.MM HH:mm}):\n{c.CommentText}";
                    lstComments.Items.Add(display);
                }

                if (lstComments.ItemCount > 0)
                    lstComments.SelectedIndex = lstComments.ItemCount - 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Yorum yükleme hatası: " + ex.Message);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text)) return;

            try
            {
                btnSave.Enabled = false;
                _task.Title = txtTitle.Text;
                _task.Description = txtDescription.Text;

                if (Enum.TryParse<TaskStatusEnum>(cmbStatus.SelectedItem?.ToString(), out var s)) _task.Status = s;
                if (Enum.TryParse<TaskPriority>(cmbPriority.SelectedItem?.ToString(), out var p)) _task.Priority = p;

                _task.DueDate = deDueDate.DateTime;
                _task.AssignedToId = (lueEmployee.EditValue != null) ? Convert.ToInt32(lueEmployee.EditValue) : 0;

                if (await _databaseService.UpdateTaskAsync(_task))
                {
                    XtraMessageBox.Show("Başarıyla güncellendi!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    XtraMessageBox.Show("Güncelleme başarısız.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Hata: " + ex.Message);
            }
            finally
            {
                btnSave.Enabled = true;
            }
        }

        private async void BtnSendComment_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewComment.Text)) return;

            try
            {
                btnSendComment.Enabled = false;
                var comment = new TaskComment
                {
                    TaskId = _taskId,
                    UserId = _currentUserId,
                    UserName = _currentUserName,
                    CommentText = txtNewComment.Text,
                    CreatedAt = DateTime.Now
                };

                bool success = await _databaseService.AddCommentAsync(comment);
                if (success)
                {
                    txtNewComment.Text = "";
                    await LoadCommentsAsync();
                }
                else
                {
                    XtraMessageBox.Show("Yorum gönderilemedi.");
                }
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Yorum hatası: " + ex.Message);
            }
            finally
            {
                btnSendComment.Enabled = true;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "TaskDetailForm";
            this.ResumeLayout(false);
        }
    }
}