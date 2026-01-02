using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using OfisAsistan.Models;

// Alias Tanımları (Çakışmayı Önler)
using AppTask = OfisAsistan.Models.Task;
using TaskStatusEnum = OfisAsistan.Models.TaskStatus;

namespace OfisAsistan.Services
{
    public class NotificationService : IDisposable
    {
        private readonly DatabaseService _databaseService;
        private System.Windows.Forms.Timer _checkTimer;
        private List<int> _notifiedTaskIds;
        private List<int> _notifiedMeetingIds;

        // Bildirim event'i - Manager panelinde dinlenecek
        public event EventHandler<NotificationEventArgs> NotificationReceived;

        public NotificationService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _notifiedTaskIds = new List<int>();
            _notifiedMeetingIds = new List<int>();
            InitializeTimer();
        }

        // Bildirim event argümanları
        public class NotificationEventArgs : EventArgs
        {
            public string Title { get; set; }
            public string Message { get; set; }
            public bool IsUrgent { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private void InitializeTimer()
        {
            _checkTimer = new System.Windows.Forms.Timer();
            _checkTimer.Interval = Constants.NOTIFICATION_CHECK_INTERVAL_MS;
            _checkTimer.Tick += (s, e) => 
            {
                // Async metodları fire-and-forget olarak çağırıyoruz
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await CheckDeadlinesAsync();
                    await CheckMeetingsAsync();
                });
            };
            _checkTimer.Start();
        }

        private async System.Threading.Tasks.Task CheckDeadlinesAsync()
        {
            try
            {
                if (_databaseService == null)
                {
                    return;
                }

                var tasks = await _databaseService.GetTasksAsync();
                if (tasks == null || !tasks.Any())
                {
                    return;
                }

                var now = DateTime.Now;
                var minDate = now.AddDays(-1);
                var maxDate = now.AddDays(1);

                foreach (var task in tasks.Where(t =>
                    t != null &&
                    t.Status != TaskStatusEnum.Completed &&
                    t.DueDate.HasValue &&
                    t.DueDate.Value >= minDate &&
                    t.DueDate.Value <= maxDate &&
                    !_notifiedTaskIds.Contains(t.Id)))
                {
                    var timeRemaining = task.DueDate.Value - now;

                    // Deadline yaklaşıyor uyarısı
                    if (timeRemaining.TotalHours <= Constants.DEADLINE_WARNING_HOURS && timeRemaining.TotalHours > 0)
                    {
                        string taskTitle = string.IsNullOrEmpty(task.Title) ? "İsimsiz Görev" : task.Title;
                        ShowNotification(
                            "⏳ Deadline Yaklaşıyor",
                            $"{taskTitle} görevinin teslim tarihi yaklaşıyor! ({timeRemaining.Hours} saat kaldı)",
                            task.Priority == TaskPriority.Critical || task.Priority == TaskPriority.High
                        );
                        _notifiedTaskIds.Add(task.Id);
                    }
                    // Gecikmiş görevler
                    else if (timeRemaining.TotalHours < 0)
                    {
                        string taskTitle = string.IsNullOrEmpty(task.Title) ? "İsimsiz Görev" : task.Title;
                        ShowNotification(
                            "🚨 Gecikmiş Görev",
                            $"{taskTitle} görevi gecikmiş! ({Math.Abs(timeRemaining.Days)} gün)",
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
                if (_databaseService == null)
                {
                    return;
                }

                // GetMeetingsAsync artık DatabaseService içinde mevcut
                var meetings = await _databaseService.GetMeetingsAsync();
                if (meetings == null || !meetings.Any())
                {
                    return;
                }

                var now = DateTime.Now;

                foreach (var meeting in meetings.Where(m =>
                    m != null &&
                    m.StartTime > now &&
                    !_notifiedMeetingIds.Contains(m.Id)))
                {
                    var timeUntilMeeting = meeting.StartTime - now;

                    // Toplantı hatırlatması
                    if (timeUntilMeeting.TotalMinutes <= Constants.MEETING_REMINDER_MINUTES && timeUntilMeeting.TotalMinutes > 0)
                    {
                        string meetingTitle = string.IsNullOrEmpty(meeting.Title) ? "İsimsiz Toplantı" : meeting.Title;
                        ShowNotification(
                            "📅 Toplantı Hatırlatması",
                            $"{meetingTitle} toplantısı {timeUntilMeeting.Minutes} dakika sonra başlayacak.",
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
            try
            {
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
                {
                    return;
                }

                // Event'i tetikle - Manager panelinde dinlenecek
                NotificationReceived?.Invoke(this, new NotificationEventArgs
                {
                    Title = title,
                    Message = message,
                    IsUrgent = isUrgent,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowNotification Genel Hatası: {ex.Message}");
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