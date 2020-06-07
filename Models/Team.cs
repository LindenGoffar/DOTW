using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Threading.Tasks;

namespace DOTW.Models
{
    public enum TeamState
    {
        In,
        Out        
    }
    public class Team
    {
        public int ID { get; set; }
        public Day Day { get; set; }
        public int RoomId { get; set; }
        public Room Room { get; set;  }
        public Row Row { get; set;  }
        public TeamState State { get; set; }
        public ICollection<Player> Players { get; set; }
    }
}
