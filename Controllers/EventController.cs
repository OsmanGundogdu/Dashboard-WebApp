using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DashBoardWebApp.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

public class EventController : Controller
{
    private string connectionString = "Data Source=./Database/app.db";

    [Authorize(Roles = "Admin")]
    public IActionResult ManageEvents()
    {
        var allEvents = new List<EtkinlikWithCreator>();
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Etkinlikler";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var etkinlik = new EtkinlikWithCreator
                    {
                        ID = reader.GetInt32(0),
                        EventName = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Date = reader.GetDateTime(3),
                        Time = reader.GetTimeSpan(4),
                        Duration = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                        Location = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Category = reader.IsDBNull(7) ? null : reader.GetString(7),
                        IsApproved = reader.GetInt32(8) == 1,
                        CreatorID = reader.GetInt32(9) // CreatorID'yi alıyoruz
                    };

                    // CreatorID'yi kullanarak kullanıcının ismini ve soyadını alıyoruz
                    var userCommand = connection.CreateCommand();
                    userCommand.CommandText = "SELECT FirstName, LastName FROM Kullanıcılar WHERE ID = @CreatorID";
                    userCommand.Parameters.AddWithValue("@CreatorID", etkinlik.CreatorID);

                    using (var userReader = userCommand.ExecuteReader())
                    {
                        if (userReader.Read())
                        {
                            etkinlik.CreatorName = $"{userReader.GetString(0)} {userReader.GetString(1)}"; // Kullanıcı adı ve soyadını ekliyoruz
                        }
                    }

                    allEvents.Add(etkinlik);
                }
            }
        }

        return View(allEvents);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult ApproveEvent(int id)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Etkinlikler SET IsApproved = 1 WHERE ID = @ID";
            command.Parameters.AddWithValue("@ID", id);

            command.ExecuteNonQuery();
        }

        return RedirectToAction("ManageEvents");
    }

    [Authorize]
    public IActionResult CreateEvent()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult GeneralEvents()
    {
        var generalEvents = new List<EtkinlikWithCreator>();

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Etkinlikler WHERE IsApproved = 1";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var etkinlik = new EtkinlikWithCreator
                    {
                        ID = reader.GetInt32(0),
                        EventName = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Date = reader.GetDateTime(3),
                        Time = reader.GetTimeSpan(4),
                        Duration = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                        Location = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Category = reader.IsDBNull(7) ? null : reader.GetString(7),
                        IsApproved = reader.GetInt32(8) == 1,
                        CreatorID = reader.GetInt32(9) // CreatorID alınıyor
                    };

                    // Kullanıcı adını ve soyadını almak için ikinci sorgu
                    var userCommand = connection.CreateCommand();
                    userCommand.CommandText = "SELECT FirstName, LastName FROM Kullanıcılar WHERE ID = @CreatorID";
                    userCommand.Parameters.AddWithValue("@CreatorID", etkinlik.CreatorID);

                    using (var userReader = userCommand.ExecuteReader())
                    {
                        if (userReader.Read())
                        {
                            etkinlik.CreatorName = $"{userReader.GetString(0)} {userReader.GetString(1)}";
                        }
                    }

                    generalEvents.Add(etkinlik);
                }
            }
        }

        return View(generalEvents);
    }

    [Authorize]
    [HttpPost]
    public IActionResult CreateEvent(Etkinlik yeniEtkinlik)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); // Giriş yapan kullanıcının ID'si

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // Kullanıcının diğer etkinliklerini çek
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = @"
                SELECT Date, Time, Duration 
                FROM Etkinlikler 
                WHERE CreatorID = @userId";
            checkCommand.Parameters.AddWithValue("@userId", userId);

            using (var reader = checkCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    // Mevcut etkinlik bilgilerini al
                    DateTime existingEventDate = DateTime.Parse(reader.GetString(0));
                    TimeSpan existingEventTime = TimeSpan.Parse(reader.GetString(1));
                    int existingEventDuration = reader.GetInt32(2);

                    // Yeni etkinlik bilgilerini hesapla
                    DateTime newEventDate = yeniEtkinlik.Date;
                    TimeSpan newEventTime = yeniEtkinlik.Time;
                    TimeSpan newEventEndTime = newEventTime.Add(TimeSpan.FromMinutes((double)yeniEtkinlik.Duration));
                    TimeSpan existingEventEndTime = existingEventTime.Add(TimeSpan.FromMinutes(existingEventDuration));

                    // Çakışma kontrolü
                    if (newEventDate == existingEventDate && 
                        (newEventTime < existingEventEndTime && newEventEndTime > existingEventTime))
                    {
                        TempData["Message"] = "Bu etkinlik başka bir etkinliğiniz ile çakışıyor.";
                        return RedirectToAction("CreateEvent");
                    }
                }
            }

            // Çakışma yoksa etkinliği ekle
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Etkinlikler (EventName, Description, Date, Time, Duration, Location, Category, CreatorID)
                VALUES (@eventName, @description, @date, @time, @duration, @location, @category, @creatorId)";
            insertCommand.Parameters.AddWithValue("@eventName", yeniEtkinlik.EventName);
            insertCommand.Parameters.AddWithValue("@description", yeniEtkinlik.Description);
            insertCommand.Parameters.AddWithValue("@date", yeniEtkinlik.Date.ToString("yyyy-MM-dd"));
            insertCommand.Parameters.AddWithValue("@time", yeniEtkinlik.Time.ToString(@"hh\:mm"));
            insertCommand.Parameters.AddWithValue("@duration", yeniEtkinlik.Duration);
            insertCommand.Parameters.AddWithValue("@location", yeniEtkinlik.Location);
            insertCommand.Parameters.AddWithValue("@category", yeniEtkinlik.Category);
            insertCommand.Parameters.AddWithValue("@creatorId", userId);
            insertCommand.ExecuteNonQuery();
        }

        TempData["Message"] = "Etkinlik başarıyla oluşturuldu!";
        return RedirectToAction("MyEvents");
    }

    [Authorize]
    [HttpPost]
    public IActionResult CreateEventWithAlternatives(Etkinlik yeniEtkinlik)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var conflictingEvents = new List<Etkinlik>();

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // Kullanıcının diğer etkinliklerini al
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = @"
                SELECT ID, EventName, Date, Time, Duration, Location, Category
                FROM Etkinlikler 
                WHERE CreatorID = @userId";
            checkCommand.Parameters.AddWithValue("@userId", userId);

            using (var reader = checkCommand.ExecuteReader())
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
                    if (existingEvent.Date == yeniEtkinlik.Date)
                    {
                        TimeSpan existingEventEnd = existingEvent.Time.Add(TimeSpan.FromMinutes((double)existingEvent.Duration));
                        TimeSpan newEventEnd = yeniEtkinlik.Time.Add(TimeSpan.FromMinutes((double)yeniEtkinlik.Duration));

                        if ((yeniEtkinlik.Time < existingEventEnd) && (newEventEnd > existingEvent.Time))
                        {
                            conflictingEvents.Add(existingEvent);
                        }
                    }
                }
            }
        }

        // Çakışma var mı kontrol et
        if (conflictingEvents.Any())
        {
            ViewBag.ConflictingEvents = conflictingEvents;
            return View("EventConflict", yeniEtkinlik);
        }

        // Çakışma yoksa etkinliği ekle
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Etkinlikler (EventName, Description, Date, Time, Duration, Location, Category, CreatorID)
                VALUES (@eventName, @description, @date, @time, @duration, @location, @category, @creatorId)";
            insertCommand.Parameters.AddWithValue("@eventName", yeniEtkinlik.EventName);
            insertCommand.Parameters.AddWithValue("@description", yeniEtkinlik.Description);
            insertCommand.Parameters.AddWithValue("@date", yeniEtkinlik.Date.ToString("yyyy-MM-dd"));
            insertCommand.Parameters.AddWithValue("@time", yeniEtkinlik.Time.ToString(@"hh\:mm"));
            insertCommand.Parameters.AddWithValue("@duration", yeniEtkinlik.Duration);
            insertCommand.Parameters.AddWithValue("@location", yeniEtkinlik.Location);
            insertCommand.Parameters.AddWithValue("@category", yeniEtkinlik.Category);
            insertCommand.Parameters.AddWithValue("@creatorId", userId);
            insertCommand.ExecuteNonQuery();
        }

        TempData["Message"] = "Etkinlik başarıyla oluşturuldu!";
        return RedirectToAction("MyEvents");
    }

    [Authorize]
    public IActionResult MyEvents()
    {
        var myEvents = new List<Etkinlik>();
        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); // Giriş yapan kullanıcının ID'si

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Etkinlikler WHERE CreatorID = @CreatorID";
            command.Parameters.AddWithValue("@CreatorID", currentUserId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    myEvents.Add(new Etkinlik
                    {
                        ID = reader.GetInt32(0),
                        EventName = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Date = reader.GetDateTime(3),
                        Time = reader.GetTimeSpan(4),
                        Duration = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                        Location = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Category = reader.IsDBNull(7) ? null : reader.GetString(7),
                        IsApproved = reader.GetInt32(8) == 1,
                        CreatorID = reader.GetInt32(9)
                    });
                }
            }
        }

        return View(myEvents);
    }

    [Authorize]
    public IActionResult EditEvent(int id)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Etkinlikler WHERE ID = @ID";
            command.Parameters.AddWithValue("@ID", id);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    var etkinlik = new Etkinlik
                    {
                        ID = reader.GetInt32(0),
                        EventName = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Date = reader.GetDateTime(3),
                        Time = reader.GetTimeSpan(4),
                        Duration = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                        Location = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Category = reader.IsDBNull(7) ? null : reader.GetString(7)
                    };
                    return View(etkinlik);
                }
            }
        }
        return NotFound();
    }

    [HttpPost]
    [Authorize]
    public IActionResult EditEvent(Etkinlik updatedEvent)
    {
        // Kullanıcı admin değilse IsApproved sıfırlanır
        bool isAdmin = User.IsInRole("Admin");

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE Etkinlikler
                SET EventName = @eventName,
                    Description = @description,
                    Date = @date,
                    Time = @time,
                    Duration = @duration,
                    Location = @location,
                    Category = @category,
                    IsApproved = CASE WHEN @isAdmin = 1 THEN IsApproved ELSE 0 END
                WHERE ID = @eventId";
            updateCommand.Parameters.AddWithValue("@eventName", updatedEvent.EventName);
            updateCommand.Parameters.AddWithValue("@description", updatedEvent.Description);
            updateCommand.Parameters.AddWithValue("@date", updatedEvent.Date.ToString("yyyy-MM-dd"));
            updateCommand.Parameters.AddWithValue("@time", updatedEvent.Time.ToString(@"hh\:mm"));
            updateCommand.Parameters.AddWithValue("@duration", updatedEvent.Duration);
            updateCommand.Parameters.AddWithValue("@location", updatedEvent.Location);
            updateCommand.Parameters.AddWithValue("@category", updatedEvent.Category);
            updateCommand.Parameters.AddWithValue("@eventId", updatedEvent.ID);
            updateCommand.Parameters.AddWithValue("@isAdmin", isAdmin ? 1 : 0); // Admin kontrolü

            updateCommand.ExecuteNonQuery();
        }

        TempData["Message"] = isAdmin 
            ? "Etkinlik başarıyla güncellendi ve onay durumu korundu." 
            : "Etkinlik başarıyla güncellendi. Onay için adminin tekrar değerlendirmesi gerekiyor.";
        TempData["MessageClass"] = isAdmin ? "alert-success" : "alert-warning";

        // Kullanıcının rolüne göre yönlendirme
        if (isAdmin)
        {
            return RedirectToAction("ManageEvents", "Event");
        }
        else
        {
            return RedirectToAction("MyEvents", "Event");
        }
    }

    public IActionResult DeleteEvent(int id)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // Etkinliği kontrol et
            var checkEventCommand = connection.CreateCommand();
            checkEventCommand.CommandText = "SELECT COUNT(*) FROM Etkinlikler WHERE ID = @eventId";
            checkEventCommand.Parameters.AddWithValue("@eventId", id);

            var eventExists = Convert.ToInt32(checkEventCommand.ExecuteScalar()) > 0;

            if (!eventExists)
            {
                // Eğer etkinlik bulunamazsa
                TempData["Message"] = "Etkinlik silinemedi. Geçersiz ID.";
                TempData["MessageClass"] = "alert-danger";
                return RedirectToAction("ManageEvents");
            }

            var deleteMessagesCommand = connection.CreateCommand();
            deleteMessagesCommand.CommandText = "DELETE FROM Mesajlar WHERE EventID = @eventId";
            deleteMessagesCommand.Parameters.AddWithValue("@eventId", id);
            deleteMessagesCommand.ExecuteNonQuery();

            // Katılımcılar tablosundan ilgili kayıtları sil
            var deleteParticipantsCommand = connection.CreateCommand();
            deleteParticipantsCommand.CommandText = "DELETE FROM Katılımcılar WHERE EventID = @eventId";
            deleteParticipantsCommand.Parameters.AddWithValue("@eventId", id);
            deleteParticipantsCommand.ExecuteNonQuery();

            // Puanlar tablosundan etkinliğe ait kayıtları sil
            var deletePointsCommand = connection.CreateCommand();
            deletePointsCommand.CommandText = "DELETE FROM Puanlar WHERE EventID = @eventId";
            deletePointsCommand.Parameters.AddWithValue("@eventId", id);
            deletePointsCommand.ExecuteNonQuery();

            // Etkinlikler tablosundan etkinliği sil
            var deleteEventCommand = connection.CreateCommand();
            deleteEventCommand.CommandText = "DELETE FROM Etkinlikler WHERE ID = @eventId";
            deleteEventCommand.Parameters.AddWithValue("@eventId", id);
            deleteEventCommand.ExecuteNonQuery();
        }

        TempData["Message"] = "Etkinlik başarıyla silindi!";
        TempData["MessageClass"] = "alert-success";
        // Kullanıcının rolüne göre yönlendirme
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("ManageEvents"); // Admin yönlendirmesi
        }
        else
        {
            return RedirectToAction("MyEvents", "Kullanici"); // Kullanıcı yönlendirmesi
        }
    }

    [Authorize]
    public IActionResult Etkinlikler()
    {
        // Kullanıcı giriş yaptıysa, kendi etkinliklerine yönlendirme
        return RedirectToAction("MyEvents", "Event");
    }

    [AllowAnonymous]
    public IActionResult EtkinliklerGenel()
    {
        // Giriş yapmamış kullanıcılar için genel etkinlik sayfası
        return RedirectToAction("Index", "Event");
    }

    [Authorize]
    public IActionResult GetRecommendations()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); // Kullanıcının ID'si
        var recommendedEvents = new List<Etkinlik>();

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // Kullanıcının ilgi alanlarını al
            var interestCommand = connection.CreateCommand();
            interestCommand.CommandText = "SELECT Interests FROM Kullanıcılar WHERE ID = @userId";
            interestCommand.Parameters.AddWithValue("@userId", userId);

            string userInterests = "";
            using (var reader = interestCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    userInterests = reader.GetString(0); // İlgi alanlarını al
                }
            }

            // İlgi alanlarını virgülle ayırarak bir diziye dönüştür
            var interestsList = userInterests.Split(',').Select(i => i.Trim()).ToList();

            // İlgi alanlarına uygun etkinlikleri al
            var command = connection.CreateCommand();
            command.CommandText = "SELECT e.ID, e.EventName, e.Date, e.Time, e.Duration, e.Location, e.Category FROM Etkinlikler e WHERE (";

            // İlgi alanlarına göre LIKE sorgusu oluştur
            for (int i = 0; i < interestsList.Count; i++)
            {
                command.CommandText += i == 0 ? "" : " OR "; // İlk parametre için boş, sonrakiler için "OR"
                command.CommandText += $"e.Category LIKE @interest{i}";
                command.Parameters.AddWithValue($"@interest{i}", "%" + interestsList[i] + "%");
            }

            // Katılmadığı etkinlikleri filtrele
            command.CommandText += @") AND e.ID NOT IN (SELECT EventID FROM Katılımcılar WHERE UserID = @userId)";
            command.Parameters.AddWithValue("@userId", userId);
            command.CommandText += " ORDER BY e.Date, e.Time;";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    recommendedEvents.Add(new Etkinlik
                    {
                        ID = reader.GetInt32(0),
                        EventName = reader.GetString(1),
                        Date = DateTime.Parse(reader.GetString(2)),
                        Time = TimeSpan.Parse(reader.GetString(3)),
                        Duration = reader.GetInt32(4),
                        Location = reader.GetString(5),
                        Category = reader.GetString(6)
                    });
                }
            }
        }

        // Öneri sayfasına önerilen etkinlikleri gönder
        return View("Recommendations", recommendedEvents);
    }

    public IActionResult ShowEventDetails(int id)
    {
        // Kullanıcı bilgilerini ve etkinlik detaylarını almak için iki ayrı sorgu yapılacak
        var eventDetails = new Etkinlik();
        Kullanici eventCreator = null;

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // Etkinlik bilgilerini al
            var eventCommand = connection.CreateCommand();
            eventCommand.CommandText = @"
                SELECT EventName, Description, Date, Time, Duration, Location, Category 
                FROM Etkinlikler
                WHERE ID = @id";
            eventCommand.Parameters.AddWithValue("@id", id);

            using (var reader = eventCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    eventDetails.EventName = reader.GetString(0);
                    eventDetails.Description = reader.GetString(1);
                    eventDetails.Date = DateTime.Parse(reader.GetString(2));
                    eventDetails.Time = TimeSpan.Parse(reader.GetString(3));
                    eventDetails.Duration = reader.GetInt32(4);
                    eventDetails.Location = reader.GetString(5);
                    eventDetails.Category = reader.GetString(6);
                }
            }

            // Etkinliği oluşturan kişinin bilgilerini al
            var creatorCommand = connection.CreateCommand();
            creatorCommand.CommandText = @"
                SELECT FirstName, LastName, Email, Location, Interests, BirthDate, Gender, ProfilePhoto
                FROM Kullanıcılar
                WHERE ID = (SELECT CreatorID FROM Etkinlikler WHERE ID = @id)";
            creatorCommand.Parameters.AddWithValue("@id", id);

            using (var reader = creatorCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    eventCreator = new Kullanici
                    {
                        Ad = reader.GetString(0),
                        Soyad = reader.GetString(1),
                        Eposta = reader.GetString(2),
                        Konum = reader.GetString(3),
                        IlgiAlanlari = reader.GetString(4),
                        DogumTarihi = reader.GetDateTime(5),
                        Cinsiyet = reader.GetString(6),
                        ProfilFotografi = reader.IsDBNull(7) ? null : reader.GetString(7)
                    };
                }
            }
        }

        // Etkinlik ve kullanıcı bilgilerini view'a göndermek için model olarak birlikte gönderiyoruz
        var model = new EventDetailsViewModel
        {
            EventDetails = eventDetails,
            EventCreator = eventCreator
        };

        return View(model);
    }

    [Authorize]
    public IActionResult ShowJoinedEvents()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); // Giriş yapan kullanıcının ID'si
        var joinedEvents = new List<Etkinlik>();

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT e.ID, e.EventName, e.Date, e.Time, e.Duration, e.Location, e.Category
                FROM Etkinlikler e
                INNER JOIN Katılımcılar k ON e.ID = k.EventID
                WHERE k.UserID = @userId";
            command.Parameters.AddWithValue("@userId", userId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    joinedEvents.Add(new Etkinlik
                    {
                        ID = reader.GetInt32(0),
                        EventName = reader.GetString(1),
                        Date = reader.GetDateTime(2),
                        Time = TimeSpan.Parse(reader.GetString(3)),
                        Duration = reader.GetInt32(4),
                        Location = reader.GetString(5),
                        Category = reader.GetString(6)
                    });
                }
            }
        }

        return View(joinedEvents);
    }

    private void AddPointsForEventCreation(int userId)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Puanlar (UserID, Points, EarnedDate)
                VALUES (@userId, 15, CURRENT_TIMESTAMP)";
            command.Parameters.AddWithValue("@userId", userId);
            command.ExecuteNonQuery();
        }
    }


}
