namespace OfisAsistan
{
    partial class Form1
    {
        /// <summary>
        ///Gerekli tasarımcı değişkeni.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private DevExpress.XtraBars.Ribbon.RibbonControl ribbonControl;
        private DevExpress.XtraBars.Ribbon.RibbonPage ribbonPage1;
        private DevExpress.XtraBars.Ribbon.RibbonPageGroup ribbonPageGroup1;
        private DevExpress.XtraBars.BarButtonItem mnuManager;
        private DevExpress.XtraBars.BarButtonItem mnuEmployee;
        private DevExpress.XtraBars.BarButtonItem mnuVoice;
        private DevExpress.XtraBars.BarButtonItem mnuExit;

        /// <summary>
        ///Kullanılan tüm kaynakları temizleyin.
        /// </summary>
        ///<param name="disposing">yönetilen kaynaklar dispose edilmeliyse doğru; aksi halde yanlış.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer üretilen kod

        /// <summary>
        /// Tasarımcı desteği için gerekli metot - bu metodun 
        ///içeriğini kod düzenleyici ile değiştirmeyin.
        /// </summary>
        private void InitializeComponent()
        {
            this.ribbonControl = new DevExpress.XtraBars.Ribbon.RibbonControl();
            this.mnuManager = new DevExpress.XtraBars.BarButtonItem();
            this.mnuEmployee = new DevExpress.XtraBars.BarButtonItem();
            this.mnuVoice = new DevExpress.XtraBars.BarButtonItem();
            this.mnuExit = new DevExpress.XtraBars.BarButtonItem();
            this.ribbonPage1 = new DevExpress.XtraBars.Ribbon.RibbonPage();
            this.ribbonPageGroup1 = new DevExpress.XtraBars.Ribbon.RibbonPageGroup();
            ((System.ComponentModel.ISupportInitialize)(this.ribbonControl)).BeginInit();
            this.SuspendLayout();
            // 
            // ribbonControl
            // 
            this.ribbonControl.ExpandCollapseItem.Id = 0;
            this.ribbonControl.Items.AddRange(new DevExpress.XtraBars.BarItem[] {
            this.ribbonControl.ExpandCollapseItem,
            this.mnuManager,
            this.mnuEmployee,
            this.mnuVoice,
            this.mnuExit});
            this.ribbonControl.Location = new System.Drawing.Point(0, 0);
            this.ribbonControl.MaxItemId = 5;
            this.ribbonControl.Name = "ribbonControl";
            this.ribbonControl.Pages.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPage[] {
            this.ribbonPage1});
            this.ribbonControl.Size = new System.Drawing.Size(800, 158);
            this.ribbonControl.RibbonStyle = DevExpress.XtraBars.Ribbon.RibbonControlStyle.Office2019;
            // 
            // mnuManager
            // 
            this.mnuManager.Caption = "Yönetici Paneli";
            this.mnuManager.Id = 1;
            this.mnuManager.ImageOptions.SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("outlook%20inspired/pivottable.svg");
            this.mnuManager.Name = "mnuManager";
            // 
            // mnuEmployee
            // 
            this.mnuEmployee.Caption = "Çalışan Paneli";
            this.mnuEmployee.Id = 2;
            this.mnuEmployee.ImageOptions.SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("outlook%20inspired/employees.svg");
            this.mnuEmployee.Name = "mnuEmployee";
            // 
            // mnuVoice
            // 
            this.mnuVoice.Caption = "Sesli Yönetici";
            this.mnuVoice.Id = 3;
            this.mnuVoice.ImageOptions.SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("icon%20builder/actions_comment.svg");
            this.mnuVoice.Name = "mnuVoice";
            // 
            // mnuExit
            // 
            this.mnuExit.Caption = "Çıkış";
            this.mnuExit.Id = 4;
            this.mnuExit.ImageOptions.SvgImage = DevExpress.Images.ImageResourceCache.Default.GetSvgImage("actions_close.svg");
            this.mnuExit.Name = "mnuExit";
            // 
            // ribbonPage1
            // 
            this.ribbonPage1.Groups.AddRange(new DevExpress.XtraBars.Ribbon.RibbonPageGroup[] {
            this.ribbonPageGroup1});
            this.ribbonPage1.Name = "ribbonPage1";
            this.ribbonPage1.Text = "ANA MENÜ";
            // 
            // ribbonPageGroup1
            // 
            this.ribbonPageGroup1.ItemLinks.Add(this.mnuManager);
            this.ribbonPageGroup1.ItemLinks.Add(this.mnuEmployee);
            this.ribbonPageGroup1.ItemLinks.Add(this.mnuVoice);
            this.ribbonPageGroup1.ItemLinks.Add(this.mnuExit);
            this.ribbonPageGroup1.Name = "ribbonPageGroup1";
            this.ribbonPageGroup1.Text = "Hızlı Erişim";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.ribbonControl);
            this.IsMdiContainer = true;
            this.Name = "Form1";
            this.Text = "Ofis Asistan - Ana Pencere";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)(this.ribbonControl)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}

