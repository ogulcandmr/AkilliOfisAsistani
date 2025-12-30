using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.Utils;
using DevExpress.XtraLayout;
using DevExpress.XtraBars.Alerter;
using DevExpress.XtraBars;
using DevExpress.XtraSplashScreen;

// --- ALIASLAR ---
using AppTask = OfisAsistan.Models.Task;
using SysTask = System.Threading.Tasks.Task;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;

namespace OfisAsistan.Forms
{
    public partial class EmployeeWorkspace : XtraForm
    {
        private readonly DatabaseService _databaseService;
        private readonly AIService _aiService;
        private readonly int _employeeId;

        // UI Bileşenleri
        private ListBoxControl lstPending, lstInProgress, lstCompleted;
        private MemoEdit txtChatHistory, txtQuickNotes, txtBriefing; // txtBriefing eklendi
        private TextEdit txtChatInput;
        private AlertControl alertControl;

        // Menü
        private PopupMenu taskPopupMenu;
        private BarManager barManager;

        // Drag & Drop
        private ListBoxControl draggedSourceList;

        // --- 1. RENKLER (İstediğin Açık Tonlar Geri Geldi) ---
        private readonly Color clrSidebar = Color.FromArgb(99, 102, 241);     // Canlı İndigo
        private readonly Color clrSidebarDark = Color.FromArgb(67, 56, 202);  // Gradient bitişi
        private readonly Color clrBackground = Color.FromArgb(243, 244, 246); // Açık Gri Zemin

        // Kart Başlık Renkleri (Göz alıcı)
        private readonly Color clrHeaderPending = Color.FromArgb(255, 179, 0);   // Amber
        private readonly Color clrHeaderProgress = Color.FromArgb(30, 136, 229); // Mavi
        private readonly Color clrHeaderDone = Color.FromArgb(67, 160, 71);      // Yeşil

        public EmployeeWorkspace(DatabaseService databaseService, AIService aiService, int employeeId)
        {
            _databaseService = databaseService;
            _aiService = aiService;
            _employeeId = employeeId;

            InitializeComponent();
            InitializeCustomUI();
            InitializeContextMenu();

            alertControl = new AlertControl();
            alertControl.AutoFormDelay = 4000;

            // Verileri ve Brifingi Yükle
            this.Shown += async (s, e) => {
                await LoadDataAsync();
                await LoadDailyBriefing();
            };
        }

        // --- 2. KART TASARIMI (Düzgün Hizalama) ---
        public class TaskDisplayItem
        {
            public AppTask Task { get; set; }

            public override string ToString()
            {
                string pColor = Task.Priority.ToString() == "High" ? "#E53935" : (Task.Priority.ToString() == "Medium" ? "#FB8C00" : "#43A047");
                string dateStr = Task.DueDate.HasValue ? Task.DueDate.Value.ToString("dd MMM") : "-";

                // Açıklama Metni Düzenleme (Satırları koru ama uzunsa kes)
                string desc = Task.Description ?? "Açıklama yok.";
                if (desc.Length > 80) desc = desc.Substring(0, 77) + "...";

                // HTML Şablonu:
                // 1. Satır: Başlık (Kalın)
                // 2. Satır: Açıklama (İnce, Gri)
                // 3. Satır: Öncelik ve Tarih
                return $"<size=11><b>{Task.Title}</b></size><br>" +
                       $"<size=9><color=#606060>{desc}</color></size><br><br>" +
                       $"<size=8><color={pColor}><b>● {Task.Priority}</b></color>      <color=gray>📅 {dateStr}</color></size>";
            }
        }

        // --- 3. UI TASARIMI ---
        private void InitializeCustomUI()
        {
            this.Text = "Ofis Asistanı";
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = clrBackground;

            // Ana İskelet
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280F)); // Sol Menü
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Sağ İçerik
            this.Controls.Add(mainLayout);

            // --- SOL PANEL (AÇIK MOR GRADIENT) ---
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            leftPanel.Paint += (s, e) => {
                using (LinearGradientBrush brush = new LinearGradientBrush(leftPanel.ClientRectangle, clrSidebar, clrSidebarDark, 45F))
                    e.Graphics.FillRectangle(brush, leftPanel.ClientRectangle);
            };
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var lblLogo = new LabelControl { Text = "OFİS\nASİSTANI", Appearance = { Font = new Font("Segoe UI", 28, FontStyle.Bold), ForeColor = Color.White, TextOptions = { HAlignment = HorzAlignment.Center } }, Dock = DockStyle.Top, Padding = new Padding(0, 60, 0, 0), AutoSizeMode = LabelAutoSizeMode.None, Height = 200, BackColor = Color.Transparent };
            leftPanel.Controls.Add(lblLogo);

            // --- SAĞ İÇERİK ---
            var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            mainLayout.Controls.Add(contentPanel, 1, 0);

            // ÜST BAR (Pencere Kontrolleri + Butonlar)
            var topBar = new Panel { Dock = DockStyle.Top, Height = 50, Margin = new Padding(0, 0, 0, 15) };

            // Pencere Kontrolleri (Sağ Üst - Görünür Renkli Butonlar)
            var pnlWinControls = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, Width = 120 };
            var btnMin = CreateWindowBtn("_", (s, e) => this.WindowState = FormWindowState.Minimized);
            var btnMax = CreateWindowBtn("□", (s, e) => this.WindowState = (this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized));
            var btnClose = CreateWindowBtn("✕", (s, e) => this.Close(), true);
            pnlWinControls.Controls.AddRange(new Control[] { btnMin, btnMax, btnClose });
            topBar.Controls.Add(pnlWinControls);

            // İşlem Butonları (Sol Üst)
            var pnlTools = new FlowLayoutPanel { Dock = DockStyle.Left, FlowDirection = FlowDirection.LeftToRight, Width = 600 };
            var btnRefresh = CreateModernButton("🔄 Yenile", Color.FromArgb(33, 150, 243), async (s, e) => await LoadDataAsync());
            var btnAiWizard = CreateModernButton("✨ Alt Görev Sihirbazı", Color.FromArgb(156, 39, 176), async (s, e) => await OpenAiWizard());

            pnlTools.Controls.AddRange(new Control[] { btnRefresh, btnAiWizard });
            topBar.Controls.Add(pnlTools);

            contentPanel.Controls.Add(topBar);

            // --- İÇERİK BÖLÜNMESİ (ÜST: KANBAN (%55) / ALT: ARAÇLAR (%45)) ---
            var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 15, 0, 0) };
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            contentPanel.Controls.Add(splitLayout);

            // A. KANBAN ALANI (3 Sütun)
            var kanbanGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 20) };
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            kanbanGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));

            lstPending = CreateKanbanList();
            lstInProgress = CreateKanbanList();
            lstCompleted = CreateKanbanList();

            AddKanbanColumn(kanbanGrid, "📋 BEKLEYENLER", lstPending, 0, clrHeaderPending);
            AddKanbanColumn(kanbanGrid, "💻 YÜRÜTÜLEN", lstInProgress, 1, clrHeaderProgress);
            AddKanbanColumn(kanbanGrid, "✅ TAMAMLANAN", lstCompleted, 2, clrHeaderDone);

            splitLayout.Controls.Add(kanbanGrid, 0, 0);

            // B. ARAÇLAR ALANI (3 Sütun: Brifing | Notlar | Chat)
            var toolsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            toolsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // Brifing
            toolsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F)); // Notlar (Ortada geniş)
            toolsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // Chat

            // B1. GÜNLÜK BRİFİNG (Pomodoro Yerine Geldi)
            var pnlBrief = CreateCardPanel("📢 Günlük Brifing");
            txtBriefing = new MemoEdit { Dock = DockStyle.Fill, Properties = { ReadOnly = true, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder, Appearance = { Font = new Font("Segoe UI", 10), BackColor = Color.White } } };
            pnlBrief.Controls.Add(txtBriefing);
            toolsGrid.Controls.Add(pnlBrief, 0, 0);

            // B2. HIZLI NOTLAR
            var pnlNotes = CreateCardPanel("📝 Hızlı Notlar");
            txtQuickNotes = new MemoEdit { Dock = DockStyle.Fill, Properties = { BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder, NullText = "Not almak için buraya yazın..." } };
            pnlNotes.Controls.Add(txtQuickNotes);
            toolsGrid.Controls.Add(pnlNotes, 1, 0);

            // B3. AI CHAT
            var pnlChat = CreateCardPanel("🤖 Asistan");
            txtChatHistory = new MemoEdit { Dock = DockStyle.Fill, Properties = { ReadOnly = true, BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder } };
            var pnlInput = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(2), BackColor = Color.WhiteSmoke };
            txtChatInput = new TextEdit { Dock = DockStyle.Fill, Properties = { NullText = "Bir şeyler sor..." } };
            txtChatInput.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await SendMessageToAI(); };
            pnlInput.Controls.Add(txtChatInput);
            pnlChat.Controls.Add(txtChatHistory); pnlChat.Controls.Add(pnlInput);
            toolsGrid.Controls.Add(pnlChat, 2, 0);

            splitLayout.Controls.Add(toolsGrid, 0, 1);

            AttachListEvents(lstPending);
            AttachListEvents(lstInProgress);
            AttachListEvents(lstCompleted);
        }

        // --- 4. YENİ ÖZELLİK: GÜNLÜK BRİFİNG ---
        private async SysTask LoadDailyBriefing()
        {
            txtBriefing.Text = "AI, görevlerinizi analiz ediyor ve brifing hazırlıyor...";
            try
            {
                string briefing = await _aiService.GenerateDailyBriefingAsync(_employeeId);
                txtBriefing.Text = briefing;
            }
            catch
            {
                txtBriefing.Text = "Brifing yüklenirken hata oluştu.";
            }
        }

        // --- 5. AI ALT GÖREV SİHİRBAZI ---
        private async SysTask OpenAiWizard()
        {
            var selectedItem = GetSelectedTaskItem();
            if (selectedItem == null)
            {
                alertControl.Show(this, "Uyarı", "Lütfen işlem yapılacak görevi seçin.", "", (Image)null);
                return;
            }

            IOverlaySplashScreenHandle overlay = SplashScreenManager.ShowOverlayForm(this);

            try
            {
                var subTasks = await _aiService.BreakDownTaskAsync(selectedItem.Task.Title + " - " + selectedItem.Task.Description);
                SplashScreenManager.CloseOverlayForm(overlay);

                if (subTasks != null && subTasks.Any())
                {
                    string msg = $"AI, '{selectedItem.Task.Title}' için {subTasks.Count} alt adım önerdi. Notlara ekleyelim mi?";
                    if (XtraMessageBox.Show(msg, "AI Planlama", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"\r\n=== 📌 PLAN: {selectedItem.Task.Title.ToUpper()} ===");
                        foreach (var st in subTasks) sb.AppendLine($"[ ] {st.Title} ({st.EstimatedHours}s)");
                        sb.AppendLine("================================\r\n");

                        txtQuickNotes.Text += sb.ToString();
                        txtQuickNotes.SelectionStart = txtQuickNotes.Text.Length;
                        txtQuickNotes.ScrollToCaret();

                        alertControl.Show(this, "Kaydedildi", "Plan notlara eklendi.", "", (Image)null);
                    }
                }
                else
                {
                    XtraMessageBox.Show("AI yanıt veremedi.", "Hata");
                }
            }
            catch (Exception ex)
            {
                if (overlay != null) SplashScreenManager.CloseOverlayForm(overlay);
                XtraMessageBox.Show("Hata: " + ex.Message);
            }
        }

        // --- 6. VERİ YÜKLEME ---
        private async SysTask LoadDataAsync()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync(_employeeId);
                if (this.IsHandleCreated)
                {
                    this.Invoke(new MethodInvoker(() => {
                        lstPending.Items.Clear(); lstInProgress.Items.Clear(); lstCompleted.Items.Clear();
                        foreach (var t in tasks)
                        {
                            var item = new TaskDisplayItem { Task = t };
                            if (t.Status == TaskStatusModel.Pending) lstPending.Items.Add(item);
                            else if (t.Status == TaskStatusModel.InProgress) lstInProgress.Items.Add(item);
                            else if (t.Status == TaskStatusModel.Completed) lstCompleted.Items.Add(item);
                        }
                    }));
                }
            }
            catch { }
        }

        // --- 7. OLAYLAR ---
        private void AttachListEvents(ListBoxControl list)
        {
            list.Click += (s, e) => {
                if (list != lstPending) lstPending.SelectedIndex = -1;
                if (list != lstInProgress) lstInProgress.SelectedIndex = -1;
                if (list != lstCompleted) lstCompleted.SelectedIndex = -1;
            };

            list.MouseDown += (s, e) => {
                var index = list.IndexFromPoint(e.Location);
                if (index != -1)
                {
                    list.SelectedIndex = index;
                    if (e.Button == MouseButtons.Right) taskPopupMenu.ShowPopup(list.PointToScreen(e.Location));
                    else if (e.Button == MouseButtons.Left) { draggedSourceList = list; list.DoDragDrop(list.SelectedItem, DragDropEffects.Move); }
                }
            };

            list.DoubleClick += (s, e) => {
                var item = GetSelectedTaskItem();
                if (item != null)
                {
                    try
                    {
                        var f = new TaskDetailForm(_databaseService, item.Task.Id, _employeeId, "Employee");
                        if (f.ShowDialog() == DialogResult.OK) _ = LoadDataAsync();
                    }
                    catch { XtraMessageBox.Show(item.Task.Description, item.Task.Title); }
                }
            };

            list.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            list.DragDrop += async (s, e) => {
                var targetList = s as ListBoxControl;
                var item = e.Data.GetData(typeof(TaskDisplayItem)) as TaskDisplayItem;
                if (item != null && targetList != draggedSourceList)
                {
                    var newStatus = targetList == lstInProgress ? TaskStatusModel.InProgress : (targetList == lstCompleted ? TaskStatusModel.Completed : TaskStatusModel.Pending);
                    if (draggedSourceList == lstCompleted && newStatus != TaskStatusModel.Completed) return;

                    targetList.Items.Add(item); draggedSourceList.Items.Remove(item);
                    item.Task.Status = newStatus;
                    await _databaseService.UpdateTaskAsync(item.Task, _employeeId);
                }
            };
        }

        private TaskDisplayItem GetSelectedTaskItem()
        {
            if (lstPending.SelectedIndex != -1) return lstPending.SelectedItem as TaskDisplayItem;
            if (lstInProgress.SelectedIndex != -1) return lstInProgress.SelectedItem as TaskDisplayItem;
            if (lstCompleted.SelectedIndex != -1) return lstCompleted.SelectedItem as TaskDisplayItem;
            return null;
        }

        private void InitializeContextMenu()
        {
            barManager = new BarManager { Form = this };
            taskPopupMenu = new PopupMenu(barManager);
            var btnNotes = new BarButtonItem(barManager, "Notlara Kopyala");
            btnNotes.ImageOptions.SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("notes");
            btnNotes.ItemClick += (s, e) => {
                var item = GetSelectedTaskItem();
                if (item != null)
                {
                    txtQuickNotes.Text += $"\n📌 {item.Task.Title}\n{item.Task.Description}\n";
                    alertControl.Show(this, "Başarılı", "Notlara eklendi.", "", (Image)null);
                }
            };
            taskPopupMenu.AddItem(btnNotes);
        }

        private async SysTask SendMessageToAI()
        {
            string t = txtChatInput.Text.Trim(); if (string.IsNullOrEmpty(t)) return;
            txtChatHistory.Text += $"Ben: {t}\n"; txtChatInput.Text = ""; txtChatInput.Enabled = false;
            try { txtChatHistory.Text += $"AI: {await _aiService.ChatWithAssistantAsync(t)}\n\n"; }
            catch { }
            finally { txtChatInput.Enabled = true; txtChatInput.Focus(); }
        }

        // --- TASARIM YARDIMCILARI ---
        private ListBoxControl CreateKanbanList() => new ListBoxControl
        {
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
            Appearance = { Font = new Font("Segoe UI", 10), BackColor = Color.White, ForeColor = Color.FromArgb(40, 40, 40) },
            ItemHeight = 120, // Kart yüksekliği
            AllowHtmlDraw = DefaultBoolean.True,
            Dock = DockStyle.Fill
        };

        private void AddKanbanColumn(TableLayoutPanel parent, string title, Control list, int col, Color color)
        {
            // BAŞLIK DÜZELTMESİ: Label ve List'i Panel içine düzgünce yerleştirdik.
            var pnl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 0), BackColor = Color.White, Padding = new Padding(1) };

            // Başlık (Üstte Sabit)
            var header = new LabelControl
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 45,
                AutoSizeMode = LabelAutoSizeMode.None,
                Appearance = { BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), TextOptions = { HAlignment = HorzAlignment.Center } }
            };

            list.Dock = DockStyle.Fill;

            // Önce listeyi ekle (Fill), sonra başlığı ekle (Top) - WinForms mantığıyla başlık üstte kalır.
            // Ama en garantisi:
            pnl.Controls.Add(list);
            pnl.Controls.Add(header);

            parent.Controls.Add(pnl, col, 0);
        }

        private Panel CreateCardPanel(string title)
        {
            var pnl = new Panel { BackColor = Color.White, Dock = DockStyle.Fill, Margin = new Padding(10, 5, 10, 5), Padding = new Padding(5) };
            var hdr = new LabelControl { Text = title, Dock = DockStyle.Top, Height = 30, Appearance = { Font = new Font("Segoe UI Semibold", 10), ForeColor = Color.Gray } };
            pnl.Controls.Add(hdr); return pnl;
        }

        private SimpleButton CreateModernButton(string text, Color color, EventHandler onClick)
        {
            var btn = new SimpleButton { Text = text, Size = new Size(160, 35), Cursor = Cursors.Hand };
            btn.Appearance.BackColor = color; btn.Appearance.ForeColor = Color.White; btn.Appearance.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
            btn.Click += onClick; return btn;
        }

        private SimpleButton CreateWindowBtn(string text, EventHandler onClick, bool isClose = false)
        {
            var btn = new SimpleButton { Text = text, Size = new Size(40, 35), Cursor = Cursors.Hand };
            btn.Appearance.BackColor = Color.FromArgb(220, 220, 225); // Hafif gri arka plan (Görünür olması için)
            btn.Appearance.ForeColor = Color.Black;
            btn.Appearance.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btn.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;

            if (isClose)
            {
                btn.MouseEnter += (s, e) => { btn.Appearance.BackColor = Color.Crimson; btn.Appearance.ForeColor = Color.White; };
                btn.MouseLeave += (s, e) => { btn.Appearance.BackColor = Color.FromArgb(220, 220, 225); btn.Appearance.ForeColor = Color.Black; };
            }
            btn.Click += onClick; return btn;
        }

        private void InitializeComponent() { this.SuspendLayout(); this.Name = "EmployeeWorkspace"; this.ResumeLayout(false); }
    }
}