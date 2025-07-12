using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DashBoardWebApp.Models
{
    public class ChatViewModel
    {
        public int EventID { get; set; }
        public string EventName { get; set; }
        public List<MessageWithSender> Messages { get; set; }
    }

    public class MessageWithSender
    {
        public string SenderName { get; set; }
        public string MessageText { get; set; }
        public DateTime SentTime { get; set; }
    }
}