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
            builder.Services.AddSingleton<IQuestService, QuestService>();
            builder.Services.AddSingleton<IConditioningWeekService, ConditioningWeekService>();

            // Stats
            builder.Services.AddSingleton<IStatsStore, StatsStore>();
            builder.Services.AddSingleton<IStatsService, StatsService>();

            // Weight logging
            builder.Services.AddSingleton<IWeightService, WeightService>();

            // Inventory
            builder.Services.AddSingleton<IInventoryService, InventoryService>();

            // In-app updates (Android delivery path; see TODO.md Epic 9)
            builder.Services.AddSingleton<IUpdateService, UpdateService>();

            // UI
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<StatsViewModel>();
            builder.Services.AddTransient<StatsPage>();
            builder.Services.AddTransient<QuestsViewModel>();
            builder.Services.AddTransient<QuestsPage>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<CheckInViewModel>();
            builder.Services.AddTransient<CheckInPage>();

            // Reusable card VMs. Transient because Home and Check-in each hold their own
            // instance — they refresh on OnAppearing and must not share expansion state.
            builder.Services.AddTransient<WeightTrendCardViewModel>();


            builder.Services.AddTransient<ViewModels.SubPages.ExerciseLibraryViewModel>();
            builder.Services.AddTransient<ExerciseLibraryPage>();
            builder.Services.AddTransient<ViewModels.SubPages.MyGearViewModel>();
            builder.Services.AddTransient<MyGearPage>();


            return builder.Build();
        }
    }
}
