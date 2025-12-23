using System;
using Newtonsoft.Json;

namespace OfisAsistan.Models
{
    public class TaskComment
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("task_id")]
        public int TaskId { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("user_name")]
        public string UserName { get; set; }

        [JsonProperty("comment_text")]
        public string CommentText { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}