using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DashBoardWebApp.Models;
using System.Collections.Generic;

namespace DashBoardWebApp.Controllers
{
    public class ParticipantController : Controller
    {
        private string connectionString = "Data Source=./Database/app.db";

        // Etkinliğe Katılma İşlemi
        [HttpPost]
        public IActionResult JoinEvent(int userId, int eventId)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                                        INSERT INTO Katılımcılar (UserID, EventID)
                                        VALUES (@userId, @eventId)";
                
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@eventId", eventId);

                command.ExecuteNonQuery();
            }

            return RedirectToAction("EventParticipants", new { eventId = eventId });
        }

        // Etkinliğe Katılanları Listeleme
        public IActionResult EventParticipants(int eventId)
        {
            List<Participant> participants = new List<Participant>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Katılımcılar WHERE EventID = @eventId";
                command.Parameters.AddWithValue("@eventId", eventId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var participant = new Participant
                        {
                            UserID = reader.GetInt32(0),
                            EventID = reader.GetInt32(1)
                        };
                        participants.Add(participant);
                    }
                }
            }

            return View(participants);
        }
    }
}
