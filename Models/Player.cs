using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DOTW.Models
{
    public enum PlayerState
    {
        UnClaimed,
        InPlay, 
        Out,
        ProperlySeated
    }
    public class Player
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public PlayerState State { get; set; }
        public int RoomId { get; set;  }
        public Room Room { get; set; }
        public Team Team { get; set; }
        //public Chair Chair { get; set;  }
    }
}
