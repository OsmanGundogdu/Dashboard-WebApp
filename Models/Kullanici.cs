using System;

namespace DashBoardWebApp.Models
{
    public class Kullanici
    {
        public int ID { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
        public string Eposta { get; set; } = string.Empty;
        public string Konum { get; set; } = string.Empty;
        public string IlgiAlanlari { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Soyad { get; set; } = string.Empty;
        public DateTime? DogumTarihi { get; set; }
        public string Cinsiyet { get; set; } = string.Empty;
        public string TelefonNumarasi { get; set; } = string.Empty;
        public string ProfilFotografi { get; set; } = string.Empty;
        public int TotalPoints { get; set; }
        public List<EtkinlikPuan> EarnedPoints { get; set; }
        public string Role { get; set; } = "Kullanıcı";
    }

    public class EtkinlikPuan
    {
        public string EventName { get; set; }
        public int Points { get; set; }
        public DateTime EarnedDate { get; set; }
    }


}
