using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Forge.Models
{
    public enum StatKind
    {
        Strength,   // STR: max force (we’ll use e1RM/BW)
        Dexterity,  // DEX: mobility/control (deep squat hold time)
        Constitution // CON: endurance/resilience (10-min distance for now)
    }
    public class Stat
    {
        public StatKind Kind { get; init; }
        public double Baseline { get; init; } // B
        public double Current { get; init; } // C
        public string Unit { get; init; } = "";
        public int Score { get; init; } // 1..20, 10 = baseline
    }
}
