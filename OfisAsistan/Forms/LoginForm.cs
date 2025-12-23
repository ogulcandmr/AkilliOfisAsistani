using System;
using System.Drawing;
using System.Drawing.Drawing2D; // Çizim kütüphanesi
using System.Windows.Forms;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.Utils;

namespace OfisAsistan.Forms
{
    public partial class LoginForm : XtraForm
    {
        // --- SERVİSLER & DATA ---
        private readonly DatabaseService _databaseService;

        // Giriş yapan kullanıcıyı Program.cs'e taşımak için property
        public User LoggedInUser { get; private set; }

        // --- TASARIM RENKLERİ ---
        private readonly Color clrPrimary = Color.FromArgb(99, 102, 241); // İndigo
        private readonly Color clrSecondary = Color.FromArgb(76, 29, 149); // Koyu Mor (Gradient için)
        private readonly Color clrBackground = Color.White;
        private readonly Color clrText = Color.FromArgb(17, 24, 39);
        private readonly Color clrGray = Color.FromArgb(107, 114, 128);

        // --- UI KONTROLLERİ ---
        private TextEdit txtUsername;
        private TextEdit txtPassword;
        private SimpleButton btnLogin;

        // Sürükleme değişkenleri
        private bool isDragging = false;
        private Point dragStart;

        // Constructor
        public LoginForm(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            InitializeComponent();
            SetupModernLoginUI();
        }

        private void SetupModernLoginUI()
        {
            // 1. Form Temel Ayarları
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1100, 700);
            this.BackColor = clrBackground;

            // Sürükleme Olayları
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < 80) { isDragging = true; dragStart = e.Location; } };
            this.MouseMove += (s, e) => { if (isDragging) { Point p = PointToScreen(e.Location); Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y); } };
            this.MouseUp += (s, e) => { isDragging = false; };

            // 2. Ana İskelet (TableLayout)
            var mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 2;
            mainLayout.RowCount = 1;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F)); // Sol %45
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F)); // Sağ %55
            this.Controls.Add(mainLayout);

            // --- SOL PANEL (LOGO ALANI) ---
            var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = clrPrimary };
            leftPanel.Paint += LeftPanel_Paint; // Gelişmiş Logo Çizimi
            mainLayout.Controls.Add(leftPanel, 0, 0);

            // Sol tarafa Slogan
            var sloganLabel = new LabelControl
            {
                Text = "İşlerinizi Akıllıca Yönetin,\nZamanınızı Geri Kazanın.",
                Appearance = { Font = new Font("Segoe UI", 16, FontStyle.Regular), ForeColor = Color.FromArgb(224, 231, 255), TextOptions = { WordWrap = WordWrap.Wrap } },
                AutoSizeMode = LabelAutoSizeMode.None,
                Size = new Size(350, 100),
                BackColor = Color.Transparent
            };
            sloganLabel.Location = new Point(50, 420); // Tahmini ortalama konum
            leftPanel.Controls.Add(sloganLabel);

            // --- SAĞ PANEL (FORM ALANI) ---
            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(80) };
            mainLayout.Controls.Add(rightPanel, 1, 0);

            // Kapatma Butonu
            var closeBtn = new SimpleButton { Text = "✕", Size = new Size(40, 40), Location = new Point(rightPanel.Width - 50, 10), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            StyleGhostButton(closeBtn);
            closeBtn.Click += (s, e) => Application.Exit();
            rightPanel.Controls.Add(closeBtn);

            // İçerik Akışı (FlowLayout)
            var formLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 50, 0, 0)
            };
            rightPanel.Controls.Add(formLayout);

            // Başlıklar
            var lblTitle = new LabelControl
            {
                Text = "Hoş Geldiniz 👋",
                Appearance = { Font = new Font("Segoe UI", 28, FontStyle.Bold), ForeColor = clrText },
                Margin = new Padding(0, 0, 0, 10)
            };
            formLayout.Controls.Add(lblTitle);

            var lblSub = new LabelControl
            {
                Text = "Hesabınıza erişmek için bilgilerinizi girin.",
                Appearance = { Font = new Font("Segoe UI", 11), ForeColor = clrGray },
                Margin = new Padding(0, 0, 0, 40)
            };
            formLayout.Controls.Add(lblSub);

            // Kullanıcı Adı
            formLayout.Controls.Add(CreateLabel("Kullanıcı Adı"));
            txtUsername = CreateInput("Kullanıcı Adı", false);
            formLayout.Controls.Add(txtUsername);

            // Şifre
            formLayout.Controls.Add(CreateLabel("Şifre"));
            txtPassword = CreateInput("••••••••", true);
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(s, e); };
            formLayout.Controls.Add(txtPassword);

            // Beni Hatırla
            var checkRemember = new CheckEdit { Text = "Beni Hatırla", Margin = new Padding(0, 0, 0, 30) };
            checkRemember.Properties.Appearance.Font = new Font("Segoe UI", 10);
            checkRemember.Properties.Appearance.ForeColor = clrGray;
            formLayout.Controls.Add(checkRemember);

            // Giriş Butonu
            btnLogin = new SimpleButton
            {
                Text = "Giriş Yap",
                Size = new Size(400, 55),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 20)
            };
            btnLogin.Appearance.BackColor = clrPrimary;
            btnLogin.Appearance.ForeColor = Color.White;
            btnLogin.Appearance.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnLogin.LookAndFeel.UseDefaultLookAndFeel = false;
            btnLogin.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btnLogin.MouseEnter += (s, e) => btnLogin.Appearance.BackColor = Color.FromArgb(124, 58, 237); // Hover rengi
            btnLogin.MouseLeave += (s, e) => btnLogin.Appearance.BackColor = clrPrimary;
            btnLogin.Click += BtnLogin_Click;
            formLayout.Controls.Add(btnLogin);

            // --- DEMO BUTONLARI ---
            var demoPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };

            var btnDemoMgr = new HyperlinkLabelControl { Text = "Demo: Yönetici", Cursor = Cursors.Hand };
            btnDemoMgr.Appearance.Font = new Font("Segoe UI", 9, FontStyle.Underline);
            btnDemoMgr.Appearance.ForeColor = clrGray;
            btnDemoMgr.Click += (s, e) => { txtUsername.Text = "manager"; txtPassword.Text = "123"; BtnLogin_Click(s, e); };

            var btnDemoEmp = new HyperlinkLabelControl { Text = "Demo: Çalışan", Cursor = Cursors.Hand, Padding = new Padding(20, 0, 0, 0) };
            btnDemoEmp.Appearance.Font = new Font("Segoe UI", 9, FontStyle.Underline);
            btnDemoEmp.Appearance.ForeColor = clrGray;
            btnDemoEmp.Click += (s, e) => { txtUsername.Text = "employee"; txtPassword.Text = "123"; BtnLogin_Click(s, e); };

            demoPanel.Controls.Add(btnDemoMgr);
            demoPanel.Controls.Add(btnDemoEmp);
            formLayout.Controls.Add(demoPanel);

            // Footer
            var footer = new LabelControl
            {
                Text = "© 2025 Ofis Asistan. Tüm hakları saklıdır.",
                Appearance = { Font = new Font("Segoe UI", 9), ForeColor = Color.LightGray },
                Margin = new Padding(0, 30, 0, 0)
            };
            formLayout.Controls.Add(footer);
        }

        // --- GELİŞTİRİLMİŞ 3D LOGO ÇİZİMİ (Burayı Güncelledim) ---
        private void LeftPanel_Paint(object sender, PaintEventArgs e)
        {
            var p = sender as Panel;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // 1. ZEMİN (Zengin Gradient)
            using (var brush = new LinearGradientBrush(p.ClientRectangle,
                clrPrimary, clrSecondary, LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(brush, p.ClientRectangle);
            }

            // 2. ARKA PLAN DESENLERİ
            using (var brush = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
            {
                g.FillEllipse(brush, -150, -150, 500, 500);
                g.FillEllipse(brush, p.Width - 250, p.Height - 250, 600, 600);
            }

            // --- HEXAGON LOGO ---
            int centerX = p.Width / 2;
            int centerY = p.Height / 2 - 60;
            int size = 70; // Logo boyutu

            // A. GÖLGE (Hexagon'un altına yumuşak siyah gölge)
            Point[] shadowPoints = GetHexagonPoints(centerX, centerY + 10, size);
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(shadowPoints);
                using (var brush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                {
                    g.FillPath(brush, path);
                }
            }

            // B. ANA HEXAGON (Beyaz ve Hafif Gri Geçişli - 3D Efekti)
            Point[] hexPoints = GetHexagonPoints(centerX, centerY, size);
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(hexPoints);
                using (var brush = new LinearGradientBrush(
                    new Point(centerX, centerY - size),
                    new Point(centerX, centerY + size),
                    Color.White,
                    Color.FromArgb(240, 240, 245)))
                {
                    g.FillPath(brush, path);
                }
            }

            // C. STİLİZE "TİK" (CHECK) İŞARETİ
            using (var tickPath = new GraphicsPath())
            {
                // Kalınlaştırılmış path mantığı
                tickPath.AddLine(centerX - 20, centerY + 5, centerX - 5, centerY + 20); // Aşağı inen kol
                tickPath.AddLine(centerX - 5, centerY + 20, centerX + 25, centerY - 20); // Yukarı çıkan kol

                using (var pen = new Pen(clrPrimary, 10)) // 10px kalınlık
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, tickPath);
                }
            }

            // D. METİN (Gölge + Yazı)
            TextRenderer.DrawText(g, "Ofis Asistan", new Font("Segoe UI", 26, FontStyle.Bold),
                new Point(centerX + 2, centerY + 102), Color.FromArgb(50, 0, 0, 0), TextFormatFlags.HorizontalCenter);

            TextRenderer.DrawText(g, "Ofis Asistan", new Font("Segoe UI", 26, FontStyle.Bold),
                new Point(centerX, centerY + 100), Color.White, TextFormatFlags.HorizontalCenter);
        }

        // Yardımcı Metot: Altıgen Noktalarını Hesaplar
        private Point[] GetHexagonPoints(int cx, int cy, int size)
        {
            return new Point[]
            {
                new Point(cx, cy - size),                 // Üst
                new Point(cx + size, cy - size / 2),      // Sağ Üst
                new Point(cx + size, cy + size / 2),      // Sağ Alt
                new Point(cx, cy + size),                 // Alt
                new Point(cx - size, cy + size / 2),      // Sol Alt
                new Point(cx - size, cy - size / 2)       // Sol Üst
            };
        }

        // --- YARDIMCI UI METOTLARI ---
        private LabelControl CreateLabel(string text)
        {
            return new LabelControl
            {
                Text = text,
                Appearance = { Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = clrText },
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        private TextEdit CreateInput(string placeholder, bool isPassword)
        {
            var edit = new TextEdit { Size = new Size(400, 45) };
            edit.Properties.Appearance.Font = new Font("Segoe UI", 11);
            edit.Properties.Appearance.BackColor = Color.FromArgb(249, 250, 251);
            edit.Properties.Appearance.BorderColor = Color.FromArgb(229, 231, 235);
            edit.Properties.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            edit.Properties.Padding = new Padding(10);
            edit.Properties.NullValuePrompt = placeholder;
            edit.Properties.UseSystemPasswordChar = isPassword;
            edit.Margin = new Padding(0, 0, 0, 20);
            return edit;
        }

        private void StyleGhostButton(SimpleButton btn)
        {
            btn.Appearance.BackColor = Color.Transparent;
            btn.Appearance.ForeColor = clrGray;
            btn.Appearance.Font = new Font("Segoe UI", 12);
            btn.LookAndFeel.UseDefaultLookAndFeel = false;
            btn.LookAndFeel.Style = DevExpress.LookAndFeel.LookAndFeelStyle.Flat;
            btn.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "LoginForm";
            this.ResumeLayout(false);
        }

        // --- GİRİŞ MANTIĞI ---
        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                XtraMessageBox.Show("Kullanıcı adı gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Demo Login Mantığı (Senin orijinal kodun)
            LoggedInUser = new User
            {
                Id = txtUsername.Text == "manager" ? 1 : 2,
                Username = txtUsername.Text,
                Role = txtUsername.Text == "manager" ? UserRole.Manager : UserRole.Employee,
                EmployeeId = txtUsername.Text == "manager" ? 1 : 2
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}