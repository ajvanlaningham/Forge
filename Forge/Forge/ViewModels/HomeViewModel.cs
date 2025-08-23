using System.ComponentModel;
using Forge.Services;
using Forge.Models;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Forge.ViewModels
{

    public class HomeViewModel : INotifyPropertyChanged
    {
        private readonly StatsService _stats;

        public HomeViewModel() : this(new StatsService()) { }

        public HomeViewModel(StatsService statsService)
        {
            _stats = statsService;

            // TODO: DB
            var stats = _stats.GetCoreStats();
            StrengthScore = stats.First(s => s.Kind == StatKind.Strength).Score;
            DexterityScore = stats.First(s => s.Kind == StatKind.Dexterity).Score;
            ConstitutionScore = stats.First(s => s.Kind == StatKind.Constitution).Score;

            BeginTrainingCommand = new AsyncRelayCommand(async () =>
                await Shell.Current.GoToAsync("//train"));
        }

        private int _strength;
        public int StrengthScore { get => _strength; set => Set(ref _strength, value); }

        private int _dexterity;
        public int DexterityScore { get => _dexterity; set => Set(ref _dexterity, value); }

        private int _constitution;
        public int ConstitutionScore { get => _constitution; set => Set(ref _constitution, value); }

        public ICommand BeginTrainingCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
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