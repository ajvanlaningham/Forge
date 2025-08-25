using Forge.Models;
using Forge.Resources.Strings;
using System.Windows.Input;
using Forge.ViewModels.Controls.Cards;
using Forge.Services.Interfaces;
using Forge.Constants;

namespace Forge.ViewModels
{
    public class HomeViewModel : BaseViewModel
    {
        private readonly IStatsService _stats;
        private readonly IExerciseLibraryImporter _importer;

        public HomeViewModel(IStatsService statsService, IExerciseLibraryImporter importer)
        {
            _stats = statsService;
            _importer = importer;

            BeginTrainingCommand = new AsyncRelayCommand(async () =>
                await Shell.Current.GoToAsync("//train"));

            GoToStatsCommand = new AsyncRelayCommand(async () =>
                await Shell.Current.GoToAsync("//stats"));

            StatsCard = new StatCardViewModel
            {
                Title = AppResources.Home_StatCard_Title,
                Level = 1,
                Xp = 0,
                Strength = 0,
                Dexterity = 0,
                Constitution = 0
            };
        }

        public async Task InitializeAsync()
        {
            await _importer.EnsureSeededAsync(
                GameConstants.Exercises.LibraryFiles,
                GameConstants.Exercises.LibraryVersion);

            await _stats.InitAsync();

            var stats = await _stats.GetCoreStatsAsyncFromDb();
            var userStats = await _stats.GetUserStatsAsync();

            UserLevel = userStats.Level;
            UserXp = userStats.Xp;

            StrengthScore = stats.FirstOrDefault(s => s.Kind == StatKind.Strength)?.Score ?? 1;
            DexterityScore = stats.FirstOrDefault(s => s.Kind == StatKind.Dexterity)?.Score ?? 1;
            ConstitutionScore = stats.FirstOrDefault(s => s.Kind == StatKind.Constitution)?.Score ?? 1;

            // Reflect to card
            StatsCard.Level = UserLevel;

            const int xpPerLevel = 1000;
            StatsCard.Xp = UserXp;
            StatsCard.XpProgress = GameMath.LevelProgress(UserXp);

            StatsCard.Strength = StrengthScore;
            StatsCard.Dexterity = DexterityScore;
            StatsCard.Constitution = ConstitutionScore;
        }

        private int _userLevel;
        public int UserLevel { get => _userLevel; set => SetProperty(ref _userLevel, value); }

        private int _userXp;
        public int UserXp { get => _userXp; set => SetProperty(ref _userXp, value); }

        private int _strength;
        public int StrengthScore { get => _strength; set => SetProperty(ref _strength, value); }

        private int _dexterity;
        public int DexterityScore { get => _dexterity; set => SetProperty(ref _dexterity, value); }

        private int _constitution;
        public int ConstitutionScore { get => _constitution; set => SetProperty(ref _constitution, value); }

        public StatCardViewModel StatsCard { get; }

        public ICommand BeginTrainingCommand { get; }
        public ICommand GoToStatsCommand { get; }

    }

    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            try
            {
                _isExecuting = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                await _execute();
            }
            finally
            {
                _isExecuting = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CanExecuteChanged;
    }
}
