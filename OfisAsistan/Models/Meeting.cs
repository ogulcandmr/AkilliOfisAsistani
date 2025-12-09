using System;
using System.Collections.Generic;

namespace OfisAsistan.Models
{
    public class Meeting
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int OrganizerId { get; set; }
        public string Location { get; set; }
        public string AttendeeIds { get; set; } // JSON formatında katılımcı ID listesi
        public bool IsReminderSent { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

