using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskStatusModel = OfisAsistan.Models.TaskStatus;
using OfisAsistan.Models;

namespace OfisAsistan.Services
{
    public class NotificationService
    {
        private readonly DatabaseService _databaseService;
        private System.Windows.Forms.Timer _checkTimer;
        private List<int> _notifiedTaskIds;
        private List<int> _notifiedMeetingIds;

        public NotificationService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _notifiedTaskIds = new List<int>();
            _notifiedMeetingIds = new List<int>();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _checkTimer = new System.Windows.Forms.Timer();
            _checkTimer.Interval = 60000; // 1 dakika
            _checkTimer.Tick += CheckTimer_Tick;
            _checkTimer.Start();
        }

        private async void CheckTimer_Tick(object sender, EventArgs e)
        {
            await CheckDeadlinesAsync();
            await CheckMeetingsAsync();
        }

        private async System.Threading.Tasks.Task CheckDeadlinesAsync()
        {
            try
            {
                var tasks = await _databaseService.GetTasksAsync();
                var now = DateTime.Now;
                var minDate = now.AddDays(-1);
                var maxDate = now.AddDays(1);

                foreach (var task in tasks.Where(t => 
                    t.Status != TaskStatusModel.Completed && 
                    t.DueDate.HasValue &&
                    t.DueDate.Value >= minDate &&
                    t.DueDate.Value <= maxDate &&
                    !_notifiedTaskIds.Contains(t.Id)))
                {
                    var timeRemaining = task.DueDate.Value - now;

                    // 2 saat kala uyarı
                    if (timeRemaining.TotalHours <= 2 && timeRemaining.TotalHours > 0)
                    {
                        ShowNotification(
                            "⏳ Deadline Yaklaşıyor",
                            $"{task.Title} görevinin teslim tarihi yaklaşıyor! ({timeRemaining.Hours} saat kaldı)",
                            task.Priority == TaskPriority.Critical || task.Priority == TaskPriority.High
                        );
                        _notifiedTaskIds.Add(task.Id);
                    }
                    // Gecikmiş görevler
                    else if (timeRemaining.TotalHours < 0)
                    {
                        ShowNotification(
                            "🚨 Gecikmiş Görev",
                            $"{task.Title} görevi gecikmiş! ({Math.Abs(timeRemaining.Days)} gün)",
                            true
                        );
                        _notifiedTaskIds.Add(task.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckDeadlinesAsync Error: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task CheckMeetingsAsync()
        {
            try
            {
                var meetings = await _databaseService.GetMeetingsAsync();
                var now = DateTime.Now;

                foreach (var meeting in meetings.Where(m => 
                    m.StartTime > now && 
                    !_notifiedMeetingIds.Contains(m.Id)))
                {
                    var timeUntilMeeting = meeting.StartTime - now;

                    // 15 dakika kala uyarı
                    if (timeUntilMeeting.TotalMinutes <= 15 && timeUntilMeeting.TotalMinutes > 0)
                    {
                        ShowNotification(
                            "📅 Toplantı Hatırlatması",
                            $"{meeting.Title} toplantısı {timeUntilMeeting.Minutes} dakika sonra başlayacak.",
                            false
                        );
                        _notifiedMeetingIds.Add(meeting.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckMeetingsAsync Error: {ex.Message}");
            }
        }

        private void ShowNotification(string title, string message, bool isUrgent)
        {
            // Windows Forms için basit bildirim
            // Daha gelişmiş için DevExpress Toast Notification kullanılabilir
            var form = Application.OpenForms.Cast<Form>().FirstOrDefault();
            if (form != null)
            {
                form.Invoke(new Action(() =>
                {
                    var result = MessageBox.Show(
                        message,
                        title,
                        MessageBoxButtons.OK,
                        isUrgent ? MessageBoxIcon.Warning : MessageBoxIcon.Information
                    );
                }));
            }
        }

        public void ClearNotifications()
        {
            _notifiedTaskIds.Clear();
            _notifiedMeetingIds.Clear();
        }

        public void Dispose()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
        }
    }
}

