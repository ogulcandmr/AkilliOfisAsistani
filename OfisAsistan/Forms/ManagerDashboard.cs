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
using DevExpress.Utils;

namespace OfisAsistan.Forms
{
    public partial class ManagerDashboard : XtraForm
    {
        // Servisler
        private readonly DatabaseService _db;
        private readonly AIService _ai;

        // UI Bileşenleri
        // LayoutControl yerine standart Panel ve TableLayout kullanıyoruz
        private GridControl gcTasks, gcEmployees;
        private GridView gvTasks, gvEmployees;
        private ListBoxControl lstAnomalies;
        private LabelControl[] statCards = new LabelControl[4];

        // Tasarım Değişkenleri
        private Panel leftPanel;
        private Panel rightPanel;
        private bool isDragging = false;
        private Point dragStart;

        // Renk Paleti
        private readonly Color clrBackground = Color.FromArgb(245, 245, 250);
        private readonly Color clrPrimary = Color.FromArgb(99, 102, 241);
        private readonly Color clrTextDark = Color.FromArgb(17, 24, 39);

        public ManagerDashboard(DatabaseService db, AIService ai, NotificationService ns)
        {
            _db = db; _ai = ai;
            InitializeComponent();

            // 1. ADIM: Dış Çerçeve
            SetupModernContainer();

            // 2. ADIM: İç Yerleşim (TableLayout ile Sabitlendi)
            SetupDashboardLayout();

            this.Shown += async (s, e) => await LoadDataSafe();
        }

        private void SetupModernContainer()
        {
            this.Controls.Clear(); // Temizlik
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrBackground;

            // Sürükleme Mantığı
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < 80) { isDragging = true; dragStart = e.Location; } };
            this.MouseMove += (s, e) => { if (isDragging) { Point p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            this.MouseUp += (s, e) => { isDragging = false; };

            // Ana Bölücü (Sol Menü | Sağ İçerik)
            var mainSplit = new TableLayoutPanel();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.ColumnCount = 2;
            mainSplit.RowCount = 1;
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400F)); // Sol taraf sabit 400px
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Sağ taraf kalanı doldurur
            mainSplit.Padding = new Padding(0);
            mainSplit.Margin = new Padding(0);
            this.Controls.Add(mainSplit);

            // --- SOL PANEL ---
            leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrPrimary };
            leftPanel.Paint += LeftPanel_Paint;
            mainSplit.Controls.Add(leftPanel, 0, 0);

            var logoLabel = new LabelControl
            {
                Text = "Ofis\nAsistan",
                Appearance = { Font = new Font("Segoe UI", 36, FontStyle.Bold), ForeColor = Color.White },
                Location = new Point(40, 60),
                AutoSizeMode = LabelAutoSizeMode.None,
                Size = new Size(300, 150),
                BackColor = Color.Transparent
            };
            leftPanel.Controls.Add(logoLabel);

            var descLabel = new LabelControl
            {
                Text = "Yönetici Stratejik\nKarar Destek Paneli",
                Appearance = { Font = new Font("Segoe UI", 14), ForeColor = Color.FromArgb(224, 231, 255) },
                Location = new Point(45, 220),
                AutoSizeMode = LabelAutoSizeMode.None,
                Size = new Size(300, 70),
                BackColor = Color.Transparent
            };
            leftPanel.Controls.Add(descLabel);

            // --- SAĞ PANEL ---
            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrBackground, Padding = new Padding(30) };
            mainSplit.Controls.Add(rightPanel, 1, 0);

            CreateCustomWindowControls(rightPanel);
        }

        private void SetupDashboardLayout()
        {
            // Izgara Sistemi (TableLayoutPanel) - Üst üste binmeyi engeller
            var contentLayout = new TableLayoutPanel();
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.RowCount = 3;
            contentLayout.ColumnCount = 1;

            // Satır Yükseklikleri
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // 1. Satır: Butonlar
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F)); // 2. Satır: Kartlar (Sabit yükseklik)
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // 3. Satır: Gridler (Kalan yer)

            rightPanel.Controls.Add(contentLayout);

            // --- 1. BUTONLAR (TOOLBAR) ---
            var toolbarPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var btnRefresh = CreateBtn("Verileri Yenile", "actions_refresh.svg");
            var btnNew = CreateBtn("Yeni Görev", "actions_add.svg");
            var btnAI = CreateBtn("AI Öneri", "outlook%20inspired/pivottable.svg");

            btnRefresh.Margin = new Padding(0, 0, 10, 0);
            btnNew.Margin = new Padding(0, 0, 10, 0);

            // Eventler
            btnRefresh.Click += async (s, e) => await LoadDataSafe();
            btnNew.Click += async (s, e) => {
                var f = new CreateTaskForm(_db, _ai);
                if (f.ShowDialog() == DialogResult.OK) await LoadDataSafe();
            };
            btnAI.Click += async (s, e) => await RunAITaskRecommendation();

            toolbarPanel.Controls.Add(btnRefresh);
            toolbarPanel.Controls.Add(btnNew);
            toolbarPanel.Controls.Add(btnAI);
            contentLayout.Controls.Add(toolbarPanel, 0, 0);

            // --- 2. İSTATİSTİK KARTLARI ---
            var cardsLayout = new TableLayoutPanel();
            cardsLayout.Dock = DockStyle.Fill;
            cardsLayout.RowCount = 1;
            cardsLayout.ColumnCount = 4;
            // Her kart %25 yer kaplar, böylece kayma olmaz
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            cardsLayout.Padding = new Padding(0, 10, 0, 10);

            for (int i = 0; i < 4; i++)
            {
                statCards[i] = new LabelControl
                {
                    AllowHtmlString = true,
                    AutoSizeMode = LabelAutoSizeMode.None,
                    Appearance = {
                        BackColor = Color.White,
                        ForeColor = clrTextDark,
                        TextOptions = { HAlignment = HorzAlignment.Center, VAlignment = VertAlignment.Center },
                        Font = new Font("Segoe UI", 12)
                    },
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 0, 15, 0)
                };
                if (i == 3) statCards[i].Margin = new Padding(0); // Son kartın sağ boşluğunu sıfırla
                cardsLayout.Controls.Add(statCards[i], i, 0);
            }
            contentLayout.Controls.Add(cardsLayout, 0, 1);

            // --- 3. GRİDLER VE LİSTE ---
            var gridLayout = new TableLayoutPanel();
            gridLayout.Dock = DockStyle.Fill;
            gridLayout.RowCount = 2;
            gridLayout.ColumnCount = 2;

            // Sütun Ayarı: %65 Sol (Görevler), %35 Sağ (Ekip)
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            // Satır Ayarı: %70 Üst (Gridler), %30 Alt (Anomali)
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

            // Kontrolleri Oluştur
            gcTasks = CreateGrid(); gvTasks = (GridView)gcTasks.MainView;
            gcEmployees = CreateGrid(); gvEmployees = (GridView)gcEmployees.MainView;
            lstAnomalies = new ListBoxControl
            {
                Appearance = { Font = new Font("Segoe UI", 11), BackColor = Color.White, ForeColor = clrTextDark },
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple,
                ItemHeight = 40,
                Dock = DockStyle.Fill
            };

            // Gridleri Yerleştir
            AddContentToGrid(gridLayout, gcTasks, "📋 AKTİF GÖREV LİSTESİ", 0, 0);
            AddContentToGrid(gridLayout, gcEmployees, "👥 EKİP ANALİZİ", 1, 0);

            // Anomali Listesini Yerleştir (Alt satır, tüm genişlik)
            var anomalyPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var lblAnomaly = new LabelControl { Text = "⚠️ SİSTEM BİLDİRİMLERİ", Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 5), Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.Gray } };
            anomalyPanel.Controls.Add(lstAnomalies);
            anomalyPanel.Controls.Add(lblAnomaly);

            gridLayout.Controls.Add(anomalyPanel, 0, 1);
            gridLayout.SetColumnSpan(anomalyPanel, 2); // İki sütunu da kaplasın

            contentLayout.Controls.Add(gridLayout, 0, 2);

            // ManagerDashboard.cs içinde SetupDashboardLayout metodunun EN SONUNA ekle:

            // ... önceki kodlar ...

            // Grid Çift Tıklama Olayı (TaskDetailForm açmak için)
            gvTasks.DoubleClick += async (s, e) =>
            {
                var view = s as GridView;
                if (view == null) return;

                // Tıklanan satırı al
                var hitInfo = view.CalcHitInfo(view.GridControl.PointToClient(MousePosition));
                if (hitInfo.InRow)
                {
                    // Satırdaki ID verisini al (Anonim tip kullanmıştık)
                    int taskId = (int)view.GetRowCellValue(hitInfo.RowHandle, "ID");

                    // Detay Formunu Aç (Yönetici ID'si şimdilik 1 varsayıyoruz)
                    var detailForm = new TaskDetailForm(_db, taskId, 1, "Yönetici");

                    if (detailForm.ShowDialog() == DialogResult.OK)
                    {
                        await LoadDataSafe(); // Değişiklik olduysa listeyi yenile
                    }
                }
            };

            // ... metod bitişi ...
        }

        private void AddContentToGrid(TableLayoutPanel parent, Control content, string title, int col, int row)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 10) };
            if (col == 1) pnl.Margin = new Padding(0, 0, 0, 10);

            var lbl = new LabelControl
            {
                Text = title,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 5),
                Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.Gray }
            };

            content.Dock = DockStyle.Fill;
            pnl.Controls.Add(content);
            pnl.Controls.Add(lbl);

            parent.Controls.Add(pnl, col, row);
        }

        // --- DİĞER YARDIMCI METOTLAR (Değişmedi) ---
        void LeftPanel_Paint(object sender, PaintEventArgs e)
        {
            var rect = leftPanel.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, Color.FromArgb(99, 102, 241), Color.FromArgb(124, 58, 237), 120f))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 2))
            {
                e.Graphics.DrawEllipse(pen, -80, -80, 250, 250);
                e.Graphics.DrawEllipse(pen, 280, 600, 300, 300);
            }
        }

        void CreateCustomWindowControls(Panel parent)
        {
            int btnSize = 40;
            var closeBtn = new SimpleButton { Text = "✕", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - btnSize, 0) };
            StyleWindowBtn(closeBtn);
            closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeBtn.Click += (s, e) => this.Close();
            closeBtn.MouseEnter += (s, e) => closeBtn.Appearance.BackColor = Color.FromArgb(239, 68, 68);
            closeBtn.MouseLeave += (s, e) => closeBtn.Appearance.BackColor = Color.Transparent;
            parent.Controls.Add(closeBtn);

            var maxBtn = new SimpleButton { Text = "□", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - (btnSize * 2), 0) };
            StyleWindowBtn(maxBtn);
            maxBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            maxBtn.Click += (s, e) => this.WindowState = (this.WindowState == FormWindowState.Normal) ? FormWindowState.Maximized : FormWindowState.Normal;
            parent.Controls.Add(maxBtn);

            var minBtn = new SimpleButton { Text = "─", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - (btnSize * 3), 0) };
            StyleWindowBtn(minBtn);
            minBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            minBtn.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            parent.Controls.Add(minBtn);
        }

        void StyleWindowBtn(SimpleButton btn)
        {
            btn.Appearance.BackColor = Color.Transparent;
            btn.Appearance.ForeColor = Color.Gray;
            btn.Appearance.Font = new Font("Segoe UI", 12);
            btn.LookAndFeel.UseDefaultLookAndFeel = false;
            btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btn.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
        }

        private GridControl CreateGrid()
        {
            var gc = new GridControl { Dock = DockStyle.Fill };
            gc.LookAndFeel.UseDefaultLookAndFeel = false;
            gc.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;

            var gv = new GridView(gc);
            gc.MainView = gv;
            gv.OptionsView.ShowGroupPanel = false;
            gv.OptionsView.ShowIndicator = false;
            gv.OptionsView.ShowVerticalLines = DefaultBoolean.False;
            gv.OptionsView.EnableAppearanceEvenRow = true;
            gv.Appearance.HeaderPanel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            gv.Appearance.HeaderPanel.ForeColor = Color.Gray;
            gv.Appearance.HeaderPanel.BackColor = Color.White;
            gv.Appearance.Row.Font = new Font("Segoe UI", 10);
            gv.Appearance.Row.ForeColor = clrTextDark;
            gv.Appearance.EvenRow.BackColor = Color.FromArgb(250, 250, 255);
            gv.RowHeight = 35;
            gv.OptionsBehavior.Editable = false;
            return gc;
        }

        private SimpleButton CreateBtn(string t, string i)
        {
            var btn = new SimpleButton
            {
                Text = t,
                ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage(i), SvgImageSize = new Size(20, 20) },
                Appearance = { Font = new Font("Segoe UI Semibold", 10), BackColor = clrPrimary, ForeColor = Color.White },
                AutoWidthInLayoutControl = true,
                Padding = new Padding(10, 5, 10, 5),
                Cursor = Cursors.Hand,
                Size = new Size(150, 40) // Sabit boyut, FlowLayout içinde düzgün dursun
            };
            btn.LookAndFeel.UseDefaultLookAndFeel = false;
            btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btn.MouseEnter += (s, e) => btn.Appearance.BackColor = Color.FromArgb(79, 82, 221);
            btn.MouseLeave += (s, e) => btn.Appearance.BackColor = clrPrimary;
            return btn;
        }

        private void UpdateCard(int idx, object val, string title, string colorHex)
        {
            statCards[idx].Text = $"<br><size=24><color={colorHex}><b>{val}</b></color></size><br><br><size=10><color=#6b7280>{title.ToUpper()}</color></size>";
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
                        UpdateCard(0, stats["Total"], "Toplam Görev", "#4f46e5");
                        UpdateCard(1, stats["Pending"], "Bekleyen", "#d97706");
                        UpdateCard(2, stats["Overdue"], "Gecikmiş", "#dc2626");
                        UpdateCard(3, stats["Completed"], "Tamamlanan", "#10b981");
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

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "ManagerDashboard"; this.ResumeLayout(false); }
    }
}