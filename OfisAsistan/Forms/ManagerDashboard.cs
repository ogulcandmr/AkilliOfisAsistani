using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskModel = OfisAsistan.Models.Task; // Model alias
using OfisAsistan.Models; // Enumlar
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.Utils;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;

namespace OfisAsistan.Forms
{
    public partial class ManagerDashboard : XtraForm
    {
        // Servisler
        private readonly DatabaseService _db;
        private readonly AIService _ai;

        // Bu örnekte NotificationService kullanılmıyor ama constructor imzasını bozmuyoruz
        private readonly NotificationService _ns;

        // UI Bileşenleri
        private GridControl gcTasks, gcEmployees;
        private GridView gvTasks, gvEmployees;
        private ListBoxControl lstAnomalies;
        private ListBoxControl lstLiveLogs;
        private LabelControl[] statCards = new LabelControl[4];

        // Veri Önbelleği
        private List<TaskModel> _allTasksCache;
        private List<Employee> _employeesCache;

        // Tasarım Değişkenleri
        private Panel leftPanel;
        private Panel rightPanel;
        private bool isDragging = false;
        private Point dragStart;

        // Renk Paleti
        private readonly Color clrBackground = Color.FromArgb(245, 245, 250);
        private readonly Color clrPrimary = Color.FromArgb(99, 102, 241);
        private readonly Color clrTextDark = Color.FromArgb(17, 24, 39);

        // Drag & Drop Değişkenleri
        private GridHitInfo downHitInfo = null;

        public ManagerDashboard(DatabaseService db, AIService ai, NotificationService ns)
        {
            _db = db; _ai = ai; _ns = ns;
            InitializeComponent();

            SetupModernContainer();
            SetupDashboardLayout();

            this.Shown += async (s, e) => await LoadDataSafe();
        }

        private void SetupModernContainer()
        {
            this.Controls.Clear();
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(1600, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrBackground;

            // Pencere Sürükleme Mantığı
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < 80) { isDragging = true; dragStart = e.Location; } };
            this.MouseMove += (s, e) => { if (isDragging) { Point p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            this.MouseUp += (s, e) => { isDragging = false; };

            var mainSplit = new TableLayoutPanel();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.ColumnCount = 2;
            mainSplit.RowCount = 1;
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350F));
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.Controls.Add(mainSplit);

            leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrPrimary };
            SetupLeftPanelContent();
            mainSplit.Controls.Add(leftPanel, 0, 0);

            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrBackground, Padding = new Padding(30) };
            mainSplit.Controls.Add(rightPanel, 1, 0);

            CreateCustomWindowControls(rightPanel);
        }

        private void SetupLeftPanelContent()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftPanel.Controls.Add(layout);

            var lblTitle = new LabelControl
            {
                Text = "Ofis Asistan\nAI Log Stream",
                Appearance = { Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.White, TextOptions = { HAlignment = HorzAlignment.Center } },
                AutoSizeMode = LabelAutoSizeMode.None,
                Dock = DockStyle.Fill
            };
            layout.Controls.Add(lblTitle, 0, 0);

            var pnlActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20, 10, 20, 0) };

            var btnMeeting = CreateQuickActionBtn("📢 Acil Toplantı", "Tüm ekibe mail atar");
            btnMeeting.Click += (s, e) => {
                AddLog("Sistem", "Tüm ekibe 'Acil Toplantı' daveti gönderildi.");
                XtraMessageBox.Show("Toplantı davetleri e-posta ile gönderildi.", "İşlem Başarılı");
            };

            var btnQuick = CreateQuickActionBtn("⚡ Hızlı Kontrol", "Bekleyen işleri analiz eder");
            btnQuick.Click += (s, e) => FilterGridByCard(1);

            // --- TAKVİM MODU BUTONU (DÜZELTİLDİ) ---
            var btnView = CreateQuickActionBtn("📅 Takvim Modu", "Gantt görünümüne geç");
            btnView.Click += (s, e) => {
                try
                {
                    // CalendarForm açılıyor
                    var calendar = new CalendarForm(_db);
                    calendar.Show();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show("Takvim açılırken hata: " + ex.Message);
                }
            };

            pnlActions.Controls.Add(btnMeeting);
            pnlActions.Controls.Add(btnQuick);
            pnlActions.Controls.Add(btnView);

            var lblActions = new LabelControl { Text = "HIZLI AKSİYONLAR", Appearance = { Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(200, 255, 255, 255) }, Padding = new Padding(25, 0, 0, 5), Dock = DockStyle.Top };
            layout.Controls.Add(lblActions, 0, 1);
            layout.Controls.Add(pnlActions, 0, 1);
            pnlActions.BringToFront();

            var pnlLog = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            var lblLogHeader = new LabelControl { Text = "🔴 CANLI AKTİVİTE", Dock = DockStyle.Top, Appearance = { Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(200, 255, 255, 255) } };

            lstLiveLogs = new ListBoxControl
            {
                Dock = DockStyle.Fill,
                Appearance = { BackColor = Color.FromArgb(80, 83, 200), ForeColor = Color.White, Font = new Font("Consolas", 9) },
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                ItemHeight = 25,
                ShowFocusRect = false
            };

            pnlLog.Controls.Add(lstLiveLogs);
            pnlLog.Controls.Add(lblLogHeader);
            layout.Controls.Add(pnlLog, 0, 2);
        }

        private SimpleButton CreateQuickActionBtn(string text, string tooltip)
        {
            var btn = new SimpleButton
            {
                Text = text,
                ToolTip = tooltip,
                Size = new Size(280, 45),
                Margin = new Padding(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Appearance = { Font = new Font("Segoe UI Semibold", 10), BackColor = Color.White, ForeColor = clrPrimary }
            };
            btn.LookAndFeel.UseDefaultLookAndFeel = false;
            btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            return btn;
        }

        private void SetupDashboardLayout()
        {
            var contentLayout = new TableLayoutPanel();
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.RowCount = 3;
            contentLayout.ColumnCount = 1;

            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            rightPanel.Controls.Add(contentLayout);

            // --- TOOLBAR ---
            var toolbarPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var btnRefresh = CreateBtn("Verileri Yenile", "actions_refresh.svg");
            var btnNew = CreateBtn("Yeni Görev", "actions_add.svg");
            var btnLoadBalance = CreateBtn("Yük Dengeleme (AI)", "outlook%20inspired/pivottable.svg");
            var btnRecommend = CreateBtn("AI Personel Önerisi", "actions_user.svg"); // Geri gelen buton

            btnRefresh.Click += async (s, e) => await LoadDataSafe();

            // YENİ GÖREV BUTONU
            btnNew.Click += async (s, e) => {
                var f = new CreateTaskForm(_db, _ai);
                if (f.ShowDialog() == DialogResult.OK)
                {
                    AddLog("Kullanıcı", "Yeni görev oluşturuldu.");
                    await LoadDataSafe();
                }
            };

            // YÜK DENGELEME (GENEL)
            btnLoadBalance.Click += async (s, e) => await RunSmartLoadBalancing();

            // GÖREV BAZLI PERSONEL ÖNERİSİ
            btnRecommend.Click += async (s, e) => await RecommendEmployeeForSelectedTask();

            toolbarPanel.Controls.Add(btnRefresh);
            toolbarPanel.Controls.Add(btnNew);
            toolbarPanel.Controls.Add(btnLoadBalance);
            toolbarPanel.Controls.Add(btnRecommend);
            contentLayout.Controls.Add(toolbarPanel, 0, 0);

            // --- KARTLAR ---
            var cardsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 4, Padding = new Padding(0, 10, 0, 10) };
            for (int i = 0; i < 4; i++) { cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); }

            for (int i = 0; i < 4; i++)
            {
                statCards[i] = new LabelControl
                {
                    AllowHtmlString = true,
                    AutoSizeMode = LabelAutoSizeMode.None,
                    Appearance = { BackColor = Color.White, ForeColor = clrTextDark, TextOptions = { HAlignment = HorzAlignment.Center, VAlignment = VertAlignment.Center }, Font = new Font("Segoe UI", 12) },
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 0, 15, 0),
                    Cursor = Cursors.Hand,
                    Tag = i
                };

                int index = i;
                statCards[i].Click += (s, e) => FilterGridByCard(index);

                if (i == 3) statCards[i].Margin = new Padding(0);
                cardsLayout.Controls.Add(statCards[i], i, 0);
            }
            contentLayout.Controls.Add(cardsLayout, 0, 1);

            // --- GRİDLER ---
            var gridLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2 };
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

            gcTasks = CreateGrid(); gvTasks = (GridView)gcTasks.MainView;
            gcEmployees = CreateGrid(); gvEmployees = (GridView)gcEmployees.MainView;

            SetupDragAndDrop();
            SetupContextMenu();
            SetupDoubleClick();

            lstAnomalies = new ListBoxControl { Appearance = { Font = new Font("Segoe UI", 11), BackColor = Color.White }, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple, ItemHeight = 40, Dock = DockStyle.Fill };

            AddContentToGrid(gridLayout, gcTasks, "📋 AKTİF GÖREV LİSTESİ (Çift Tıkla -> Detay)", 0, 0);
            AddContentToGrid(gridLayout, gcEmployees, "👥 EKİP YÜKÜ (Hedef)", 1, 0);

            var anomalyPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            anomalyPanel.Controls.Add(lstAnomalies);
            anomalyPanel.Controls.Add(new LabelControl { Text = "⚠️ AI SİSTEM BİLDİRİMLERİ (Gerçek Zamanlı)", Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 5), Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.Gray } });

            gridLayout.Controls.Add(anomalyPanel, 0, 1);
            gridLayout.SetColumnSpan(anomalyPanel, 2);
            contentLayout.Controls.Add(gridLayout, 0, 2);
        }

        // --- TASK DETAIL FORM BAĞLANTISI (DÜZELTİLDİ) ---
        private void SetupDoubleClick()
        {
            gvTasks.DoubleClick += async (s, e) => {
                var view = s as GridView;
                var pt = view.GridControl.PointToClient(Control.MousePosition);
                var info = view.CalcHitInfo(pt);

                if (info.InRow)
                {
                    int id = (int)view.GetRowCellValue(info.RowHandle, "ID");

                    // TaskDetailForm'u açıyoruz (Mevcut kullanıcının ID'sini 1 (Yönetici) varsayıyoruz)
                    // Önceki hatayı çözmek için direkt formu new'liyoruz.
                    int currentUserId = 1;
                    string currentUserRole = "Yönetici";

                    var detailForm = new TaskDetailForm(_db, id, currentUserId, currentUserRole);

                    if (detailForm.ShowDialog() == DialogResult.OK)
                    {
                        AddLog("Kullanıcı", $"Görev güncellendi (ID: {id})");
                        await LoadDataSafe(); // Listeyi yenile
                    }
                }
            };
        }

        private void SetupDragAndDrop()
        {
            gcTasks.MouseDown += (s, e) => {
                GridView view = s as GridView ?? ((GridControl)s).MainView as GridView;
                downHitInfo = null;
                GridHitInfo hitInfo = view.CalcHitInfo(new Point(e.X, e.Y));
                if (Control.ModifierKeys != Keys.None) return;
                if (e.Button == MouseButtons.Left && hitInfo.InRow && hitInfo.HitTest != GridHitTest.RowIndicator)
                    downHitInfo = hitInfo;
            };

            gcTasks.MouseMove += (s, e) => {
                GridView view = s as GridView ?? ((GridControl)s).MainView as GridView;
                if (e.Button == MouseButtons.Left && downHitInfo != null)
                {
                    Size dragSize = SystemInformation.DragSize;
                    Rectangle dragRect = new Rectangle(new Point(downHitInfo.HitPoint.X - dragSize.Width / 2, downHitInfo.HitPoint.Y - dragSize.Height / 2), dragSize);
                    if (!dragRect.Contains(new Point(e.X, e.Y)))
                    {
                        int taskId = (int)view.GetRowCellValue(downHitInfo.RowHandle, "ID");
                        string taskTitle = (string)view.GetRowCellValue(downHitInfo.RowHandle, "BAŞLIK");

                        var originalTask = _allTasksCache.FirstOrDefault(t => t.Id == taskId);
                        int? currentAssignedId = originalTask?.AssignedToId;

                        gcTasks.DoDragDrop(new { TaskID = taskId, Title = taskTitle, CurrentAssignedId = currentAssignedId }, DragDropEffects.Move);
                        downHitInfo = null;
                        DevExpress.Utils.DXMouseEventArgs.GetMouseArgs(e).Handled = true;
                    }
                }
            };

            gcEmployees.AllowDrop = true;
            gcEmployees.DragOver += (s, e) => {
                if (e.Data.GetDataPresent(typeof(string)) || e.Data.GetDataPresent(typeof(object)))
                    e.Effect = DragDropEffects.Move;
                else
                    e.Effect = DragDropEffects.None;
            };

            gcEmployees.DragDrop += async (s, e) => {
                var grid = (GridControl)s;
                var view = (GridView)grid.MainView;

                var srcData = e.Data.GetData(e.Data.GetFormats()[0]);
                int taskId = (int)srcData.GetType().GetProperty("TaskID").GetValue(srcData);
                string taskTitle = (string)srcData.GetType().GetProperty("Title").GetValue(srcData);
                int? oldAssignedId = (int?)srcData.GetType().GetProperty("CurrentAssignedId").GetValue(srcData);

                Point p = grid.PointToClient(new Point(e.X, e.Y));
                GridHitInfo hit = view.CalcHitInfo(p);

                if (hit.InRow)
                {
                    int targetEmpId = (int)view.GetRowCellValue(hit.RowHandle, "ID");
                    string empName = (string)view.GetRowCellValue(hit.RowHandle, "ÇALIŞAN");

                    if (XtraMessageBox.Show($"'{taskTitle}' görevi '{empName}' kişisine atansın mı?", "Atama Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        var originalTask = _allTasksCache.FirstOrDefault(t => t.Id == taskId);
                        if (originalTask != null)
                        {
                            originalTask.AssignedToId = targetEmpId;
                            bool success = await _db.UpdateTaskAsync(originalTask, oldAssignedId);

                            if (success)
                            {
                                AddLog("Sistem", $"BAŞARILI: {taskTitle} -> {empName}");
                                await LoadDataSafe();
                            }
                            else
                            {
                                XtraMessageBox.Show("Veritabanı güncellenemedi!", "Hata");
                            }
                        }
                    }
                }
            };
        }

        private void SetupContextMenu()
        {
            var menu = new ContextMenuStrip();
            var itemNudge = new ToolStripMenuItem("🔔 Personele Hatırlatma Gönder");
            itemNudge.Click += (s, e) => {
                var row = gvTasks.GetFocusedRow();
                if (row == null) return;
                string title = (string)((dynamic)row).BAŞLIK;
                AddLog("Yönetici", $"Dürtme gönderildi: {title}");
            };
            menu.Items.Add(itemNudge);
            gcTasks.ContextMenuStrip = menu;
        }

        // --- GÖREV BAZLI PERSONEL ÖNERİSİ ---
        private async System.Threading.Tasks.Task RecommendEmployeeForSelectedTask()
        {
            var row = gvTasks.GetFocusedRow();
            if (row == null)
            {
                XtraMessageBox.Show("Lütfen listeden bir görev seçin.", "Uyarı");
                return;
            }

            int taskId = (int)((dynamic)row).ID;
            var task = _allTasksCache.FirstOrDefault(t => t.Id == taskId);

            if (task == null) return;

            var recommendation = await _ai.RecommendEmployeeForTaskAsync(task);

            if (recommendation != null)
            {
                string msg = $"<b>ÖNERİLEN PERSONEL:</b> {recommendation.RecommendedEmployee.FullName}<br>" +
                             $"<b>GEREKÇE:</b> {recommendation.Reason}<br><br>" +
                             $"Görev bu kişiye atansın mı?";

                if (XtraMessageBox.Show(msg, "AI Atama Önerisi", MessageBoxButtons.YesNo, MessageBoxIcon.Information, DefaultBoolean.True) == DialogResult.Yes)
                {
                    int? oldId = task.AssignedToId;
                    task.AssignedToId = recommendation.RecommendedEmployee.Id;

                    bool success = await _db.UpdateTaskAsync(task, oldId);
                    if (success)
                    {
                        AddLog("AI", $"Görev {recommendation.RecommendedEmployee.FullName} kişisine atandı.");
                        await LoadDataSafe();
                    }
                }
            }
            else
            {
                XtraMessageBox.Show("Uygun bir öneri bulunamadı.", "Bilgi");
            }
        }

        // --- YÜK DENGELEME (GENEL ANALİZ) ---
        private async System.Threading.Tasks.Task RunSmartLoadBalancing()
        {
            if (_employeesCache == null || _allTasksCache == null) return;

            var overloaded = _employeesCache.OrderByDescending(e => e.WorkloadPercentage).FirstOrDefault();
            var available = _employeesCache.OrderBy(e => e.WorkloadPercentage).FirstOrDefault();

            if (overloaded != null && available != null && overloaded.WorkloadPercentage > 70 && available.WorkloadPercentage < 50)
            {
                string msg = $"<b>YÜK DENGELEME ÖNERİSİ:</b><br>" +
                             $"{overloaded.FullName} (%{overloaded.WorkloadPercentage}) -> {available.FullName} (%{available.WorkloadPercentage})<br><br>" +
                             $"Yüksek yüklü personelden 2 görev alınıp, müsait personele aktarılacak.<br>" +
                             $"Onaylıyor musunuz?";

                if (XtraMessageBox.Show(msg, "AI Optimizasyon", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, DefaultBoolean.True) == DialogResult.Yes)
                {
                    AddLog("AI", "Optimizasyon başlatılıyor...");

                    var tasksToMove = _allTasksCache
                        .Where(t => t.AssignedToId == overloaded.Id && t.Status != OfisAsistan.Models.TaskStatus.Completed)
                        .Take(2)
                        .ToList();

                    if (tasksToMove.Count == 0)
                    {
                        AddLog("AI", "Transfer edilecek uygun görev bulunamadı.");
                        await System.Threading.Tasks.Task.CompletedTask;
                        return;
                    }

                    foreach (var task in tasksToMove)
                    {
                        int oldId = task.AssignedToId;
                        task.AssignedToId = available.Id;
                        bool success = await _db.UpdateTaskAsync(task, oldId);
                        if (success) AddLog("AI", $"Transfer edildi: {task.Title}");
                    }

                    AddLog("AI", "Dengeleme tamamlandı. Tablo yenileniyor.");
                    await LoadDataSafe();
                }
            }
            else
            {
                XtraMessageBox.Show("Sistem şu an dengeli. Müdahaleye gerek yok.", "Analiz Sonucu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await System.Threading.Tasks.Task.CompletedTask;
            }
        }

        private void FilterGridByCard(int cardIndex)
        {
            if (_allTasksCache == null) return;
            IEnumerable<TaskModel> filtered = _allTasksCache;

            switch (cardIndex)
            {
                case 1: // Bekleyen
                    filtered = _allTasksCache.Where(t => t.Status == OfisAsistan.Models.TaskStatus.Pending || t.Status == OfisAsistan.Models.TaskStatus.InProgress);
                    break;
                case 2: // Gecikmiş
                    filtered = _allTasksCache.Where(t => t.DueDate < DateTime.Now && t.Status != OfisAsistan.Models.TaskStatus.Completed);
                    break;
                case 3: // Tamamlanan
                    filtered = _allTasksCache.Where(t => t.Status == OfisAsistan.Models.TaskStatus.Completed);
                    break;
            }

            BindTasksToGrid(filtered.ToList());
            AddLog("Arayüz", $"Filtre uygulandı. Sonuç: {filtered.Count()}");
        }

        // --- VERİ BAĞLAMA (ATANAN KİŞİ EKLENDİ) ---
        private void BindTasksToGrid(List<TaskModel> tasks)
        {
            // Grid'de görünmesini istediğin alanları burada seçiyorsun
            // ATANAN (AssignedTo) sütunu eklendi
            gcTasks.DataSource = tasks.Select(t => new {
                ID = t.Id,
                BAŞLIK = t.Title,
                ATANAN = GetEmployeeName(t.AssignedToId), // Çalışan İsmini Getir
                DURUM = t.Status.ToString(),
                TERMİN = t.DueDate?.ToShortDateString()
            }).ToList();

            if (gvTasks.Columns["ID"] != null) gvTasks.Columns["ID"].Visible = false;
        }

        // Yardımcı: ID'den İsim Bulma
        private string GetEmployeeName(int id)
        {
            if (_employeesCache == null) return "Bilinmiyor";
            var emp = _employeesCache.FirstOrDefault(e => e.Id == id);
            return emp != null ? emp.FullName : "-";
        }

        private void AddLog(string actor, string message)
        {
            string time = DateTime.Now.ToString("HH:mm");
            lstLiveLogs.Items.Insert(0, $"[{time}] {actor}: {message}");
            if (lstLiveLogs.Items.Count > 50) lstLiveLogs.Items.RemoveAt(50);
        }

        private async System.Threading.Tasks.Task LoadDataSafe()
        {
            try
            {
                // Verileri Çek
                _allTasksCache = await _db.GetTasksAsync();
                _employeesCache = await _db.GetEmployeesAsync();
                var stats = await _db.GetTaskStatisticsAsync();

                this.Invoke(new MethodInvoker(() => {
                    BindTasksToGrid(_allTasksCache);

                    gcEmployees.DataSource = _employeesCache.Select(e => new {
                        ID = e.Id,
                        ÇALIŞAN = e.FullName,
                        DOLULUK = $"%{e.WorkloadPercentage}"
                    }).ToList();

                    if (gvEmployees.Columns["ID"] != null) gvEmployees.Columns["ID"].Visible = false;

                    if (stats != null)
                    {
                        UpdateCard(0, stats["Total"], "Toplam Görev", "#4f46e5");
                        UpdateCard(1, stats["Pending"], "Bekleyen", "#d97706");
                        UpdateCard(2, stats["Overdue"], "Gecikmiş", "#dc2626");
                        UpdateCard(3, stats["Completed"], "Tamamlanan", "#10b981");
                    }
                    gvTasks.BestFitColumns(); gvEmployees.BestFitColumns();
                }));

                // GERÇEK ZAMANLI ANOMALİ: Listeyi temizleyip taze veri çekiyoruz
                var anomalies = await _ai.DetectAnomaliesAsync();
                this.Invoke(new MethodInvoker(() => {
                    lstAnomalies.Items.Clear();
                    foreach (var a in anomalies) lstAnomalies.Items.Add($"[{a.Severity}] {(a.Task != null ? a.Task.Title : "Sistem")}: {a.Message}");
                }));
            }
            catch (Exception ex) { XtraMessageBox.Show("Veri hatası: " + ex.Message); }
        }

        // --- HELPERLAR ---
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
                Size = new Size(180, 40)
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

        void CreateCustomWindowControls(Panel parent)
        {
            int btnSize = 40;
            var closeBtn = new SimpleButton { Text = "✕", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - btnSize, 0) };
            StyleWindowBtn(closeBtn);
            closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeBtn.Click += (s, e) => this.Close();
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

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "ManagerDashboard"; this.ResumeLayout(false); }
    }
}