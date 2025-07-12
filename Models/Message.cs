using System;

namespace DashBoardWebApp.Models
{
    public class Message
    {
        public int MessageID { get; set; } // Benzersiz Mesaj ID'si
        public int SenderID { get; set; } // Mesajı gönderen kullanıcı ID'si
        public int EventID { get; set; } // Mesajın ait olduğu etkinlik ID'si
        public string MessageText { get; set; } // Mesaj içeriği
        public DateTime SentTime { get; set; } // Mesajın gönderildiği zaman
    }
}
