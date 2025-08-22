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
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
