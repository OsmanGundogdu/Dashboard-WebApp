using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DashBoardWebApp.Models;
using System.Collections.Generic;

namespace DashBoardWebApp.Controllers
{
    public class ScoreController : Controller
    {
        private string connectionString = "Data Source=./Database/app.db";

        // Kullanıcının puanlarını listeleme
        public IActionResult Index(int userId)
        {
            List<Score> scores = new List<Score>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Puanlar WHERE UserID = @userId";
                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var score = new Score
                        {
                            UserID = reader.GetInt32(0),
                            Points = reader.GetInt32(1),
                            EarnedDate = reader.GetDateTime(2)
                        };
                        scores.Add(score);
                    }
                }
            }

            return View(scores);
        }

        // Puan Ekleme Sayfası
        public IActionResult AddScore()
        {
            return View();
        }

        // Puan Ekleme İşlemi
        [HttpPost]
        public IActionResult AddScore(Score newScore)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                                        INSERT INTO Puanlar (UserID, Points, EarnedDate)
                                        VALUES (@userId, @points, datetime('now'))";
                
                command.Parameters.AddWithValue("@userId", newScore.UserID);
                command.Parameters.AddWithValue("@points", newScore.Points);

                command.ExecuteNonQuery();
            }

            return RedirectToAction("Index", new { userId = newScore.UserID });
        }
    }
}
