using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DOTW.Models
{
    public enum Day
    {
        Sunday = 1,
        Monday = 2, 
        Tuesday = 3, 
        Wednesday = 4,
        Thursday = 5,
        Friday = 6,
        Sabbath = 7
    }

    public enum RowState
    {
        AcceptingPlayers,
        TeamLocked, 
        InPlay, 
        RowComplete,
        ExpectedEmpty
    }
    public class Row
    {
        public int ID { get; set; }
        public int Position { get; set; }
        public Day Day { get; set; }
        public RowState State { get; set; }
        public int RoomId { get; set; }
        public Room Room { get; set; }
        public ICollection<Chair> Chairs { get; set; }
        public DateTime CompletedTime { get; set;  }
        public TimeSpan TimeToComplete { get; set; }
        [NotMapped]
        public int TempTeamId { get; set; }
    }
}
