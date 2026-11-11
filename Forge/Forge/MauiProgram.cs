using Forge.Data;
using Forge.Services.Implementations;
using Forge.Services.Interfaces;
using Forge.ViewModels;
using Forge.ViewModels.Controls.Cards;
using Forge.Views;
using Forge.Views.SubPages;

using Microcharts.Maui;

using Microsoft.Extensions.Logging;

using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Forge
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                // Microcharts' ChartView is a SkiaSharp SKCanvasView; both handler
                // registrations are required or the sparkline on WeightTrendCard renders blank.
                .UseSkiaSharp()
                .UseMicrocharts()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("VT323-Regular.ttf", "PixelUI");     // readable retro UI
                    fonts.AddFont("PressStart2P-Regular.ttf", "PixelH");// chunky heading
                    fonts.AddFont("MedievalSharp-Regular.ttf", "FantasyH");//Fantasy heading
                    fonts.AddFont("fa-solid-900.otf", "FA"); //Font Awesome
                    // In-gym numerals: legible at arm's length, still gamified. SIL OFL 1.1 —
                    // license text in Resources/FontLicenses/Jersey15-OFL.txt.
                    fonts.AddFont("Jersey15-Regular.ttf", "GymNum");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            // Data + repos
            builder.Services.AddSingleton<IAppDatabase, AppDatabase>();
            builder.Services.AddSingleton(typeof(IRepository<>), typeof(SQLiteRepository<>));

            //Importer
            builder.Services.AddSingleton<IExerciseLibraryImporter, ExerciseLibraryImporter>();

            //Exercise
            builder.Services.AddSingleton<IExerciseLibraryService, ExerciseLibraryService>();
            builder.Services.AddSingleton<IProgramService, ProgramService>();
            builder.Services.AddSingleton<IActiveRestService, ActiveRestService>();
            builder.Services.AddSingleton<IForgeService, ForgeService>();
            builder.Services.AddSingleton<IStatTestService, StatTestService>();
            builder.Services.AddSingleton<ITrainingService, TrainingService>();
#if ANDROID
            builder.Services.AddSingleton<IRestAlarm, Forge.Platforms.Android.RestAlarm>();
            builder.Services.AddSingleton<IBackupSink, Forge.Platforms.Android.DownloadsBackupSink>();
#else
            builder.Services.AddSingleton<IRestAlarm, NoopRestAlarm>();
            builder.Services.AddSingleton<IBackupSink, AppDataBackupSink>();
#endif
            // Daily backup (PBI 10.1), triggered from App on start/resume.
            builder.Services.AddSingleton<IBackupService, BackupService>();
            // Streak + level-up moment (PBIs 4.2 / 4.3).
            builder.Services.AddSingleton<Forge.Services.ProgressService>();
            builder.Services.AddSingleton<IConditioningWeekService, ConditioningWeekService>();

            // Stats
            builder.Services.AddSingleton<IStatsStore, StatsStore>();
            builder.Services.AddSingleton<IStatsService, StatsService>();
            // Every XP change goes through one service (TODO 8.6: consolidated 2026-11-11).
            builder.Services.AddSingleton<IXpService, XpService>();

            // Weight logging
            builder.Services.AddSingleton<IWeightService, WeightService>();

            // Inventory
            builder.Services.AddSingleton<IInventoryService, InventoryService>();

            // In-app updates (Android delivery path; see TODO.md Epic 9)
            builder.Services.AddSingleton<IUpdateService, UpdateService>();

            // UI
            // The four tabs (Decision 20)
            builder.Services.AddTransient<TodayViewModel>();
            builder.Services.AddTransient<TodayPage>();
            builder.Services.AddTransient<QuestBoardViewModel>();
            builder.Services.AddTransient<CurrentQuestViewModel>();
            builder.Services.AddTransient<QuestPage>();
            builder.Services.AddTransient<HeroViewModel>();
            builder.Services.AddTransient<HeroPage>();
            builder.Services.AddTransient<ChronicleViewModel>();
            builder.Services.AddTransient<ChroniclePage>();
            builder.Services.AddTransient<BriefingViewModel>();
            builder.Services.AddTransient<BriefingPage>();
            builder.Services.AddTransient<GuidedSessionViewModel>();
            builder.Services.AddTransient<GuidedSessionPage>();
            builder.Services.AddTransient<ActiveSessionViewModel>();
            builder.Services.AddTransient<ActiveSessionPage>();
            builder.Services.AddTransient<SessionSummaryViewModel>();
            builder.Services.AddTransient<SessionSummaryPage>();
            builder.Services.AddTransient<SessionHistoryViewModel>();
            builder.Services.AddTransient<ExerciseHistoryViewModel>();
            builder.Services.AddTransient<ExerciseHistoryPage>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<CheckInViewModel>();
            builder.Services.AddTransient<CheckInPage>();

            // Reusable card VMs. Transient because Chronicle and Check-in each hold their own
            // instance — they refresh on OnAppearing and must not share expansion state.
            builder.Services.AddTransient<WeightTrendCardViewModel>();


            builder.Services.AddTransient<ViewModels.SubPages.ExerciseLibraryViewModel>();
            builder.Services.AddTransient<ExerciseLibraryPage>();
            builder.Services.AddTransient<ViewModels.SubPages.MyGearViewModel>();
            builder.Services.AddTransient<MyGearPage>();
            builder.Services.AddTransient<HomeWeightsPage>();
            builder.Services.AddTransient<StatTestPage>();


            return builder.Build();
        }
    }
}
