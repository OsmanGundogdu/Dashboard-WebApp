using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DashBoardWebApp.Models
{
    public class EventDetailsViewModel
    {
        public Etkinlik EventDetails { get; set; }
        public Kullanici EventCreator { get; set; }
    }
}