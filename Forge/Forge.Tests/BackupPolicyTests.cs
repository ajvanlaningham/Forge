using Forge.Constants;

namespace Forge.Tests;

public class BackupPolicyTests
{
    [Fact]
    public void NeverBackedUp_IsDue()
        => Assert.True(BackupPolicy.IsDue(null, DateTime.UtcNow));

    [Fact]
    public void BackedUpEarlierToday_IsNotDue()
    {
        var now = DateTime.Now.Date.AddHours(20).ToUniversalTime();
        Assert.False(BackupPolicy.IsDue(now.AddHours(-1), now));
    }

    [Fact]
    public void BackedUpYesterday_IsDue_EvenUnder24HoursAgo()
    {
        var morning = DateTime.Now.Date.AddHours(7).ToUniversalTime();
        var lastNight = DateTime.Now.Date.AddHours(-1).ToUniversalTime(); // 23:00 yesterday
        Assert.True(BackupPolicy.IsDue(lastNight, morning));
    }

    [Fact]
    public void FileName_IsStampedAndRecognised()
    {
        var name = BackupPolicy.FileName(new DateTime(2026, 11, 11, 7, 5, 0), 2);
        Assert.Equal("forge-backup-20261111-0705-s2.db3", name);
        Assert.True(BackupPolicy.IsBackupFile(name));
        Assert.Equal(2, BackupPolicy.SchemaOf(name));
    }

    [Theory]
    [InlineData("forge.db3")]
    [InlineData("forge-backup-2026-11-11.db3")]
    [InlineData("photo.jpg")]
    public void ForeignFiles_AreNotBackups(string name)
    {
        Assert.False(BackupPolicy.IsBackupFile(name));
        Assert.Null(BackupPolicy.SchemaOf(name));
    }

    [Fact]
    public void Prune_KeepsTheNewest_AndIgnoresForeignFiles()
    {
        var names = Enumerable.Range(1, 16)
            .Select(d => BackupPolicy.FileName(new DateTime(2026, 11, d, 7, 0, 0)))
            .Append("notes.txt")
            .Reverse() // input order must not matter
            .ToList();

        var prune = BackupPolicy.ToPrune(names, retain: 14);

        Assert.Equal(new[] { "forge-backup-20261102-0700-s2.db3", "forge-backup-20261101-0700-s2.db3" }, prune);
    }

    [Fact]
    public void Prune_WithFewerThanRetain_DeletesNothing()
        => Assert.Empty(BackupPolicy.ToPrune(new[] { BackupPolicy.FileName(DateTime.Now) }));
}
