using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DOTW.Models
{
    public enum RoomState
    {
        Closed, 
        TeamSelection, 
        InPlay
    }
    public class Room
    {
        public int ID { get; set; }
        public string HostEmail { get; set; }
        public string RoomName { get; set; }
        public Day Day { get; set;  }
        public RoomState State { get; set; }
        public int PlayerCount { get; set; }
        public ICollection<Row> Rows { get; set; }
        public ICollection<Player> Players { get; set; }
        public DateTime LastDayChange { get; set;  }
    }
}
