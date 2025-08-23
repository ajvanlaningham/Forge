using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Forge.Models;

namespace Forge.Services
{
    public class StatsService
    {
        // +10% => +1 score, -10% => -1 score; clamp 1..20, base = 10
        private static int ScoreFrom(double baseline, double current, bool inverse = false)
        {
            if (baseline <= 0 || current <= 0) return 10;
            var ratio = inverse ? (baseline / current) : (current / baseline);
            var delta = ratio - 1.0;
            var steps = Math.Round(delta / 0.10); // 10% per step
            var raw = 10 + (int)steps;
            return Math.Max(1, Math.Min(20, raw));
        }

        // Dummy numbers for now (no DB): 
        //TODO: Local DB
        public IReadOnlyList<Stat> GetCoreStats()
        {
            // ---- Strength (STR): use e1RM normalized by bodyweight ----
            // Baseline: 150kg @ 80kg BW  => 1.875x
            // Current:  165kg @ 80kg BW  => 2.0625x  (~+10%, score 11)
            double e1rmBase = 150, bwBase = 80, e1rmNow = 165, bwNow = 80;
            double strB = e1rmBase / bwBase, strC = e1rmNow / bwNow;
            int strScore = ScoreFrom(strB, strC); // higher = better

            // ---- Dexterity (DEX): deep squat hold (seconds) ----
            // Baseline: 45s → Current: 60s  (+33%, score 13)
            double dexB = 45, dexC = 60;
            int dexScore = ScoreFrom(dexB, dexC); // higher = better

            // ---- Constitution (CON): 10-minute distance (km) ----
            // Baseline: 3.0km → Current: 3.3km  (+10%, score 11)
            double conB = 3.0, conC = 3.3;
            int conScore = ScoreFrom(conB, conC); // higher = better

            return new[]
            {
            new Stat { Kind = StatKind.Strength,     Baseline = strB, Current = strC, Unit = "×BW",      Score = strScore },
            new Stat { Kind = StatKind.Dexterity,    Baseline = dexB, Current = dexC, Unit = "s",        Score = dexScore },
            new Stat { Kind = StatKind.Constitution, Baseline = conB, Current = conC, Unit = "km/10min", Score = conScore },
            };
        }
    }
}
   