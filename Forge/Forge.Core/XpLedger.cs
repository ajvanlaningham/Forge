namespace Forge.Constants
{
    /// <summary>What an XP-earning record has been paid: whether, and exactly how much.</summary>
    public readonly record struct XpEntry(bool Granted, int Awarded);

    /// <summary>
    /// The one rule every XP award follows (training days, active rest, conditioning weeks, weigh-ins):
    /// a completed thing is paid once, at the award it earned then; un-completing it takes back
    /// exactly what was paid, whatever the award would be now (a multiplier may have changed since).
    /// </summary>
    public static class XpLedger
    {
        /// <param name="completed">Whether the thing counts as done right now.</param>
        /// <param name="entry">What it has been paid so far.</param>
        /// <param name="award">What it would earn if paid now.</param>
        /// <param name="legacyAward">For records paid before amounts were stored (Awarded = 0): what they were paid.</param>
        /// <returns>The updated entry and the XP change to apply.</returns>
        public static (XpEntry Entry, int Delta) Reconcile(bool completed, XpEntry entry, int award, int legacyAward = 0)
        {
            if (completed && !entry.Granted)
                return (new XpEntry(true, award), award);
            if (!completed && entry.Granted)
            {
                var paid = entry.Awarded > 0 ? entry.Awarded : legacyAward;
                return (new XpEntry(false, 0), -paid);
            }
            return (entry, 0);
        }

        /// <summary>A total never goes below zero, however much is taken back.</summary>
        public static int Apply(int total, int delta) => Math.Max(0, total + delta);
    }
}
