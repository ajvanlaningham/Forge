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
            var window = new Window(new AppShell());

            // Daily backup (PBI 10.1): on launch and whenever the app comes back to the
            // foreground, back up if today's copy doesn't exist yet. Fire-and-forget — it never
            // blocks startup and never shows UI; failures retry on the next foreground.
            window.Created += (_, _) => BackUpIfDue();
            window.Resumed += (_, _) => BackUpIfDue();
            return window;
        }

        private static void BackUpIfDue()
        {
            var backup = IPlatformApplication.Current?.Services.GetService<Services.Interfaces.IBackupService>();
            if (backup is not null) _ = Task.Run(backup.BackUpIfDueAsync);
        }
    }
}