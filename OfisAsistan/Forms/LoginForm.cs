using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OfisAsistan.Models;
using OfisAsistan.Services;

namespace OfisAsistan.Forms
{
    public partial class LoginForm : Form
    {
        private DatabaseService _databaseService;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private Button btnLogin;
        private Label lblTitle;

        public User LoggedInUser { get; private set; }

        public LoginForm(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Ofis Asistan - Giriş";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(20)
            };

            lblTitle = new Label
            {
                Text = "🎯 Ofis Asistan",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            mainPanel.Controls.Add(lblTitle, 0, 0);
            mainPanel.SetColumnSpan(lblTitle, 2);

            mainPanel.Controls.Add(new Label { Text = "Kullanıcı Adı:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            txtUsername = new TextBox { Dock = DockStyle.Fill, Height = 30 };
            mainPanel.Controls.Add(txtUsername, 1, 1);

            mainPanel.Controls.Add(new Label { Text = "Şifre:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            txtPassword = new TextBox { Dock = DockStyle.Fill, Height = 30, UseSystemPasswordChar = true };
            mainPanel.Controls.Add(txtPassword, 1, 2);

            btnLogin = new Button
            {
                Text = "Giriş Yap",
                Dock = DockStyle.Fill,
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Height = 40
            };
            mainPanel.Controls.Add(btnLogin, 0, 3);
            mainPanel.SetColumnSpan(btnLogin, 2);

            // Demo için hızlı giriş butonları
            var demoPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnDemoManager = new Button { Text = "Demo: Yönetici", Size = new Size(120, 30) };
            var btnDemoEmployee = new Button { Text = "Demo: Çalışan", Size = new Size(120, 30) };
            demoPanel.Controls.Add(btnDemoManager);
            demoPanel.Controls.Add(btnDemoEmployee);
            mainPanel.Controls.Add(demoPanel, 0, 4);
            mainPanel.SetColumnSpan(demoPanel, 2);

            this.Controls.Add(mainPanel);

            btnLogin.Click += BtnLogin_Click;
            btnDemoManager.Click += (s, e) => { txtUsername.Text = "manager"; txtPassword.Text = "123"; BtnLogin_Click(s, e); };
            btnDemoEmployee.Click += (s, e) => { txtUsername.Text = "employee"; txtPassword.Text = "123"; BtnLogin_Click(s, e); };
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(s, e); };
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                MessageBox.Show("Kullanıcı adı gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Basit demo login (gerçek uygulamada veritabanından kontrol edilmeli)
            // Şimdilik demo için direkt geçiş yapıyoruz
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

