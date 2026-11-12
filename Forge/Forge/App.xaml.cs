namespace Forge
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // The loading screen (the hammer on the anvil) first, then the tabs.
            var window = new Window();
            // A fresh install gets the first run (start fresh or restore, units, hero, gear, first quest).
            window.Page = new Views.LoadingPage(async () =>
            {
                window.Page = await Views.OnboardingPage.NeededAsync()
                    ? new Views.OnboardingPage(() => window.Page = new AppShell())
                    : new AppShell();
            });

            // Daily backup (PBI 10.1): on launch and whenever the app comes back to the
            // foreground, back up if today's copy doesn't exist yet. Fire-and-forget — it never
            // blocks startup and never shows UI; failures retry on the next foreground.
            window.Created += (_, _) => { BackUpIfDue(); SyncStrava(); SyncGuild(); };
            window.Resumed += (_, _) => { BackUpIfDue(); SyncStrava(); SyncGuild(); };
            return window;
        }

        private static void BackUpIfDue()
        {
            var backup = IPlatformApplication.Current?.Services.GetService<Services.Interfaces.IBackupService>();
            if (backup is not null) _ = Task.Run(backup.BackUpIfDueAsync);
            // The encrypted off-phone copy (Decision 24) rides along, at most once a day.
            var offsite = IPlatformApplication.Current?.Services.GetService<Services.Interfaces.IOffsiteBackupService>();
            if (offsite is { IsSetUp: true }) _ = Task.Run(offsite.UploadIfDueAsync);
        }

        /// <summary>Tells the guild what changed (a session finished, a new day) and pays guild bonuses.</summary>
        public static void SyncGuild()
        {
            var guild = IPlatformApplication.Current?.Services.GetService<Services.Interfaces.IGuildService>();
            if (guild is { InGuild: true }) _ = Task.Run(guild.SyncAsync);
        }

        /// <summary>Posts any finished sessions still waiting for Strava (offline at the time, say).</summary>
        public static void SyncStrava()
        {
            var strava = IPlatformApplication.Current?.Services.GetService<Services.Interfaces.IStravaService>();
            if (strava is { IsConnected: true }) _ = Task.Run(strava.SyncAsync);
        }
    }
}