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
using DevExpress.Utils;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;

namespace OfisAsistan.Forms
{
    public partial class ManagerDashboard : XtraForm
    {
        // Servisler
        private readonly DatabaseService _db;
        private readonly AIService _ai;
        private readonly NotificationService _ns;

        // UI Bileşenleri
        private GridControl gcTasks, gcEmployees;
        private GridView gvTasks, gvEmployees;
        private ListBoxControl lstAnomalies;
        private ListBoxControl lstLiveLogs;
        private ListBoxControl lstNotifications; // Bildirimler için
        private LabelControl[] statCards = new LabelControl[4];

        // Veri Önbelleği
        private List<TaskModel> _allTasksCache;
        private List<Employee> _employeesCache;

        // Tasarım Değişkenleri
        private Panel leftPanel;
        private Panel rightPanel;
        private bool isDragging = false;
        private Point dragStart;
        private GridHitInfo downHitInfo = null;

        // --- MODERN RENK PALETİ ---
        private readonly Color clrBackground = ColorTranslator.FromHtml("#F3F4F6"); // Açık Gri Zemin
        private readonly Color clrSurface = Color.White; // Beyaz Kartlar
        private readonly Color clrPrimary = ColorTranslator.FromHtml("#6366F1"); // Mor/İndigo
        private readonly Color clrPrimaryDark = ColorTranslator.FromHtml("#4F46E5"); // Hover Rengi
        private readonly Color clrTextTitle = ColorTranslator.FromHtml("#1F2937"); // Koyu Gri Yazı
        private readonly Color clrTextBody = ColorTranslator.FromHtml("#6B7280"); // Gri Metin
        private readonly Color clrBorder = ColorTranslator.FromHtml("#E5E7EB"); // İnce Çizgiler

        public ManagerDashboard(DatabaseService db, AIService ai, NotificationService ns)
        {
            _db = db; _ai = ai; _ns = ns;
            InitializeComponent();

            SetupModernContainer();
            SetupDashboardLayout();
            SetupNotificationHandler(); // Bildirim event handler'ını bağla

            this.Shown += async (s, e) => await LoadDataSafe();
        }

        private void SetupNotificationHandler()
        {
            if (_ns != null)
            {
                _ns.NotificationReceived += (sender, e) =>
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.Invoke(new Action(() =>
                        {
                            AddNotification(e.Title, e.Message, e.IsUrgent);
                        }));
                    }
                };
            }
        }

        private void AddNotification(string title, string message, bool isUrgent)
        {
            if (lstNotifications == null) return;

            string icon = isUrgent ? "🔴" : "🔵";
            string displayText = $"[{DateTime.Now:HH:mm}] {icon} {title}: {message}";
            
            lstNotifications.Items.Insert(0, displayText);
            
            // Maksimum 20 bildirim tut
            if (lstNotifications.Items.Count > 20)
            {
                lstNotifications.Items.RemoveAt(20);
            }

            // Yeni bildirim geldiğinde log'a da ekle
            AddLog("Bildirim", $"{title}: {message}");
        }

        private void SetupModernContainer()
        {
            this.Controls.Clear();
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(1600, 950);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrBackground;

            // Pencere Sürükleme
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < 80) { isDragging = true; dragStart = e.Location; } };
            this.MouseMove += (s, e) => { if (isDragging) { Point p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            this.MouseUp += (s, e) => { isDragging = false; };

            var mainSplit = new TableLayoutPanel();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.ColumnCount = 2;
            mainSplit.RowCount = 1;
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.Controls.Add(mainSplit);

            // SOL PANEL
            leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrPrimary, Padding = new Padding(0) };
            SetupLeftPanelContent();
            mainSplit.Controls.Add(leftPanel, 0, 0);

            // SAĞ PANEL
            rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrBackground, Padding = new Padding(25) };
            mainSplit.Controls.Add(rightPanel, 1, 0);

            CreateCustomWindowControls(rightPanel);
        }

        private void SetupLeftPanelContent()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            leftPanel.Controls.Add(layout);

            // LOGO
            var pnlHeader = new Panel { Dock = DockStyle.Fill };
            var lblTitle = new LabelControl
            {
                Text = "Ofis<br><size=14>Asistan AI</size>",
                AllowHtmlString = true,
                Appearance = { Font = new Font("Segoe UI", 24, FontStyle.Bold), ForeColor = Color.White, TextOptions = { HAlignment = HorzAlignment.Center, VAlignment = VertAlignment.Center } },
                AutoSizeMode = LabelAutoSizeMode.None,
                Dock = DockStyle.Fill
            };
            pnlHeader.Controls.Add(lblTitle);
            layout.Controls.Add(pnlHeader, 0, 0);

            // SIDEBAR BUTONLARI
            var pnlActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20, 20, 20, 0) };

            pnlActions.Controls.Add(CreateSidebarBtn("📢  Acil Toplantı", () => {
                AddLog("Sistem", "Tüm ekibe 'Acil Toplantı' daveti gönderildi.");
                XtraMessageBox.Show("Toplantı davetleri gönderildi.", "Başarılı");
            }));

            pnlActions.Controls.Add(CreateSidebarBtn("⚡  Hızlı Kontrol", () => FilterGridByCard(1)));

            pnlActions.Controls.Add(CreateSidebarBtn("📅  Takvim Modu", () => {
                try { new CalendarForm(_db).Show(); } catch (Exception ex) { XtraMessageBox.Show("Hata: " + ex.Message); }
            }));

            layout.Controls.Add(pnlActions, 0, 1);

            // LOG PANELİ
            var pnlLog = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            var lblLogHeader = new LabelControl { Text = "🔴 CANLI AKIŞ", Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 10), Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(180, 255, 255, 255) } };

            lstLiveLogs = new ListBoxControl
            {
                Dock = DockStyle.Fill,
                Appearance = { BackColor = Color.FromArgb(40, 0, 0, 0), ForeColor = Color.White, Font = new Font("Consolas", 9) },
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                ItemHeight = 28,
                ShowFocusRect = false
            };

            pnlLog.Controls.Add(lstLiveLogs);
            pnlLog.Controls.Add(lblLogHeader);
            layout.Controls.Add(pnlLog, 0, 2);
        }

        // SOL PANEL BUTON TASARIMI
        private SimpleButton CreateSidebarBtn(string text, Action onClick)
        {
            var btn = new SimpleButton
            {
                Text = text,
                Size = new Size(260, 50),
                Margin = new Padding(0, 0, 0, 15),
                Cursor = Cursors.Hand,
                Appearance = { Font = new Font("Segoe UI Semibold", 10), BackColor = Color.White, ForeColor = clrPrimary },
                AppearanceHovered = { BackColor = Color.FromArgb(240, 240, 255) },
                ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder // Düz görünüm
            };
            btn.PaintStyle = DevExpress.XtraEditors.Controls.PaintStyles.Light; // Arka planı yumuşat
            btn.Appearance.BackColor = Color.White; // Net beyaz
            btn.Appearance.TextOptions.HAlignment = HorzAlignment.Near;
            btn.Padding = new Padding(20, 0, 0, 0);

            // Düzgün görünmesi için stil ayarı
            btn.LookAndFeel.UseDefaultLookAndFeel = false;
            btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;

            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }

        private void SetupDashboardLayout()
        {
            var contentLayout = new TableLayoutPanel();
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.RowCount = 3;
            contentLayout.ColumnCount = 1;

            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F)); // Toolbar
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F)); // Kartlar
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Gridler

            rightPanel.Controls.Add(contentLayout);

            // --- 1. TOOLBAR ---
            var toolbarPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 5, 0, 0) };

            // Butonları oluştur
            var btnRefresh = CreateHeaderBtn("Verileri Yenile", "actions_refresh.svg", false);
            btnRefresh.Click += async (s, e) => await LoadDataSafe();

            var btnNew = CreateHeaderBtn("Yeni Görev", "actions_add.svg", true); // TRUE = Mor Buton
            btnNew.Click += async (s, e) => {
                var f = new CreateTaskForm(_db, _ai);
                if (f.ShowDialog() == DialogResult.OK) { AddLog("Kullanıcı", "Yeni görev."); await LoadDataSafe(); }
            };

            var btnLoad = CreateHeaderBtn("Yük Dengeleme (AI)", "outlook%20inspired/pivottable.svg", false);
            btnLoad.Click += async (s, e) => await RunSmartLoadBalancing();

            var btnRec = CreateHeaderBtn("AI Personel Önerisi", "actions_user.svg", false);
            btnRec.Click += async (s, e) => await RecommendEmployeeForSelectedTask();

            toolbarPanel.Controls.Add(btnRefresh);
            toolbarPanel.Controls.Add(btnNew);
            toolbarPanel.Controls.Add(btnLoad);
            toolbarPanel.Controls.Add(btnRec);

            contentLayout.Controls.Add(toolbarPanel, 0, 0);

            // --- 2. KARTLAR (KPI) - DÜZELTİLDİ ---
            var cardsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 4, Padding = new Padding(0, 10, 0, 10) };
            for (int i = 0; i < 4; i++) cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            for (int i = 0; i < 4; i++)
            {
                statCards[i] = new LabelControl
                {
                    AllowHtmlString = true, // HTML Özelliği Açıldı
                    AutoSizeMode = LabelAutoSizeMode.None,
                    Appearance = {
                        BackColor = clrSurface,
                        ForeColor = clrTextTitle,
                        TextOptions = { HAlignment = HorzAlignment.Center, VAlignment = VertAlignment.Center },
                        Font = new Font("Segoe UI", 12)
                    },
                    BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                    Dock = DockStyle.Fill,
                    Cursor = Cursors.Hand,
                    Tag = i
                };

                // Gölge hissi vermek için panel içine koyuyoruz
                var cardContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 15, 0) };
                if (i == 3) cardContainer.Padding = new Padding(0); // Sonuncuda boşluk yok

                statCards[i].Dock = DockStyle.Fill;
                cardContainer.Controls.Add(statCards[i]);

                int index = i;
                statCards[i].Click += (s, e) => FilterGridByCard(index);
                cardsLayout.Controls.Add(cardContainer, i, 0);
            }
            contentLayout.Controls.Add(cardsLayout, 0, 1);

            // --- 3. GRİDLER VE AI PANELİ ---
            var gridLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 2 };
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F)); // Görevler ve Çalışanlar
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F)); // AI Bildirimleri
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F)); // Sistem Bildirimleri

            gcTasks = CreateStylishGrid(); gvTasks = (GridView)gcTasks.MainView;
            gcEmployees = CreateStylishGrid(); gvEmployees = (GridView)gcEmployees.MainView;

            SetupDragAndDrop();
            SetupContextMenu();
            SetupDoubleClick();

            lstAnomalies = new ListBoxControl
            {
                Appearance = { Font = new Font("Segoe UI", 10), BackColor = clrSurface, ForeColor = clrTextBody },
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                ItemHeight = 30,
                Dock = DockStyle.Fill,
                ShowFocusRect = false
            };

            // Bildirimler listesi
            lstNotifications = new ListBoxControl
            {
                Appearance = { Font = new Font("Segoe UI", 9), BackColor = clrSurface, ForeColor = clrTextBody },
                BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
                ItemHeight = 35,
                Dock = DockStyle.Fill,
                ShowFocusRect = false
            };

            AddContentToGrid(gridLayout, gcTasks, "AKTİF GÖREV LİSTESİ", 0, 0);
            AddContentToGrid(gridLayout, gcEmployees, "EKİP YÜKÜ (HEDEF)", 1, 0);

            // AI Panel Wrapper
            var aiContainer = new Panel { Dock = DockStyle.Fill, BackColor = clrSurface, Padding = new Padding(15) };
            aiContainer.Controls.Add(lstAnomalies);
            aiContainer.Controls.Add(new LabelControl { Text = "⚠️  AI SİSTEM BİLDİRİMLERİ", Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 10), Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = clrTextBody } });

            var pnlWrapperOuter = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 10) };
            pnlWrapperOuter.Controls.Add(aiContainer);

            gridLayout.Controls.Add(pnlWrapperOuter, 0, 1);
            gridLayout.SetColumnSpan(pnlWrapperOuter, 2);

            // Bildirimler Panel Wrapper
            var notificationContainer = new Panel { Dock = DockStyle.Fill, BackColor = clrSurface, Padding = new Padding(15) };
            notificationContainer.Controls.Add(lstNotifications);
            var lblNotifications = new LabelControl 
            { 
                Text = "🔔 SİSTEM BİLDİRİMLERİ", 
                Dock = DockStyle.Top, 
                Padding = new Padding(0, 0, 0, 10), 
                Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = clrPrimary } 
            };
            notificationContainer.Controls.Add(lblNotifications);

            var pnlNotificationWrapper = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 0) };
            pnlNotificationWrapper.Controls.Add(notificationContainer);

            gridLayout.Controls.Add(pnlNotificationWrapper, 0, 2);
            gridLayout.SetColumnSpan(pnlNotificationWrapper, 2);

            contentLayout.Controls.Add(gridLayout, 0, 2);
        }

        // --- MODERN GRİD ---
        private GridControl CreateStylishGrid()
        {
            var gc = new GridControl { Dock = DockStyle.Fill };
            gc.LookAndFeel.UseDefaultLookAndFeel = false;
            gc.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;

            var gv = new GridView(gc);
            gc.MainView = gv;

            gv.OptionsView.ShowGroupPanel = false;
            gv.OptionsView.ShowIndicator = false;
            gv.OptionsView.ShowVerticalLines = DefaultBoolean.False;
            gv.OptionsView.ShowHorizontalLines = DefaultBoolean.True;
            gv.OptionsView.EnableAppearanceEvenRow = false;

            gv.RowHeight = 40;
            gv.OptionsView.RowAutoHeight = true;

            gv.Appearance.HeaderPanel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            gv.Appearance.HeaderPanel.ForeColor = Color.Gray;
            gv.Appearance.HeaderPanel.BackColor = clrSurface;

            gv.Appearance.Row.Font = new Font("Segoe UI", 10);
            gv.Appearance.Row.ForeColor = clrTextTitle;
            gv.Appearance.Row.BackColor = clrSurface;

            gv.Appearance.FocusedRow.BackColor = Color.FromArgb(238, 242, 255);
            gv.Appearance.FocusedRow.ForeColor = clrPrimary;
            gv.Appearance.SelectedRow.BackColor = Color.FromArgb(238, 242, 255);

            gv.Appearance.HorzLine.BackColor = clrBorder;
            gv.OptionsBehavior.Editable = false;
            gv.FocusRectStyle = DrawFocusRectStyle.None;

            return gc;
        }

        // --- ÜST BUTONLARIN DÜZELTİLMİŞ HALİ ---
        private SimpleButton CreateHeaderBtn(string text, string iconName, bool isPrimary)
        {
            var btn = new SimpleButton
            {
                Text = text,
                ImageOptions = { SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage(iconName), SvgImageSize = new Size(16, 16) },
                Appearance = {
                    Font = new Font("Segoe UI Semibold", 9), 
                    // Eğer Primary ise Mor, değilse Beyaz arka plan
                    BackColor = isPrimary ? clrPrimary : clrSurface,
                    ForeColor = isPrimary ? Color.White : clrTextBody
                },
                AutoWidthInLayoutControl = true,
                Padding = new Padding(10, 5, 10, 5),
                Cursor = Cursors.Hand,
                Size = new Size(180, 42),
                Margin = new Padding(0, 0, 10, 0),
                AllowFocus = false // Kesik çizgi oluşmasını engeller
            };

            btn.LookAndFeel.UseDefaultLookAndFeel = false;
            btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;

            // Kenarlık stili: Primary ise kenarlık yok, Secondary ise ince gri kenarlık
            btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            btn.Appearance.BorderColor = isPrimary ? clrPrimary : clrBorder;

            // Hover Efekti
            btn.MouseEnter += (s, e) => btn.Appearance.BackColor = isPrimary ? clrPrimaryDark : Color.FromArgb(249, 250, 251);
            btn.MouseLeave += (s, e) => btn.Appearance.BackColor = isPrimary ? clrPrimary : clrSurface;

            return btn;
        }

        // --- KART GÜNCELLEME (HTML TABLO YERİNE GÜVENLİ FORMAT) ---
        private void UpdateCard(int idx, object val, string title, string colorHex)
        {
            // Eski "<table>" kodu yerine bu güvenli formatı kullanıyoruz.
            // DevExpress her sürümde bu tag'leri destekler.
            string safeHtml = $@"
            <br>
            <size=10><color=#9CA3AF>{title.ToUpper()}</color></size><br>
            <size=26><b><color={colorHex}>{val}</color></b></size>
            <br>";

            statCards[idx].Text = safeHtml;
        }

        private void AddContentToGrid(TableLayoutPanel parent, Control content, string title, int col, int row)
        {
            var container = new Panel { Dock = DockStyle.Fill, BackColor = clrSurface, Padding = new Padding(0) };

            var lbl = new LabelControl
            {
                Text = title,
                Dock = DockStyle.Top,
                Padding = new Padding(15, 15, 0, 10),
                Appearance = { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#9CA3AF") },
                AutoSizeMode = LabelAutoSizeMode.None,
                Height = 45
            };

            content.Dock = DockStyle.Fill;
            container.Controls.Add(content);
            container.Controls.Add(lbl);

            var wrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = clrBorder };
            if (col == 0) wrapper.Margin = new Padding(0, 0, 15, 0);
            wrapper.Controls.Add(container);

            parent.Controls.Add(wrapper, col, row);
        }

        // --- MEVCUT FONKSİYONLAR (AYNEN KORUNDU) ---
        private void SetupDoubleClick()
        {
            gvTasks.DoubleClick += async (s, e) => {
                var view = s as GridView;
                var pt = view.GridControl.PointToClient(Control.MousePosition);
                var info = view.CalcHitInfo(pt);
                if (info.InRow)
                {
                    int id = (int)view.GetRowCellValue(info.RowHandle, "ID");
                    var detailForm = new TaskDetailForm(_db, id, 1, "Yönetici");
                    if (detailForm.ShowDialog() == DialogResult.OK) { AddLog("Kullanıcı", $"Görev güncellendi (ID: {id})"); await LoadDataSafe(); }
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
                if (e.Button == MouseButtons.Left && hitInfo.InRow && hitInfo.HitTest != GridHitTest.RowIndicator) downHitInfo = hitInfo;
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
                        gcTasks.DoDragDrop(new { TaskID = taskId, Title = taskTitle, CurrentAssignedId = originalTask?.AssignedToId }, DragDropEffects.Move);
                        downHitInfo = null;
                        DevExpress.Utils.DXMouseEventArgs.GetMouseArgs(e).Handled = true;
                    }
                }
            };
            gcEmployees.AllowDrop = true;
            gcEmployees.DragOver += (s, e) => { if (e.Data.GetDataPresent(typeof(object))) e.Effect = DragDropEffects.Move; else e.Effect = DragDropEffects.None; };
            gcEmployees.DragDrop += async (s, e) => {
                var grid = (GridControl)s; var view = (GridView)grid.MainView;
                dynamic srcData = e.Data.GetData(e.Data.GetFormats()[0]);
                int taskId = srcData.TaskID; string taskTitle = srcData.Title; int? oldId = srcData.CurrentAssignedId;

                Point p = grid.PointToClient(new Point(e.X, e.Y));
                GridHitInfo hit = view.CalcHitInfo(p);
                if (hit.InRow)
                {
                    int targetEmpId = (int)view.GetRowCellValue(hit.RowHandle, "ID");
                    string empName = (string)view.GetRowCellValue(hit.RowHandle, "ÇALIŞAN");
                    if (XtraMessageBox.Show($"'{taskTitle}' görevi '{empName}' kişisine atansın mı?", "Atama Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        var task = _allTasksCache.FirstOrDefault(t => t.Id == taskId);
                        if (task != null)
                        {
                            task.AssignedToId = targetEmpId;
                            if (await _db.UpdateTaskAsync(task, oldId)) { AddLog("Sistem", $"BAŞARILI: {taskTitle} -> {empName}"); await LoadDataSafe(); }
                        }
                    }
                }
            };
        }

        private void SetupContextMenu()
        {
            var menu = new ContextMenuStrip();
            var itemNudge = new ToolStripMenuItem("🔔 Personele Hatırlatma Gönder");
            itemNudge.Click += (s, e) => { var row = gvTasks.GetFocusedRow(); if (row == null) return; AddLog("Yönetici", $"Dürtme gönderildi: {((dynamic)row).BAŞLIK}"); };
            menu.Items.Add(itemNudge);
            gcTasks.ContextMenuStrip = menu;
        }

        // --- MANAGER DASHBOARD İÇİN DÜZELTİLMİŞ METOT ---
        private async System.Threading.Tasks.Task RecommendEmployeeForSelectedTask()
        {
            // 1. Satır seçili mi?
            int focusedRowHandle = gvTasks.FocusedRowHandle;
            if (focusedRowHandle < 0)
            {
                XtraMessageBox.Show("Lütfen listeden bir görev seçin.", "Uyarı");
                return;
            }

            // 2. ID'yi GÜVENLİ AL (Eski kod burda patlıyordu)
            // DevExpress'in kendi fonksiyonunu kullanıyoruz, dynamic yerine.
            object idObj = gvTasks.GetRowCellValue(focusedRowHandle, "ID");

            if (idObj == null)
            {
                XtraMessageBox.Show("Seçilen satırın ID'si okunamadı.", "Hata");
                return;
            }

            int taskId = Convert.ToInt32(idObj);

            // 3. Görevi Bul
            var task = _allTasksCache.FirstOrDefault(t => t.Id == taskId);
            if (task == null)
            {
                XtraMessageBox.Show("Görev veritabanında bulunamadı.", "Hata");
                return;
            }

            // 4. AI Çağır
            // Kullanıcı beklediğini bilsin diye imleci değiştir
            this.Cursor = Cursors.WaitCursor;

            var rec = await _ai.RecommendEmployeeForTaskAsync(task);

            this.Cursor = Cursors.Default;

            if (rec != null)
            {
                string msg = $"<b>ÖNERİLEN PERSONEL:</b> {rec.RecommendedEmployee.FullName}\n\n" +
                             $"<b>NEDEN:</b> {rec.Reason}\n\n" +
                             $"Atamayı onaylıyor musunuz?";

                if (XtraMessageBox.Show(msg, "AI Atama Önerisi", MessageBoxButtons.YesNo, MessageBoxIcon.Information, DefaultBoolean.True) == DialogResult.Yes)
                {
                    int? oldId = task.AssignedToId;
                    task.AssignedToId = rec.RecommendedEmployee.Id;

                    bool success = await _db.UpdateTaskAsync(task, oldId);
                    if (success)
                    {
                        AddLog("AI", $"Görev {rec.RecommendedEmployee.FullName}'a atandı.");
                        await LoadDataSafe();
                    }
                }
            }
            else
            {
                XtraMessageBox.Show("Uygun öneri bulunamadı veya AI yanıt vermedi.", "Bilgi");
            }
        }

        private async System.Threading.Tasks.Task RunSmartLoadBalancing()
        {
            if (_employeesCache == null) return;
            var overloaded = _employeesCache.OrderByDescending(e => e.WorkloadPercentage).FirstOrDefault();
            var available = _employeesCache.OrderBy(e => e.WorkloadPercentage).FirstOrDefault();
            if (overloaded != null && available != null && overloaded.WorkloadPercentage > Constants.WORKLOAD_OVERLOAD_THRESHOLD && available.WorkloadPercentage < Constants.WORKLOAD_AVAILABLE_THRESHOLD)
            {
                if (XtraMessageBox.Show($"{overloaded.FullName} (%{overloaded.WorkloadPercentage}) üzerindeki yükü {available.FullName} kişisine aktarmak istiyor musunuz?", "AI Dengeleme", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    var tasks = _allTasksCache.Where(t => t.AssignedToId == overloaded.Id && t.Status != OfisAsistan.Models.TaskStatus.Completed).Take(2).ToList();
                    foreach (var t in tasks) { int old = t.AssignedToId; t.AssignedToId = available.Id; await _db.UpdateTaskAsync(t, old); }
                    AddLog("AI", "Dengeleme tamamlandı."); await LoadDataSafe();
                }
            }
            else XtraMessageBox.Show("Sistem dengeli.");
        }

        private void FilterGridByCard(int cardIndex)
        {
            if (_allTasksCache == null) return;
            IEnumerable<TaskModel> filtered = _allTasksCache;
            switch (cardIndex)
            {
                case 0: filtered = _allTasksCache; break; // Tüm görevler
                case 1: filtered = _allTasksCache.Where(t => t != null && (t.Status == OfisAsistan.Models.TaskStatus.Pending || t.Status == OfisAsistan.Models.TaskStatus.InProgress)); break;
                case 2: filtered = _allTasksCache.Where(t => t != null && t.DueDate.HasValue && t.DueDate.Value < DateTime.Now && t.Status != OfisAsistan.Models.TaskStatus.Completed); break;
                case 3: filtered = _allTasksCache.Where(t => t != null && t.Status == OfisAsistan.Models.TaskStatus.Completed); break;
                default: filtered = _allTasksCache; break; // Varsayılan: tüm görevler
            }
            BindTasksToGrid(filtered.ToList());
            AddLog("Arayüz", $"Filtre Sonucu: {filtered.Count()}");
        }

        private void BindTasksToGrid(List<TaskModel> tasks)
        {
            gcTasks.DataSource = tasks.Select(t => new { ID = t.Id, BAŞLIK = t.Title, ATANAN = GetEmployeeName(t.AssignedToId), DURUM = t.Status.ToString(), TERMİN = t.DueDate?.ToShortDateString() }).ToList();
            if (gvTasks.Columns["ID"] != null) gvTasks.Columns["ID"].Visible = false;
        }

        private string GetEmployeeName(int id) { var emp = _employeesCache?.FirstOrDefault(e => e.Id == id); return emp != null ? emp.FullName : "-"; }

        private void AddLog(string actor, string message) { lstLiveLogs.Items.Insert(0, $"[{DateTime.Now:HH:mm}] {actor}: {message}"); if (lstLiveLogs.Items.Count > Constants.MAX_LOG_ITEMS) lstLiveLogs.Items.RemoveAt(Constants.MAX_LOG_ITEMS); }

        private async System.Threading.Tasks.Task LoadDataSafe()
        {
            try
            {
                _allTasksCache = await _db.GetTasksAsync(); _employeesCache = await _db.GetEmployeesAsync(); var stats = await _db.GetTaskStatisticsAsync();
                this.Invoke(new MethodInvoker(() => {
                    BindTasksToGrid(_allTasksCache);
                    gcEmployees.DataSource = _employeesCache.Select(e => new { ID = e.Id, ÇALIŞAN = e.FullName, DOLULUK = $"%{e.WorkloadPercentage}" }).ToList();
                    if (gvEmployees.Columns["ID"] != null) gvEmployees.Columns["ID"].Visible = false;
                    if (stats != null)
                    {
                        UpdateCard(0, stats["Total"], "Toplam Görev", "#6366F1");
                        UpdateCard(1, stats["Pending"], "Bekleyen", "#F59E0B");
                        UpdateCard(2, stats["Overdue"], "Gecikmiş", "#EF4444");
                        UpdateCard(3, stats["Completed"], "Tamamlanan", "#10B981");
                    }
                    gvTasks.BestFitColumns(); gvEmployees.BestFitColumns();
                }));
                var anomalies = await _ai.DetectAnomaliesAsync();
                this.Invoke(new MethodInvoker(() => {
                    lstAnomalies.Items.Clear();
                    foreach (var a in anomalies) lstAnomalies.Items.Add($"[{a.Severity}] {(a.Task != null ? a.Task.Title : "Sistem")}: {a.Message}");
                }));
            }
            catch (Exception ex) { XtraMessageBox.Show(ex.Message); }
        }

        void CreateCustomWindowControls(Panel parent)
        {
            int btnSize = 40;
            var closeBtn = new SimpleButton { Text = "✕", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - btnSize, 0) };
            StyleWindowBtn(closeBtn); closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; closeBtn.Click += (s, e) => this.Close(); parent.Controls.Add(closeBtn);
            var maxBtn = new SimpleButton { Text = "□", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - (btnSize * 2), 0) };
            StyleWindowBtn(maxBtn); maxBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; maxBtn.Click += (s, e) => this.WindowState = (this.WindowState == FormWindowState.Normal) ? FormWindowState.Maximized : FormWindowState.Normal; parent.Controls.Add(maxBtn);
            var minBtn = new SimpleButton { Text = "─", Size = new Size(btnSize, btnSize), Location = new Point(parent.Width - (btnSize * 3), 0) };
            StyleWindowBtn(minBtn); minBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right; minBtn.Click += (s, e) => this.WindowState = FormWindowState.Minimized; parent.Controls.Add(minBtn);
        }
        void StyleWindowBtn(SimpleButton btn) { btn.Appearance.BackColor = Color.Transparent; btn.Appearance.ForeColor = Color.Gray; btn.Appearance.Font = new Font("Segoe UI", 12); btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder; btn.LookAndFeel.UseDefaultLookAndFeel = false; }
        private void InitializeComponent() { this.SuspendLayout(); this.Name = "ManagerDashboard"; this.ResumeLayout(false); }
    }
}