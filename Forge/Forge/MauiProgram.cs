using Forge.Data;
using Forge.Services.Implementations;
using Forge.Services.Interfaces;
using Forge.ViewModels;
using Forge.Views;
using Forge.Views.SubPages;

using Microsoft.Extensions.Logging;

namespace Forge
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
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

            // Stats
            builder.Services.AddSingleton<IStatsStore, StatsStore>();
            builder.Services.AddSingleton<IStatsService, StatsService>();

            // Inventory
            builder.Services.AddSingleton<IInventoryService, InventoryService>();

            // UI
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<StatsViewModel>();
            builder.Services.AddTransient<StatsPage>();
            builder.Services.AddTransient<QuestsViewModel>();
            builder.Services.AddTransient<QuestsPage>();


            builder.Services.AddTransient<ViewModels.SubPages.ExerciseLibraryViewModel>();
            builder.Services.AddTransient<ExerciseLibraryPage>();
            builder.Services.AddTransient<ViewModels.SubPages.MyGearViewModel>();
            builder.Services.AddTransient<MyGearPage>();


            return builder.Build();
        }
    }
}
