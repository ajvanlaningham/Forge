using Forge.Constants;

namespace Forge.Tests;

public class XpLedgerTests
{
    [Fact]
    public void Completing_PaysOnce()
    {
        var (entry, delta) = XpLedger.Reconcile(true, new XpEntry(false, 0), award: 60);
        Assert.Equal(60, delta);
        Assert.Equal(new XpEntry(true, 60), entry);
        var (again, delta2) = XpLedger.Reconcile(true, entry, award: 60);
        Assert.Equal(0, delta2);                 // reconciling twice never pays twice
        Assert.Equal(entry, again);
    }

    [Fact]
    public void Undoing_TakesBackExactlyWhatWasPaid_EvenIfTheAwardChanged()
    {
        var (paid, _) = XpLedger.Reconcile(true, new XpEntry(false, 0), award: 60);   // ×1.2 week: 60
        var (undone, delta) = XpLedger.Reconcile(false, paid, award: 50);              // multiplier gone now
        Assert.Equal(-60, delta);
        Assert.Equal(new XpEntry(false, 0), undone);
    }

    [Fact]
    public void DoUndoDo_IsSymmetric()
    {
        var entry = new XpEntry(false, 0);
        var total = 0;
        foreach (var (done, award) in new[] { (true, 162), (false, 162), (true, 194), (false, 150), (true, 162) })
        {
            (entry, var d) = XpLedger.Reconcile(done, entry, award);
            total = XpLedger.Apply(total, d);
        }
        Assert.Equal(162, total);                // only the last completion stands
    }

    [Fact]
    public void LegacyRecords_ReclaimTheirOldAward()
    {
        var (_, delta) = XpLedger.Reconcile(false, new XpEntry(true, 0), award: 60, legacyAward: 50);
        Assert.Equal(-50, delta);
    }

    [Fact]
    public void Totals_NeverGoNegative()
        => Assert.Equal(0, XpLedger.Apply(30, -50));
}
