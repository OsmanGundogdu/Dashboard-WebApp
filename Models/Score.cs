using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DashBoardWebApp.Models
{
    public class Score
    {
        public int UserID { get; set; }
        public int Points { get; set; }
        public DateTime EarnedDate { get; set; }
    }
}
