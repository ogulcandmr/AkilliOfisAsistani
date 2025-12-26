using DevExpress.XtraScheduler;
using DevExpress.XtraScheduler.Drawing;
using Microsoft.VisualBasic;
using OfisAsistan.Models;
using OfisAsistan.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OfisAsistan.Forms
{
    public partial class CalendarForm : DevExpress.XtraEditors.XtraForm
    {
        private SchedulerControl schedulerControl;
        // DÜZELTME: SchedulerStorage yerine SchedulerDataStorage (Yeni versiyonlar için)
        private SchedulerDataStorage schedulerStorage;
        private readonly DatabaseService _db;

        public CalendarForm(DatabaseService db)
        {
            _db = db;
            InitializeComponent();
            SetupScheduler();
            this.Load += async (s, e) => await LoadDataAsync();
        }

        private void SetupScheduler()
        {
            this.Text = "Görev Takvimi ve Planlama";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            // DÜZELTME: SchedulerDataStorage kullanımı
            schedulerStorage = new SchedulerDataStorage(this.components);
            schedulerControl = new SchedulerControl();

            // Storage bağlantısı
            schedulerControl.DataStorage = schedulerStorage;
            schedulerControl.Dock = DockStyle.Fill;

            // Görünüm Ayarları
            // Not: Bu satırların çalışması için önceki mesajda bahsettiğim 
            // 'DevExpress.XtraScheduler.v25.1.Core.Desktop' referansının ekli olması şarttır.
            schedulerControl.ActiveViewType = SchedulerViewType.Month;
            schedulerControl.GroupType = SchedulerGroupType.None;

            // DÜZELTİLEN SATIR BURASI:
            // Başına 'DevExpress.XtraScheduler.' ekleyerek belirsizliği giderdik.
            schedulerControl.OptionsView.FirstDayOfWeek = DevExpress.XtraScheduler.FirstDayOfWeek.Monday;

            this.Controls.Add(schedulerControl);
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var tasks = await _db.GetTasksAsync();

                // Appointments temizleme yöntemi değiştiyse diye try-catch içinde
                try { schedulerStorage.Appointments.Clear(); } catch { }

                foreach (var t in tasks)
                {
                    DateTime end = t.DueDate ?? DateTime.Now;
                    DateTime start = end.AddHours(-t.EstimatedHours > 0 ? -t.EstimatedHours : -2);

                    // Appointment oluşturma
                    Appointment apt = schedulerStorage.CreateAppointment(AppointmentType.Normal);
                    apt.Start = start;
                    apt.End = end;
                    apt.Subject = $"{t.Title} ({t.Status})";
                    apt.Description = t.Description;
                    apt.Location = t.Priority.ToString();

                    // Renklendirme (LabelId: 1=Kırmızı, 2=Mavi, 3=Yeşil genelde)
                    if (t.Status == OfisAsistan.Models.TaskStatus.Completed)
                        apt.LabelKey = 3; // Yeşil (Tamamlandı)
                    else if (end < DateTime.Now)
                        apt.LabelKey = 1; // Kırmızı (Gecikmiş)
                    else if (t.Status == OfisAsistan.Models.TaskStatus.InProgress)
                        apt.LabelKey = 2; // Mavi/Sarı (Devam Ediyor)
                    else
                        apt.LabelKey = 0; // Varsayılan

                    schedulerStorage.Appointments.Add(apt);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Takvim verileri yüklenirken hata: " + ex.Message);
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        }
        private System.ComponentModel.IContainer components = null;
    }
}