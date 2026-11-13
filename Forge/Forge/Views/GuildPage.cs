using Forge.Common;
using Forge.Guild;
using Forge.Services;
using Forge.Services.Interfaces;

namespace Forge.Views;

/// <summary>
/// The Guild tab (Decisions 20 and 24): found or join a guild, then see the joint weekly quest,
/// the campfire (two thirds moved today), banners (everyone forged another quest) and each member.
/// Built in code, like the onboarding screens.
/// </summary>
public sealed class GuildPage : ContentPage
{
    private readonly IGuildService _guild;
    private readonly VerticalStackLayout _body = new() { Padding = new Thickness(20, 16), Spacing = 14 };
    private readonly Label _status = new() { FontSize = 15, TextColor = Muted, FontFamily = "Body" };
    private bool _busy;

    private static Color Res(string k) => (Color)Application.Current!.Resources[k];
    private static Color Accent => Res("Accent");
    private static Color Muted => Res("Muted");
    private static Color Surface => Res("Surface");
    private static Color Rest => Res("Leaf");

    public GuildPage(IGuildService guild)
    {
        _guild = guild;
        Title = "Guild";
        Content = new RefreshView { Content = new ScrollView { Content = _body }, Command = new Command(async () => await SyncAsync(fromPull: true)) };
        TabChrome.AddSettingsCog(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Render();
        await SyncAsync();
    }

    private async Task SyncAsync(bool fromPull = false)
    {
        if (_busy) { if (Content is RefreshView rv0) rv0.IsRefreshing = false; return; }
        _busy = true;
        try
        {
            if (_guild.InGuild)
            {
                var error = await _guild.SyncAsync();
                Render(error);
            }
        }
        finally
        {
            _busy = false;
            if (Content is RefreshView rv) rv.IsRefreshing = false;
        }
    }

    // ------------------------------------------------------------------ layout

    private void Render(string? error = null)
    {
        _body.Children.Clear();
        if (!_guild.InGuild) RenderLobby();
        else RenderGuild();
        _status.Text = error ?? SyncedText();
        _body.Children.Add(_status);
    }

    private string SyncedText()
    {
        if (!_guild.InGuild || _guild.LastSyncUtc is not DateTime t) return "";
        var ago = DateTime.UtcNow - t;
        return ago.TotalMinutes < 1 ? "Synced just now." : ago.TotalHours < 1 ? $"Synced {(int)ago.TotalMinutes} min ago." : $"Synced {t.ToLocalTime():ddd h:mm tt}.";
    }

    private static Label Heading(string text, double size = 22) => new() { Text = text, FontFamily = "FantasyH", FontSize = size, TextColor = Accent };
    private static Label Text(string text, double size = 15, Color? color = null) => new() { Text = text, FontFamily = "Body", FontSize = size, TextColor = color ?? Color.FromArgb("#E6E1CF") };

    private static Border Card(params IView[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        foreach (var c in children) stack.Children.Add(c);
        return new Border
        {
            Content = stack,
            Padding = 14,
            BackgroundColor = Surface,
            Stroke = new SolidColorBrush(Color.FromArgb("#3A3A32")),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
        };
    }

    private static Button Primary(string text, Func<Task> act)
    {
        var b = new Button { Text = text, BackgroundColor = Color.FromArgb("#D4AF37"), TextColor = Color.FromArgb("#101418"), FontFamily = "Display", FontSize = 18 };
        b.Clicked += async (_, _) => await act();
        return b;
    }

    private static Button Plain(string text, Func<Task> act)
    {
        var b = new Button { Text = text, BackgroundColor = Colors.Transparent, BorderColor = Color.FromArgb("#D4AF37"), BorderWidth = 2, TextColor = Color.FromArgb("#E6E1CF"), FontFamily = "Display", FontSize = 17 };
        b.Clicked += async (_, _) => await act();
        return b;
    }

    // ------------------------------------------------------------------ not in a guild

    private void RenderLobby()
    {
        _body.Children.Add(Heading("The Guild Hall", 26));
        _body.Children.Add(Text("Train alongside friends. A guild shares a joint weekly quest, a campfire that's lit on days two thirds of you move, and banners for every quest you all forge. A guild becomes official, and starts earning bonus XP, at three members."));

        var name = new Entry
        {
            Text = _guild.DisplayName.Length > 0 ? _guild.DisplayName : "",
            Placeholder = UserSettings.HeroTitle,
            MaxLength = GuildLimits.MaxNameLength,
            FontFamily = "Body",
        };
        _body.Children.Add(Card(Text("Your name in the guild", 15, Muted), name));

        _body.Children.Add(Primary("Found a guild", async () =>
        {
            _guild.DisplayName = name.Text ?? "";
            var guildName = await DisplayPromptAsync("Found a guild", "Name your guild.", "Found", "Cancel",
                placeholder: "The Iron Anvils", maxLength: GuildLimits.MaxGuildNameLength);
            if (string.IsNullOrWhiteSpace(guildName)) return;
            await RunAsync(() => _guild.CreateAsync(guildName));
        }));
        _body.Children.Add(Plain("Join with an invite code", async () =>
        {
            _guild.DisplayName = name.Text ?? "";
            var code = await DisplayPromptAsync("Join a guild", "Enter the invite code a member shared with you.", "Join", "Cancel",
                placeholder: "K7QF-9M2D", maxLength: 12, keyboard: Keyboard.Create(KeyboardFlags.CapitalizeCharacter));
            if (string.IsNullOrWhiteSpace(code)) return;
            await RunAsync(() => _guild.JoinAsync(code));
        }));

        _body.Children.Add(Text(
            "What the guild sees: your name, your hero and level, which days you trained or rested, your streak, and your quest. " +
            "Never your weights, sets, bodyweight, heart rate, sleep or steps.", 15, Muted));
    }

    private async Task RunAsync(Func<Task<string?>> act)
    {
        if (_busy) return;
        _busy = true;
        _status.Text = "Contacting the guild hall…";
        try
        {
            var error = await act();
            Render(error);
        }
        finally { _busy = false; }
    }

    // ------------------------------------------------------------------ in a guild

    private void RenderGuild()
    {
        var members = _guild.Members().ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var me = _guild.MemberId;

        _body.Children.Add(Heading(_guild.GuildName ?? "Your guild", 26));
        var invite = new HorizontalStackLayout { Spacing = 10 };
        invite.Children.Add(Text($"Invite code: {_guild.InviteCode}", 17, Muted));
        var share = new Button { Text = "Share", FontFamily = "Display", FontSize = 15, Padding = new Thickness(10, 0), HeightRequest = 34, BackgroundColor = Colors.Transparent, BorderColor = Accent, BorderWidth = 1, TextColor = Accent };
        share.Clicked += async (_, _) => await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Join my Forge guild",
            Text = $"Join my Forge guild \"{_guild.GuildName}\": open Forge → Guild → Join with an invite code → {_guild.InviteCode}",
        });
        invite.Children.Add(share);
        _body.Children.Add(invite);

        if (!GuildRules.IsOfficial(members.Count))
        {
            var missing = GuildRules.OfficialSize - members.Count;
            _body.Children.Add(Card(
                Text("PRE-GUILD", 15, Muted),
                Text(missing == 1 ? "1 more member makes it official." : $"{missing} more members make it official.", 20, Accent),
                Text("Bonuses start at three members. Share the invite code.", 17)));
        }

        // Joint weekly quest
        var goal = GuildRules.WeeklyGoal(members, today);
        var done = GuildRules.TrainedDays(members, today);
        var met = GuildRules.WeeklyQuestMet(members, today);
        _body.Children.Add(Card(
            Text("THIS WEEK'S JOINT QUEST", 15, Muted),
            Text(met ? $"Complete! {done} training days together." : $"Train {goal} days together: {done} so far", 18),
            new ProgressBar { Progress = goal == 0 ? 0 : Math.Min(1, done / (double)goal), ProgressColor = Accent, HeightRequest = 10 },
            Text(met ? $"+{GuildRules.WeeklyQuestXp} XP to everyone who trained this week." : $"+{GuildRules.WeeklyQuestXp} XP each for those who train, when the guild gets there.", 15, Muted)));

        // Campfire (Decision 26): two thirds of the guild moved today.
        var lit = GuildRules.CampfireLit(members, today);
        var moved = members.Where(m => m.MemberOn(today)).Select(m => (m.Status.Name, Moved: m.On(today) != GuildDayKind.None)).ToList();
        _body.Children.Add(Card(
            Text("THE CAMPFIRE", 15, Muted),
            Text(lit ? "Lit! Two thirds of you moved today." : $"{moved.Count(m => m.Moved)} of {moved.Count} have moved today.", 20, lit ? Accent : null),
            Text(string.Join("   ", moved.Select(m => $"{(m.Moved ? "■" : "□")} {m.Name}")), 17),
            Text($"+{GuildRules.CampfireXp} XP to each of you who moved, on days two thirds of the guild did. Training or active rest both count.", 15, Muted)));

        // Banners
        var banners = GuildRules.Banners(members);
        var next = banners + 1;
        _body.Children.Add(Card(
            Text("BANNERS", 15, Muted),
            Text(banners == 0 ? "No banners yet." : $"{banners} banner{(banners == 1 ? "" : "s")} raised.", 18),
            Text($"Banner {next} rises when everyone has forged {next} quest{(next == 1 ? "" : "s")} since joining: " +
                 string.Join(", ", members.Select(m => $"{m.Status.Name} {m.ForgedSinceJoining}/{next}")), 15),
            Text($"+{GuildRules.BannerXp} XP each per banner.", 15, Muted)));

        // Members
        _body.Children.Add(Heading("Members", 20));
        var monday = WeekMath.MondayOf(today);
        foreach (var m in members) _body.Children.Add(MemberCard(m, m.Id == me, monday, today));

        _body.Children.Add(Plain("Sync now", async () => { _status.Text = "Syncing…"; await SyncAsync(); }));
        _body.Children.Add(new Button
        {
            Text = "Leave guild", BackgroundColor = Colors.Transparent, TextColor = Muted, FontFamily = "Display", FontSize = 15,
            Command = new Command(async () =>
            {
                if (!await DisplayAlertAsync("Leave the guild?", "Bonus XP already earned stays yours. You can rejoin with the invite code.", "Leave", "Stay")) return;
                await RunAsync(_guild.LeaveAsync);
            }),
        });
    }

    private static View MemberCard(GuildMember m, bool isMe, DateOnly monday, DateOnly today)
    {
        var s = m.Status;
        var sprite = new Image
        {
            Source = UserSettings.IsHero(s.Race, s.Class) ? UserSettings.HeroSprite(s.Race, s.Class) : "char_construct_wizard.png",
            WidthRequest = 48, HeightRequest = 75, Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Start,
        };

        var week = new HorizontalStackLayout { Spacing = 6 };
        for (var i = 0; i < 7; i++)
        {
            var day = monday.AddDays(i);
            var kind = m.On(day);
            week.Children.Add(new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    new Label { Text = "MTWTFSS"[i].ToString(), FontFamily = "Body", FontSize = 15, TextColor = Muted, HorizontalTextAlignment = TextAlignment.Center },
                    new Label
                    {
                        Text = day > today ? "·" : kind switch { GuildDayKind.Trained => "■", GuildDayKind.ActiveRest => "▲", _ => "□" },
                        FontSize = 17, HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = kind switch { GuildDayKind.Trained => Accent, GuildDayKind.ActiveRest => Rest, _ => Muted },
                    },
                },
            });
        }

        var info = new VerticalStackLayout { Spacing = 2 };
        info.Children.Add(Text(isMe ? $"{s.Name} (you)" : s.Name, 18, isMe ? Accent : null));
        info.Children.Add(Text($"Lv {s.Level} · {UserSettings.HeroTitleFor(s.Race, s.Class)}", 15, Muted));
        if (!string.IsNullOrEmpty(s.QuestName)) info.Children.Add(Text($"{s.QuestName} · week {s.QuestWeek}", 15));
        info.Children.Add(week);
        info.Children.Add(Text(s.Streak > 0 ? $"Streak {s.Streak} day{(s.Streak == 1 ? "" : "s")} · {GuildRules.TrainedDays(m, today)} training this week" : $"{GuildRules.TrainedDays(m, today)} training days this week", 15, Muted));

        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 14 };
        grid.Add(sprite, 0, 0);
        grid.Add(info, 1, 0);
        return Card(grid);
    }
}
