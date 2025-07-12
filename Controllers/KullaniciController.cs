using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DashBoardWebApp.Models;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace DashBoardWebApp.Controllers
{
    public class KullaniciController : Controller
    {
        private string connectionString = "Data Source=./Database/app.db";

        [Authorize(Roles = "Admin")]
        public IActionResult AdminPanel()
        {
            return View();
        }
       
        [Authorize(Roles = "Admin")] // Admin kontrolü
        public IActionResult ShowUsers()
        {
            List<Kullanici> users = new List<Kullanici>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Kullanıcılar";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new Kullanici
                        {
                            ID = reader.GetInt32(0),
                            KullaniciAdi = reader.GetString(1),
                            Sifre = reader.GetString(2),
                            Eposta = reader.GetString(3),
                            Konum = reader.GetString(4),
                            IlgiAlanlari = reader.GetString(5),
                            Ad = reader.GetString(6),
                            Soyad = reader.GetString(7),
                            DogumTarihi = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                            Cinsiyet = reader.GetString(9),
                            TelefonNumarasi = reader.GetString(10),
                            ProfilFotografi = reader.GetString(11),
                            Role = reader.GetString(12) // Role bilgisi ekleniyor
                        });
                    }
                }
            }

            return View(users);
        }
      
        [Authorize(Roles = "Admin")]
        public IActionResult EditUser(int id)
        {
            Kullanici kullanici = null;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Kullanıcılar WHERE ID = @id";
                command.Parameters.AddWithValue("@id", id);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        kullanici = new Kullanici
                        {
                            ID = reader.GetInt32(0),
                            KullaniciAdi = reader.GetString(1),
                            Eposta = reader.GetString(3),
                            Ad = reader.GetString(6),
                            Soyad = reader.GetString(7),
                            Role = reader.GetString(12)
                        };
                    }
                }
            }

            if (kullanici == null)
            {
                // Eğer kullanıcı bulunamazsa, hata mesajı gösterelim
                TempData["Message"] = "Kullanıcı bulunamadı!";
                return RedirectToAction("ShowUsers");
            }

            return View(kullanici); // Modeli View'a gönderiyoruz
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult EditUser(Kullanici updatedUser)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Kullanıcılar 
                    SET UserName = @kullaniciAdi, Email = @eposta, FirstName = @ad, LastName = @soyad, Role = @role 
                    WHERE ID = @id";
                command.Parameters.AddWithValue("@kullaniciAdi", updatedUser.KullaniciAdi);
                command.Parameters.AddWithValue("@eposta", updatedUser.Eposta);
                command.Parameters.AddWithValue("@ad", updatedUser.Ad);
                command.Parameters.AddWithValue("@soyad", updatedUser.Soyad);
                command.Parameters.AddWithValue("@role", updatedUser.Role);
                command.Parameters.AddWithValue("@id", updatedUser.ID);

                command.ExecuteNonQuery();
            }

            TempData["Message"] = "Kullanıcı başarıyla güncellendi.";
            return RedirectToAction("ShowUsers");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult DeleteUser(int id)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Kullanıcıya ait mesajları sil
                        var deleteMessagesCommand = connection.CreateCommand();
                        deleteMessagesCommand.CommandText = "DELETE FROM Mesajlar WHERE SenderID = @userId";
                        deleteMessagesCommand.Parameters.AddWithValue("@userId", id);
                        deleteMessagesCommand.ExecuteNonQuery();

                        // 2. Katılımcılar tablosundan kullanıcıyı temizle
                        var deleteParticipationCommand = connection.CreateCommand();
                        deleteParticipationCommand.CommandText = "DELETE FROM Katılımcılar WHERE UserID = @userId";
                        deleteParticipationCommand.Parameters.AddWithValue("@userId", id);
                        deleteParticipationCommand.Transaction = transaction;
                        deleteParticipationCommand.ExecuteNonQuery();

                        // 3. Kullanıcının oluşturduğu etkinliklerden katılım kayıtlarını temizle
                        var deleteEventParticipationCommand = connection.CreateCommand();
                        deleteEventParticipationCommand.CommandText = @"
                            DELETE FROM Katılımcılar 
                            WHERE EventID IN (SELECT ID FROM Etkinlikler WHERE CreatorID = @userId)";
                        deleteEventParticipationCommand.Parameters.AddWithValue("@userId", id);
                        deleteEventParticipationCommand.Transaction = transaction;
                        deleteEventParticipationCommand.ExecuteNonQuery();

                        // 4. Kullanıcının oluşturduğu etkinlikleri temizle
                        var deleteEventsCommand = connection.CreateCommand();
                        deleteEventsCommand.CommandText = "DELETE FROM Etkinlikler WHERE CreatorID = @userId";
                        deleteEventsCommand.Parameters.AddWithValue("@userId", id);
                        deleteEventsCommand.Transaction = transaction;
                        deleteEventsCommand.ExecuteNonQuery();

                        // 5. Puanlar tablosundan ilgili kullanıcıya ait kayıtları sil
                        var deletePointsCommand = connection.CreateCommand();
                        deletePointsCommand.CommandText = "DELETE FROM Puanlar WHERE UserID = @userId";
                        deletePointsCommand.Parameters.AddWithValue("@userId", id);
                        deletePointsCommand.ExecuteNonQuery();

                        // 6. Kullanıcıyı temizle
                        var deleteUserCommand = connection.CreateCommand();
                        deleteUserCommand.CommandText = "DELETE FROM Kullanıcılar WHERE ID = @id";
                        deleteUserCommand.Parameters.AddWithValue("@id", id);
                        deleteUserCommand.Transaction = transaction;
                        deleteUserCommand.ExecuteNonQuery();

                        // İşlemleri tamamla
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        // Bir hata olursa işlemi geri al
                        transaction.Rollback();
                        TempData["Message"] = "Bir hata oluştu: " + ex.Message;
                        return RedirectToAction("ShowUsers");
                    }
                }
            }

            TempData["Message"] = "Kullanıcı ve ilişkili tüm veriler başarıyla silindi.";
            return RedirectToAction("ShowUsers");
        }

        // Kullanıcı Giriş İşlemi (View gösterir)
        public IActionResult Login()
        {
            return View();
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        // Kullanıcı Giriş İşlemi (Form Gönderildiğinde)
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            Kullanici kullanici = KullaniciGirisi(email, password);
            if (kullanici != null)
            {
                // Kullanıcı giriş bilgilerini Claims olarak ekle
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, kullanici.ID.ToString()),
                    new Claim(ClaimTypes.Name, kullanici.Ad + " " + kullanici.Soyad),
                    new Claim(ClaimTypes.Email, kullanici.Eposta),
                    new Claim(ClaimTypes.Role, kullanici.Role) // Kullanıcının rolünü ekle
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.SignInAsync(new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Profile", new { id = kullanici.ID });
            }

            ViewBag.ErrorMessage = "E-posta veya şifre hatalı!";
            return View();
        }

        private Kullanici KullaniciGirisi(string email, string password)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Kullanıcılar WHERE Email = @Email AND Password = @Password";
                command.Parameters.AddWithValue("@Email", email);
                command.Parameters.AddWithValue("@Password", HashPassword(password));

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Kullanici
                        {
                            ID = reader.GetInt32(0),
                            KullaniciAdi = reader.GetString(1),
                            Sifre = reader.GetString(2),
                            Eposta = reader.GetString(3),
                            Konum = reader.GetString(4),
                            IlgiAlanlari = reader.GetString(5),
                            Ad = reader.GetString(6),
                            Soyad = reader.GetString(7),
                            DogumTarihi = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                            Cinsiyet = reader.GetString(9),
                            TelefonNumarasi = reader.GetString(10),
                            ProfilFotografi = reader.GetString(11),
                            Role = reader.IsDBNull(12) ? "Kullanıcı" : reader.GetString(12)
                        };
                    }
                }
            }
            return null;
        }

        // Yeni Kullanıcı Kayıt Sayfası
        public IActionResult Register()
        {
            return View();
        }

        // Yeni Kullanıcı Kayıt İşlemi
        [HttpPost]
        public IActionResult Register(Kullanici yeniKullanici, IFormFile? profilFotografi)
        {
            if (profilFotografi != null && profilFotografi.Length > 0)
            {
                // Fotoğraf dosyasının adını oluşturuyoruz ve yolu belirliyoruz
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(profilFotografi.FileName);
                var fotoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", uniqueFileName);
                
                // Fotoğrafı belirtilen dizine kaydediyoruz
                using (var stream = new FileStream(fotoPath, FileMode.Create))
                {
                    profilFotografi.CopyTo(stream);
                }

                // Kullanıcının fotoğraf yolunu modele ekliyoruz
                yeniKullanici.ProfilFotografi = $"/uploads/{uniqueFileName}";
            }
            
            // Yeni kullanıcıyı veritabanına ekliyoruz
            YeniKullaniciEkle(yeniKullanici);

            return RedirectToAction("Login");
        }

        // Profil Görüntüleme
        public IActionResult Profile(int id)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (id != currentUserId)
            {
                TempData["PreviousUrl"] = $"/Kullanici/Profile/{currentUserId}";
                return RedirectToAction("AccessDenied", "Home");
            }

            // Kullanıcı bilgilerini alıyoruz
            Kullanici kullanici = ProfilBilgileriniGetir(id);

            // Kullanıcının katıldığı etkinlikler
            var userEventIds = GetUserEventIds(id);

            foreach (var eventId in userEventIds)
            {
                // Öncelikle ilk katılım bonusunu kontrol et ve ekle
                bool isFirstParticipation = AddBonusForFirstParticipation(id, eventId);

                // Eğer bu etkinlik ilk katılım değilse normal katılım puanı ekle
                if (!isFirstParticipation)
                {
                    AddPointsForEventParticipation(id, eventId);
                }
            }

            // Kullanıcının oluşturduğu etkinlikler
            var createdEventIds = GetCreatedEventIds(id);

            foreach (var eventId in createdEventIds)
            {
                AddPointsForEventCreation(id, eventId); // Etkinlik oluşturma puanı ekle
            }

            // Kullanıcının kazandığı tüm puanları al
            List<EtkinlikPuan> earnedPoints = GetEarnedPoints(id);

            // Toplam puanı hesapla
            int totalPoints = earnedPoints.Sum(p => p.Points);
            kullanici.TotalPoints = totalPoints;
            kullanici.EarnedPoints = earnedPoints;

            if (kullanici != null)
            {
                return View(kullanici);
            }

            return NotFound();
        }

        private List<int> GetCreatedEventIds(int userId)
        {
            List<int> eventIds = new List<int>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = "SELECT ID FROM Etkinlikler WHERE CreatorID = @userId";
                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        eventIds.Add(reader.GetInt32(0)); // Etkinlik ID'sini listeye ekliyoruz
                    }
                }
            }

            return eventIds;
        }


        public List<int> GetUserEventIds(int userId)
        {
            var eventIds = new List<int>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT EventID
                    FROM Katılımcılar
                    WHERE UserID = @userId";

                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        eventIds.Add(reader.GetInt32(0)); // EventID'yi alıyoruz
                    }
                }
            }

            return eventIds;
        }

        public List<EtkinlikPuan> GetEarnedPoints(int userId)
        {
            var points = new List<EtkinlikPuan>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT e.EventName, p.Points, p.EarnedDate
                    FROM Puanlar p
                    INNER JOIN Etkinlikler e ON p.EventID = e.ID
                    WHERE p.UserID = @userId";

                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        points.Add(new EtkinlikPuan
                        {
                            EventName = reader.GetString(0),  // Etkinlik Adı
                            Points = reader.GetInt32(1),      // Kazanılan Puan
                            EarnedDate = reader.GetDateTime(2) // Puanın kazanıldığı tarih
                        });
                    }
                }
            }

            return points;
        }


        // Yeni Kullanıcıyı Veritabanına Ekleme
        private void YeniKullaniciEkle(Kullanici yeniKullanici)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Kullanıcılar (Username, Password, Email, Location, Interests, FirstName, LastName, BirthDate, Gender, PhoneNumber, ProfilePhoto)
                    VALUES (@username, @password, @email, @location, @interests, @firstName, @lastName, @birthDate, @gender, @phoneNumber, @profilePhoto)";
                
                // Parametreleri null değer kontrolü ile ekliyoruz
                command.Parameters.AddWithValue("@username", yeniKullanici.KullaniciAdi ?? string.Empty);
                command.Parameters.AddWithValue("@password", HashPassword(yeniKullanici.Sifre) ?? string.Empty);
                command.Parameters.AddWithValue("@email", yeniKullanici.Eposta ?? string.Empty);
                command.Parameters.AddWithValue("@location", yeniKullanici.Konum ?? string.Empty);
                command.Parameters.AddWithValue("@interests", yeniKullanici.IlgiAlanlari ?? string.Empty);
                command.Parameters.AddWithValue("@firstName", yeniKullanici.Ad ?? string.Empty);
                command.Parameters.AddWithValue("@lastName", yeniKullanici.Soyad ?? string.Empty);
                command.Parameters.AddWithValue("@birthDate", yeniKullanici.DogumTarihi.HasValue ? (object)yeniKullanici.DogumTarihi.Value : DBNull.Value);
                command.Parameters.AddWithValue("@gender", yeniKullanici.Cinsiyet ?? string.Empty);
                command.Parameters.AddWithValue("@phoneNumber", yeniKullanici.TelefonNumarasi ?? string.Empty);
                command.Parameters.AddWithValue("@profilePhoto", yeniKullanici.ProfilFotografi ?? string.Empty);

                command.ExecuteNonQuery();
            }
        }

        // Veritabanından profil bilgilerini alma
        private Kullanici ProfilBilgileriniGetir(int id)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Kullanıcılar WHERE ID = @id";
                command.Parameters.AddWithValue("@id", id);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Kullanici 
                        {
                            ID = reader.GetInt32(0),
                            KullaniciAdi = reader.GetString(1),
                            Eposta = reader.GetString(3),
                            Konum = reader.GetString(4),
                            IlgiAlanlari = reader.GetString(5),
                            Ad = reader.GetString(6),
                            Soyad = reader.GetString(7),
                            DogumTarihi = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8), // Null kontrolü
                            Cinsiyet = reader.GetString(9),
                            TelefonNumarasi = reader.GetString(10),
                            ProfilFotografi = reader.GetString(11)
                        };
                    }
                }
            }
            return null;
        }

        // Çıkış işlemi
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // Şifremi Unuttum Sayfası
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            // Kullanıcının e-posta adresi var mı kontrol et
            if (!IsEmailExists(email))
            {
                ViewBag.ErrorMessage = "Bu e-posta adresi kayıtlı değil!";
                return View();
            }

            // Kullanıcıya doğrulama kodu gönder
            string verificationCode = GenerateVerificationCode();
            SendVerificationCode(email, verificationCode);

            // Doğrulama kodunu ve e-postayı geçici olarak sakla
            HttpContext.Session.SetString("VerificationCode", verificationCode);
            HttpContext.Session.SetString("ResetEmail", email);

            // Kullanıcıyı doğrulama kodu sayfasına yönlendir
            return RedirectToAction("VerifyCode");
        }

        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString(); // 6 haneli rastgele kod
        }

        private void SendVerificationCode(string email, string verificationCode)
        {
            // SMTP falan eklenebilir ama şuan proje yetişmeyebilir bu yüzden doğrulama kodunu terminalden kontrol ediyoruz.
            Console.WriteLine($"E-posta gönderildi: {email}, Doğrulama Kodu: {verificationCode}");
        }

        // Yeni Şifre Oluşturma Sayfası
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string email, string newPassword)
        {
            if (!IsEmailExists(email))
            {
                ViewBag.ErrorMessage = "Bu e-posta adresi kayıtlı değil!";
                return View();
            }

            // Yeni şifreyi güncelle
            UpdateUserPassword(email, HashPassword(newPassword));

            ViewBag.SuccessMessage = "Şifreniz başarıyla güncellendi!";
            return RedirectToAction("Login");
        }

        private bool IsEmailExists(string email)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(1) FROM Kullanıcılar WHERE Email = @Email";
                command.Parameters.AddWithValue("@Email", email);

                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private void UpdateUserPassword(string email, string hashedPassword)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE Kullanıcılar SET Password = @Password WHERE Email = @Email";
                command.Parameters.AddWithValue("@Password", hashedPassword);
                command.Parameters.AddWithValue("@Email", email);
                command.ExecuteNonQuery();
            }
        }

        public IActionResult VerifyCode()
        {
            return View();
        }

        [HttpPost]
        public IActionResult VerifyCode(string enteredCode)
        {
            // Session'dan doğrulama kodunu al
            string? verificationCode = HttpContext.Session.GetString("VerificationCode");

            if (verificationCode == enteredCode)
            {
                // Kod doğru, kullanıcıyı ResetPassword sayfasına yönlendir
                return RedirectToAction("ResetPassword");
            }

            ViewBag.ErrorMessage = "Doğrulama kodu yanlış, lütfen tekrar deneyin.";
            return View();
        }

        // Profil Görüntüleme ve Güncelleme Sayfası
        [Authorize]
        public IActionResult EditProfile(int id)
        {
            // Kullanıcı bilgilerini veritabanından al
            Kullanici kullanici = ProfilBilgileriniGetir(id);
            if (kullanici != null)
            {
                return View(kullanici);
            }
            return NotFound();
        }

        [HttpPost]
        [Authorize]
        public IActionResult EditProfile(Kullanici updatedUser, IFormFile? profilFotografi)
        {
            if (ModelState.IsValid)
            {
                // Kullanıcı profilini güncelle
                UpdateUserProfile(updatedUser, profilFotografi);
                return RedirectToAction("Profile", new { id = updatedUser.ID });
            }

            return View(updatedUser);
        }

        // Kullanıcı Profili Güncelleme İşlemi
        private void UpdateUserProfile(Kullanici updatedUser, IFormFile? profilFotografi)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();

                // Profil fotoğrafı varsa yükle ve dosya yolunu belirle
                if (profilFotografi != null && profilFotografi.Length > 0)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(profilFotografi.FileName);
                    var fotoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", uniqueFileName);

                    // wwwroot/uploads dizinine dosyayı kaydet
                    using (var stream = new FileStream(fotoPath, FileMode.Create))
                    {
                        profilFotografi.CopyTo(stream);
                    }

                    // Kullanıcının fotoğraf yolunu modele ekle
                    updatedUser.ProfilFotografi = $"/uploads/{uniqueFileName}";
                }

                command.CommandText = @"
                    UPDATE Kullanıcılar
                    SET Email = @Email,
                        Location = @Konum,
                        Interests = @IlgiAlanlari,
                        FirstName = @Ad,
                        LastName = @Soyad,
                        BirthDate = @DogumTarihi,
                        Gender = @Cinsiyet,
                        PhoneNumber = @TelefonNumarasi,
                        ProfilePhoto = @ProfilFotografi
                    WHERE ID = @ID";

                command.Parameters.AddWithValue("@ID", updatedUser.ID);
                command.Parameters.AddWithValue("@Email", updatedUser.Eposta ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Konum", updatedUser.Konum ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IlgiAlanlari", updatedUser.IlgiAlanlari ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Ad", updatedUser.Ad ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Soyad", updatedUser.Soyad ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@DogumTarihi", updatedUser.DogumTarihi ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Cinsiyet", updatedUser.Cinsiyet ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@TelefonNumarasi", updatedUser.TelefonNumarasi ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ProfilFotografi", updatedUser.ProfilFotografi ?? (object)DBNull.Value);

                command.ExecuteNonQuery();
            }
        }

        [Authorize]
        public IActionResult JoinEventWithAlternatives(int eventId, string returnUrl)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); // Kullanıcının ID'si
            var conflictingEvents = new List<Etkinlik>();
            Etkinlik alternativeEvent = null;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Yeni etkinlik bilgilerini al
                var newEventCommand = connection.CreateCommand();
                newEventCommand.CommandText = "SELECT Date, Time, Duration FROM Etkinlikler WHERE ID = @eventId";
                newEventCommand.Parameters.AddWithValue("@eventId", eventId);
                DateTime newEventDate = DateTime.MinValue;
                TimeSpan newEventTime = TimeSpan.Zero;
                int newEventDuration = 0;

                using (var reader = newEventCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        newEventDate = DateTime.Parse(reader.GetString(0));
                        newEventTime = TimeSpan.Parse(reader.GetString(1));
                        newEventDuration = reader.GetInt32(2);
                    }
                }

                // Kullanıcının katıldığı etkinlikleri al ve çakışmaları kontrol et
                var participationCommand = connection.CreateCommand();
                participationCommand.CommandText = @"
                    SELECT e.ID, e.EventName, e.Date, e.Time, e.Duration, e.Location, e.Category 
                    FROM Etkinlikler e
                    INNER JOIN Katılımcılar k ON e.ID = k.EventID
                    WHERE k.UserID = @userId";
                participationCommand.Parameters.AddWithValue("@userId", userId);

                using (var reader = participationCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var existingEvent = new Etkinlik
                        {
                            ID = reader.GetInt32(0),
                            EventName = reader.GetString(1),
                            Date = DateTime.Parse(reader.GetString(2)),
                            Time = TimeSpan.Parse(reader.GetString(3)),
                            Duration = reader.GetInt32(4),
                            Location = reader.GetString(5),
                            Category = reader.GetString(6)
                        };

                        // Çakışma kontrolü
                        if (existingEvent.Date == newEventDate)
                        {
                            TimeSpan existingEventEnd = existingEvent.Time.Add(TimeSpan.FromMinutes((double)existingEvent.Duration));
                            TimeSpan newEventEnd = newEventTime.Add(TimeSpan.FromMinutes(newEventDuration));

                            if (newEventTime < existingEventEnd && newEventEnd > existingEvent.Time)
                            {
                                conflictingEvents.Add(existingEvent);
                            }
                        }
                    }
                }

                // Eğer çakışma varsa alternatif etkinlik bul
                if (conflictingEvents.Any())
                {
                    var alternativeCommand = connection.CreateCommand();
                    alternativeCommand.CommandText = @"
                        SELECT e.ID, e.EventName, e.Date, e.Time, e.Duration, e.Location, e.Category
                        FROM Etkinlikler e
                        WHERE e.CreatorID != @userId 
                        AND e.ID != @eventId 
                        AND e.ID NOT IN (SELECT EventID FROM Katılımcılar WHERE UserID = @userId)";
                    alternativeCommand.Parameters.AddWithValue("@eventId", eventId);
                    alternativeCommand.Parameters.AddWithValue("@userId", userId);

                    using (var reader = alternativeCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var possibleEvent = new Etkinlik
                            {
                                ID = reader.GetInt32(0),
                                EventName = reader.GetString(1),
                                Date = DateTime.Parse(reader.GetString(2)),
                                Time = TimeSpan.Parse(reader.GetString(3)),
                                Duration = reader.GetInt32(4),
                                Location = reader.GetString(5),
                                Category = reader.GetString(6)
                            };

                            bool isConflicting = false;
                            foreach (var conflict in conflictingEvents)
                            {
                                if (possibleEvent.Date == conflict.Date)
                                {
                                    TimeSpan possibleEventEnd = possibleEvent.Time.Add(TimeSpan.FromMinutes((double)possibleEvent.Duration));
                                    TimeSpan conflictEnd = conflict.Time.Add(TimeSpan.FromMinutes((double)conflict.Duration));

                                    if (possibleEvent.Time < conflictEnd && possibleEventEnd > conflict.Time)
                                    {
                                        isConflicting = true;
                                        break;
                                    }
                                }
                            }

                            if (!isConflicting)
                            {
                                alternativeEvent = possibleEvent;
                                break;
                            }
                        }
                    }
                }

                if (conflictingEvents.Any())
                {
                    TempData["Message"] = alternativeEvent != null
                        ? $"Bu etkinlik başka bir etkinlik ile çakışıyor! Alternatif öneri: <strong>{alternativeEvent.EventName}</strong>, Tarih: {alternativeEvent.Date:yyyy-MM-dd}, Saat: {alternativeEvent.Time:hh\\:mm}"
                        : "Bu etkinlik çakışıyor ve alternatif bir etkinlik bulunamadı.";
                    TempData["MessageClass"] = "alert-warning";
                    return RedirectToAction(
                        returnUrl,
                        returnUrl == "GeneralEvents" ? "Event" :
                        returnUrl == "GetRecommendations" ? "Event" :
                        "Kullanici"
                    );
                }

                // Çakışma yoksa katılımı kaydet
                var joinCommand = connection.CreateCommand();
                joinCommand.CommandText = "INSERT INTO Katılımcılar (UserID, EventID) VALUES (@userId, @eventId)";
                joinCommand.Parameters.AddWithValue("@userId", userId);
                joinCommand.Parameters.AddWithValue("@eventId", eventId);
                joinCommand.ExecuteNonQuery();
            }

            TempData["Message"] = "Etkinliğe başarıyla katıldınız!";
            TempData["MessageClass"] = "alert-success";
            return RedirectToAction(
                returnUrl,
                returnUrl == "GeneralEvents" ? "Event" :
                returnUrl == "GetRecommendations" ? "Event" :
                "Kullanici"
            );
        }

        private void AddPointsForEventParticipation(int userId, int eventId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Kullanıcının bu etkinlik için daha önce puan alıp almadığını kontrol et
                var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT COUNT(*) FROM Puanlar WHERE UserID = @userId AND EventID = @eventId";
                checkCommand.Parameters.AddWithValue("@userId", userId);
                checkCommand.Parameters.AddWithValue("@eventId", eventId);

                var count = Convert.ToInt32(checkCommand.ExecuteScalar());

                if (count == 0) // Daha önce bu etkinlik için puan alınmamış
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO Puanlar (UserID, EventID, Points, EarnedDate)
                        VALUES (@userId, @eventId, 10, CURRENT_TIMESTAMP)"; // Katılım puanı 10
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@eventId", eventId);
                    command.ExecuteNonQuery();
                }
            }
        }

        private bool AddBonusForFirstParticipation(int userId, int eventId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Kullanıcının daha önce etkinliğe katılıp katılmadığını kontrol et
                var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT COUNT(*) FROM Katılımcılar WHERE UserID = @userId";
                checkCommand.Parameters.AddWithValue("@userId", userId);

                var count = Convert.ToInt32(checkCommand.ExecuteScalar());

                if (count == 0) // İlk katılım
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO Puanlar (UserID, EventID, Points, EarnedDate)
                        VALUES (@userId, @eventId, 20, CURRENT_TIMESTAMP)"; // İlk katılım bonusu 20 puan
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@eventId", eventId);
                    command.ExecuteNonQuery();

                    return true; // Bonus eklendi
                }
            }
            return false; // Bonus eklenmedi
        }

        private void AddPointsForEventCreation(int userId, int eventId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Kullanıcının bu etkinlik için daha önce puan alıp almadığını kontrol et
                var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT COUNT(*) FROM Puanlar WHERE UserID = @userId AND EventID = @eventId";
                checkCommand.Parameters.AddWithValue("@userId", userId);
                checkCommand.Parameters.AddWithValue("@eventId", eventId);

                var count = Convert.ToInt32(checkCommand.ExecuteScalar());

                if (count == 0) // Daha önce bu etkinlik için puan alınmamış
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT INTO Puanlar (UserID, EventID, Points, EarnedDate)
                        VALUES (@userId, @eventId, 15, CURRENT_TIMESTAMP)"; // Etkinlik oluşturma 15 puan
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@eventId", eventId);
                    command.ExecuteNonQuery();
                }
            }
        }

    }
}
