using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DashBoardWebApp.Models
{
    public class Etkinlik
    {
        public int ID { get; set; }
        public string EventName { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; } // Zamanı string olarak saklıyoruz
        public int? Duration { get; set; } // Dakika cinsinden etkinlik süresi
        public string Location { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Category { get; set; }
        public bool IsApproved { get; set; }
        public int CreatorID { get; set; }
    }

    public class EtkinlikWithCreator : Etkinlik
    {
        public string CreatorName { get; set; } // Etkinliği oluşturan kullanıcının adı ve soyadı
    }
}
