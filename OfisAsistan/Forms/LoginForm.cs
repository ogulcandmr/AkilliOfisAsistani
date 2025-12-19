using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OfisAsistan.Models;
using OfisAsistan.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using DevExpress.Utils;

namespace OfisAsistan.Forms
{
    public partial class LoginForm : XtraForm
    {
        private DatabaseService _databaseService;
        
        private TextEdit txtUsername;
        private TextEdit txtPassword;
        private SimpleButton btnLogin;
        private SimpleButton btnDemoManager;
        private SimpleButton btnDemoEmployee;
        private LabelControl lblTitle;
        private LayoutControl layoutControl;

        public User LoggedInUser { get; private set; }
        
        public LoginForm(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            InitializeComponent();
            SetupModernUI();
        }

        private void SetupModernUI()
        {
            this.Text = "Ofis Asistan - Giriş";
            this.Size = new Size(450, 350);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Appearance.BackColor = Color.FromArgb(240, 240, 240);

            layoutControl = new LayoutControl { Dock = DockStyle.Fill };
            this.Controls.Add(layoutControl);

            lblTitle = new LabelControl
            {
                Text = "🎯 OFİS ASİSTAN",
                Appearance = {
                    Font = new Font("Segoe UI", 20, FontStyle.Bold),
                    ForeColor = Color.FromArgb(40, 40, 40)
                },
                AllowHtmlString = true,
                AutoSizeMode = LabelAutoSizeMode.None,
                Height = 60,
                LineVisible = true
            };
            // HAlignment ayarını ayrı satırda yapın:
            lblTitle.Appearance.TextOptions.HAlignment = HorzAlignment.Center;

            txtUsername = new TextEdit { Name = "txtUsername" };
            txtUsername.Properties.NullValuePrompt = "Kullanıcı Adı";
            txtUsername.Properties.ContextImageOptions.Image = DevExpress.Images.ImageResourceCache.Default.GetImage("images/business%20objects/bo_user_16x16.png");

            txtPassword = new TextEdit { Name = "txtPassword" };
            txtPassword.Properties.UseSystemPasswordChar = true;
            txtPassword.Properties.NullValuePrompt = "Şifre";
            txtPassword.Properties.ContextImageOptions.Image = DevExpress.Images.ImageResourceCache.Default.GetImage("images/edit/encryption_16x16.png");

            btnLogin = new SimpleButton
            {
                Text = "Giriş Yap",
                Appearance = { 
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    BackColor = Color.FromArgb(0, 120, 215),
                    ForeColor = Color.White
                },
                Height = 45,
                ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat
            };

            btnDemoManager = new SimpleButton { Text = "Demo: Yönetici", AutoWidthInLayoutControl = true };
            btnDemoEmployee = new SimpleButton { Text = "Demo: Çalışan", AutoWidthInLayoutControl = true };

            // Layout Items
            var group = layoutControl.Root;
            group.Padding = new DevExpress.XtraLayout.Utils.Padding(30);
            group.Spacing = new DevExpress.XtraLayout.Utils.Padding(0);

            var titleItem = group.AddItem();
            titleItem.Control = lblTitle;
            titleItem.TextVisible = false;
            titleItem.SizeConstraintsType = SizeConstraintsType.Custom;
            titleItem.MinSize = new Size(0, 70);
            titleItem.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 20);

            var usernameItem = group.AddItem("Kullanıcı Adı", txtUsername);
            usernameItem.TextLocation = DevExpress.Utils.Locations.Top;
            usernameItem.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 10);

            var passwordItem = group.AddItem("Şifre", txtPassword);
            passwordItem.TextLocation = DevExpress.Utils.Locations.Top;
            passwordItem.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 20);

            var loginItem = group.AddItem();
            loginItem.Control = btnLogin;
            loginItem.TextVisible = false;
            loginItem.Padding = new DevExpress.XtraLayout.Utils.Padding(0, 0, 0, 20);

            var demoGroup = group.AddGroup("Hızlı Giriş (Demo)");
            demoGroup.LayoutMode = DevExpress.XtraLayout.Utils.LayoutMode.Table;
            demoGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });
            demoGroup.OptionsTableLayoutGroup.ColumnDefinitions.Add(new ColumnDefinition { SizeType = SizeType.Percent, Width = 50 });
            demoGroup.OptionsTableLayoutGroup.RowDefinitions.Add(new RowDefinition { SizeType = SizeType.AutoSize });

            var managerItem = demoGroup.AddItem();
            managerItem.Control = btnDemoManager;
            managerItem.TextVisible = false;
            managerItem.OptionsTableLayoutItem.ColumnIndex = 0;

            var employeeItem = demoGroup.AddItem();
            employeeItem.Control = btnDemoEmployee;
            employeeItem.TextVisible = false;
            employeeItem.OptionsTableLayoutItem.ColumnIndex = 1;

            // Events
            btnLogin.Click += BtnLogin_Click;
            btnDemoManager.Click += (s, e) => { txtUsername.Text = "manager"; txtPassword.Text = "123"; BtnLogin_Click(s, e); };
            btnDemoEmployee.Click += (s, e) => { txtUsername.Text = "employee"; txtPassword.Text = "123"; BtnLogin_Click(s, e); };
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(s, e); };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "LoginForm";
            this.ResumeLayout(false);
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                XtraMessageBox.Show("Kullanıcı adı gerekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Basit demo login
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
