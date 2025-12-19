using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraLayout;
using DevExpress.Utils;

namespace OfisAsistan.Forms
{
    public partial class ManagerDashboard : XtraForm
    {
        private readonly DatabaseService _db;
        private readonly AIService _ai;
        private LayoutControl lc;
        private GridControl gcTasks, gcEmployees;
        private GridView gvTasks, gvEmployees;
        private ListBoxControl lstAnomalies;
        private LabelControl[] statCards = new LabelControl[4];

        public ManagerDashboard(DatabaseService db, AIService ai, NotificationService ns)
        {
            _db = db; _ai = ai;
            InitializeComponent();
            SetupDashboardLayout();
            this.Shown += async (s, e) => await LoadDataSafe();
        }

        private void SetupDashboardLayout()
        {
            this.Text = "Yönetici Stratejik Karar Destek Paneli";
            this.WindowState = FormWindowState.Maximized;

            // Eğer formun içinde önceden kalma bir layoutControl varsa siliyoruz (Zorlayıcı çözüm)
            var oldLc = this.Controls.OfType<LayoutControl>().FirstOrDefault();
            if (oldLc != null) this.Controls.Remove(oldLc);

            lc = new LayoutControl { Dock = DockStyle.Fill, Name = "mainLC" };
            this.Controls.Add(lc);
            lc.BeginUpdate();

            var root = lc.Root;
            root.GroupBordersVisible = false;
            root.Padding = new DevExpress.XtraLayout.Utils.Padding(10);
            root.Spacing = new DevExpress.XtraLayout.Utils.Padding(0);

            // --- 1. TOOLBAR ---
            var btnRefresh = CreateBtn("Verileri Yenile", "actions_refresh.svg");
            var btnNew = CreateBtn("Yeni Görev Tanımla", "actions_add.svg");
            var btnAI = CreateBtn("AI Görev Öneri", "outlook%20inspired/pivottable.svg");

            var groupButtons = root.AddGroup();
            groupButtons.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Flow;
            groupButtons.GroupBordersVisible = false;
            groupButtons.AddItem("", btnRefresh);
            groupButtons.AddItem("", btnNew);
            groupButtons.AddItem("", btnAI);

            // --- 2. KPI KARTLARI ---
            var groupStats = root.AddGroup();
            groupStats.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            groupStats.GroupBordersVisible = false;

            for (int i = 0; i < 4; i++)
            {
                groupStats.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 25 });
                statCards[i] = new LabelControl
                {
                    AllowHtmlString = true,
                    AutoSizeMode = LabelAutoSizeMode.None,
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat,
                    Appearance = { TextOptions = { HAlignment = HorzAlignment.Center, VAlignment = VertAlignment.Center }, Font = new Font("Segoe UI Semibold", 20) }
                };

                LayoutControlItem item = groupStats.AddItem("", statCards[i]);
                item.TextVisible = false;
                item.OptionsTableLayoutItem.ColumnIndex = i;

                // Kart yüksekliğini ZORLA sabitliyoruz
                item.SizeConstraintsType = SizeConstraintsType.Custom;
                item.MinSize = new Size(100, 110);
                item.MaxSize = new Size(0, 110);
            }

            // --- 3. ANA İÇERİK (Hücre Başına Yayılma Garantili) ---
            var mainContentGroup = root.AddGroup();
            mainContentGroup.GroupBordersVisible = false;
            mainContentGroup.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;

            mainContentGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 60 });
            mainContentGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 40 });

            // Tablo satırını %80 yaparak yukarı çekiyoruz
            mainContentGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.Percent, Height = 80 });
            mainContentGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.Percent, Height = 20 });

            gcTasks = CreateGrid(); gvTasks = (GridView)gcTasks.MainView;
            gcEmployees = CreateGrid(); gvEmployees = (GridView)gcEmployees.MainView;
            lstAnomalies = new ListBoxControl { Appearance = { Font = new Font("Segoe UI", 12) }, ItemHeight = 35 };

            // ZORLAYICI YERLEŞTİRME
            AddControlForce(mainContentGroup, "📋 AKTİF GÖREV LİSTESİ", gcTasks, 0, 0);
            AddControlForce(mainContentGroup, "👥 EKİP İŞ YÜKÜ ANALİZİ", gcEmployees, 1, 0);
            AddControlForce(mainContentGroup, "⚠️ SİSTEM ANOMALİLERİ", lstAnomalies, 0, 1, 2, 1);

            lc.EndUpdate();

            // Click Events
            btnRefresh.Click += async (s, e) => await LoadDataSafe();
            btnNew.Click += async (s, e) => {
                var f = new CreateTaskForm(_db, _ai);
                if (f.ShowDialog() == DialogResult.OK) await LoadDataSafe();
            };
            btnAI.Click += async (s, e) => await RunAITaskRecommendation();
        }

        private void AddControlForce(LayoutControlGroup g, string title, Control c, int col, int row, int cs = 1, int rs = 1)
        {
            var grp = g.AddGroup(title);
            grp.OptionsTableLayoutItem.ColumnIndex = col;
            grp.OptionsTableLayoutItem.RowIndex = row;
            grp.OptionsTableLayoutItem.ColumnSpan = cs;
            grp.OptionsTableLayoutItem.RowSpan = rs;
            grp.Padding = new DevExpress.XtraLayout.Utils.Padding(2);

            var item = grp.AddItem("", c);
            item.TextVisible = false;

            // KRİTİK: Kontrolün tüm dikey boşluğu doldurmasını sağlayan tek ayar
            item.SizeConstraintsType = SizeConstraintsType.Default;
            c.Dock = DockStyle.Fill;
            c.MinimumSize = new Size(100, 300); // Küçük kalmasını kesin olarak engeller
        }

        private async System.Threading.Tasks.Task LoadDataSafe()
        {
            try
            {
                var tasks = await _db.GetTasksAsync();
                var employees = await _db.GetEmployeesAsync();
                var stats = await _db.GetTaskStatisticsAsync();

                this.Invoke(new MethodInvoker(() => {
                    gcTasks.DataSource = tasks.Select(t => new { ID = t.Id, BAŞLIK = t.Title, DURUM = t.Status, TERMİN = t.DueDate?.ToShortDateString() }).ToList();
                    gcEmployees.DataSource = employees.Select(e => new { ÇALIŞAN = e.FullName, DOLULUK = $"%{e.WorkloadPercentage}" }).ToList();

                    if (stats != null)
                    {
                        UpdateCard(0, stats["Total"], "TOPLAM GÖREV", "#2b579a");
                        UpdateCard(1, stats["Pending"], "BEKLEYEN", "#d83b01");
                        UpdateCard(2, stats["Overdue"], "GECİKMİŞ", "#a4262c");
                        UpdateCard(3, stats["Completed"], "TAMAMLANAN", "#107c10");
                    }
                    gvTasks.BestFitColumns(); gvEmployees.BestFitColumns();
                }));

                var anomalies = await _ai.DetectAnomaliesAsync();
                this.Invoke(new MethodInvoker(() => {
                    lstAnomalies.Items.Clear();
                    foreach (var a in anomalies) lstAnomalies.Items.Add($"[{a.Severity}] {(a.Task != null ? a.Task.Title : "Sistem")}: {a.Message}");
                }));
            }
            catch (Exception ex) { XtraMessageBox.Show("Veri hatası: " + ex.Message); }
        }

        private async System.Threading.Tasks.Task RunAITaskRecommendation()
        {
            if (gvTasks.FocusedRowHandle < 0) return;
            var row = gvTasks.GetFocusedRow();
            int taskId = (int)((dynamic)row).ID;

            var all = await _db.GetTasksAsync();
            var task = all.FirstOrDefault(t => t.Id == taskId);
            var rec = await _ai.RecommendEmployeeForTaskAsync(task);

            if (rec != null)
            {
                string msg = $"<b>ÖNERİ:</b> {rec.RecommendedEmployee.FullName}<br><b>NEDEN:</b> {rec.Reason}";
                if (XtraMessageBox.Show(msg, "AI Önerisi", MessageBoxButtons.YesNo, MessageBoxIcon.Question, DefaultBoolean.True) == DialogResult.Yes)
                {
                    task.AssignedToId = rec.RecommendedEmployee.Id;
                    await _db.UpdateTaskAsync(task, 0);
                    await LoadDataSafe();
                }
            }
        }

        private GridControl CreateGrid()
        {
            var gc = new GridControl { Dock = DockStyle.Fill };
            var gv = new GridView(gc);
            gc.MainView = gv;
            gv.OptionsView.ShowGroupPanel = false;
            gv.OptionsBehavior.Editable = false;
            gv.Appearance.HeaderPanel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            gv.Appearance.Row.Font = new Font("Segoe UI", 11);
            return gc;
        }

        private void UpdateCard(int idx, object val, string title, string color) =>
          statCards[idx].Text = $"<color={color}><b>{val}</b></color><br><size=11>{title}</size>";

        private SimpleButton CreateBtn(string t, string i) => new SimpleButton
        {
            Text = t,
            ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage(i), SvgImageSize = new Size(24, 24) },
            Appearance = { Font = new Font("Segoe UI Semibold", 11) },
            AutoWidthInLayoutControl = true,
            Padding = new Padding(15, 8, 15, 8)
        };

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "ManagerDashboard"; this.Size = new Size(1366, 768); this.ResumeLayout(false); }
    }
}

