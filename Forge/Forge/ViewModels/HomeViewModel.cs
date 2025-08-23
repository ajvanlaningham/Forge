using Forge.Models;
using Forge.Resources.Strings;
using System.Windows.Input;
using Forge.ViewModels.Controls.Cards;
using Forge.Services.Implementations;

namespace Forge.ViewModels
{
    public class HomeViewModel : BaseViewModel
    {
        private readonly StatsService _stats;

        // Keeps your existing call sites working
        public HomeViewModel() : this(new StatsService()) { }

        public HomeViewModel(StatsService statsService)
        {
            _stats = statsService;

            BeginTrainingCommand = new AsyncRelayCommand(async () =>
                await Shell.Current.GoToAsync("//train"));

            // create the card; values get filled after InitializeAsync runs
            StatsCard = new StatCardViewModel
            {
                Title = AppResources.Home_StatCard_Title,
                Strength = 0,
                Dexterity = 0,
                Constitution = 0
            };
        }

        /// <summary>
        /// Call this from HomePage.OnAppearing().
        /// Initializes the DB, then loads STR/DEX/CON from SQLite (seeded at 1).
        /// </summary>
        public async Task InitializeAsync()
        {
            await _stats.InitAsync();

            var stats = await _stats.GetCoreStatsAsyncFromDb();

            StrengthScore = stats.First(s => s.Kind == StatKind.Strength).Score;
            DexterityScore = stats.First(s => s.Kind == StatKind.Dexterity).Score;
            ConstitutionScore = stats.First(s => s.Kind == StatKind.Constitution).Score;

            // Reflect values into the card VM
            StatsCard.Strength = StrengthScore;
            StatsCard.Dexterity = DexterityScore;
            StatsCard.Constitution = ConstitutionScore;
        }

        private int _strength;
        public int StrengthScore { get => _strength; set => SetProperty(ref _strength, value); }

        private int _dexterity;
        public int DexterityScore { get => _dexterity; set => SetProperty(ref _dexterity, value); }

        private int _constitution;
        public int ConstitutionScore { get => _constitution; set => SetProperty(ref _constitution, value); }

        public StatCardViewModel StatsCard { get; }

        public ICommand BeginTrainingCommand { get; }
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
