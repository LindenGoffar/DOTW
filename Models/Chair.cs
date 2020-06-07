using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Threading.Tasks;

namespace DOTW.Models
{
    public enum ChairState
    {
        Empty,
        OccupiedInvalid,
        OccupiedValid
    }
    public class Chair
    {
        public int ID { get; set; }
        public int RowId { get; set; }
        public Row Row { get; set; }
        public Player Player { get; set; }
        public ChairState State { get; set; }
    }
}
