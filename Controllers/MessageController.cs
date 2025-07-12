using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DashBoardWebApp.Models;
using System.Security.Claims;

namespace DashBoardWebApp.Controllers
{
    public class MessageController : Controller
    {
        private string connectionString = "Data Source=./Database/app.db";

        [Authorize]
        public IActionResult ShowChat(int eventId)
        {
            var messages = new List<MessageWithSender>();
            var eventName = "";
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)); // Giriş yapan kullanıcının ID'si

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Kullanıcının bu etkinliğe katılıp katılmadığını kontrol ediyoruz
                var isParticipant = false;

                if (!User.IsInRole("Admin")) // Admin değilse katılım kontrolü yap
                {
                    var participationCommand = connection.CreateCommand();
                    participationCommand.CommandText = @"
                        SELECT COUNT(1)
                        FROM Katılımcılar
                        WHERE EventID = @eventId AND UserID = @userId";
                    participationCommand.Parameters.AddWithValue("@eventId", eventId);
                    participationCommand.Parameters.AddWithValue("@userId", userId);

                    isParticipant = Convert.ToInt32(participationCommand.ExecuteScalar()) > 0;

                    if (!isParticipant)
                    {
                        TempData["Message"] = "Bu etkinlik için mesajlaşma paneline erişiminiz yok!";
                        TempData["MessageClass"] = "alert-danger";
                        return RedirectToAction("GeneralEvents", "Event");
                    }
                }

                // Etkinlik adı alınıyor
                var eventCommand = connection.CreateCommand();
                eventCommand.CommandText = "SELECT EventName FROM Etkinlikler WHERE ID = @eventId";
                eventCommand.Parameters.AddWithValue("@eventId", eventId);

                using (var reader = eventCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        eventName = reader.GetString(0);
                    }
                }

                // Mesajlar alınıyor
                var messageCommand = connection.CreateCommand();
                messageCommand.CommandText = @"
                    SELECT m.MessageText, m.SentTime, u.FirstName, u.LastName
                    FROM Mesajlar m
                    INNER JOIN Kullanıcılar u ON m.SenderID = u.ID
                    WHERE m.EventID = @eventId
                    ORDER BY m.SentTime ASC";
                messageCommand.Parameters.AddWithValue("@eventId", eventId);

                using (var reader = messageCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        messages.Add(new MessageWithSender
                        {
                            SenderName = $"{reader.GetString(2)} {reader.GetString(3)}",
                            MessageText = reader.GetString(0),
                            SentTime = reader.GetDateTime(1)
                        });
                    }
                }
            }

            var model = new ChatViewModel
            {
                EventID = eventId,
                EventName = eventName,
                Messages = messages
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        public IActionResult SendMessage(int eventId, string messageText)
        {
            var senderId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO Mesajlar (EventID, SenderID, MessageText)
                    VALUES (@eventId, @senderId, @messageText)";
                insertCommand.Parameters.AddWithValue("@eventId", eventId);
                insertCommand.Parameters.AddWithValue("@senderId", senderId);
                insertCommand.Parameters.AddWithValue("@messageText", messageText);
                insertCommand.ExecuteNonQuery();
            }

            return RedirectToAction("ShowChat", new { eventId });
        }


    }
}

