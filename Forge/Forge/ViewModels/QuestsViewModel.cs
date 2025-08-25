using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services.Interfaces;
using System.Windows.Input;

namespace Forge.ViewModels
{
    public class QuestsViewModel : BaseViewModel
    {
        private readonly IQuestService _quests;

        private DateOnly _todayDate;

        private DailyQuests? _today;
        public DailyQuests? Today
        {
            get => _today;
            private set => SetProperty(ref _today, value);
        }

        private string _dayFocusDisplay = "—";
        public string DayFocusDisplay
        {
            get => _dayFocusDisplay;
            private set => SetProperty(ref _dayFocusDisplay, value);
        }

        private bool _isRecoveryDay;
        public bool IsRecoveryDay
        {
            get => _isRecoveryDay;
            private set => SetProperty(ref _isRecoveryDay, value);
        }

        private bool _isStrengthCompleted;
        public bool IsStrengthCompleted
        {
            get => _isStrengthCompleted;
            private set => SetProperty(ref _isStrengthCompleted, value);
        }

        private bool _isMobilityCompleted;
        public bool IsMobilityCompleted
        {
            get => _isMobilityCompleted;
            private set => SetProperty(ref _isMobilityCompleted, value);
        }

        private bool _isConditioningCompleted;
        public bool IsConditioningCompleted
        {
            get => _isConditioningCompleted;
            private set => SetProperty(ref _isConditioningCompleted, value);
        }

        private string? _xpAwardMessage;
        public string? XpAwardMessage
        {
            get => _xpAwardMessage;
            private set => SetProperty(ref _xpAwardMessage, value);
        }

        public ICommand ToggleStrengthCommand { get; }
        public ICommand ToggleMobilityCommand { get; }
        public ICommand ToggleConditioningCommand { get; }

        public QuestsViewModel(IQuestService quests)
        {
            _quests = quests;
            Title = AppResources.QuestPage_Title;

            ToggleStrengthCommand = new Command(async () => await ToggleQuestAsync(QuestKind.Strength));
            ToggleMobilityCommand = new Command(async () => await ToggleQuestAsync(QuestKind.Mobility));
            ToggleConditioningCommand = new Command(async () => await ToggleQuestAsync(QuestKind.Conditioning));
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;

                await _quests.InitializeAsync();
                _todayDate = DateOnly.FromDateTime(DateTime.Today);   // ✅ set once

                Today = await _quests.GetDailyQuestsAsync(_todayDate, ct);

                // Recovery panel
                IsRecoveryDay = IsRecovery(_todayDate);
                DayFocusDisplay = IsRecoveryDay
                    ? $"Recovery ({Today?.BodyFocus.ToString() ?? "FullBody"})"
                    : FocusToString(Today?.BodyFocus ?? BodyZone.FullBody);

                // Seed completion state
                IsStrengthCompleted = await _quests.IsQuestCompletedAsync(_todayDate, QuestKind.Strength, ct);
                IsMobilityCompleted = await _quests.IsQuestCompletedAsync(_todayDate, QuestKind.Mobility, ct);
                IsConditioningCompleted = await _quests.IsQuestCompletedAsync(_todayDate, QuestKind.Conditioning, ct);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static bool IsRecovery(DateOnly d) =>
            d.DayOfWeek is DayOfWeek.Wednesday or DayOfWeek.Saturday or DayOfWeek.Sunday;

        private async Task ToggleQuestAsync(QuestKind kind)
        {
            var isDone = await _quests.IsQuestCompletedAsync(_todayDate, kind);
            if (isDone)
                await _quests.UncompleteQuestAsync(_todayDate, kind);
            else
                await _quests.CompleteQuestAsync(_todayDate, kind);

            switch (kind)
            {
                case QuestKind.Strength:
                    IsStrengthCompleted = !isDone; break;
                case QuestKind.Mobility:
                    IsMobilityCompleted = !isDone; break;
                case QuestKind.Conditioning:
                    IsConditioningCompleted = !isDone; break;
            }

            var awarded = await _quests.TryAwardDailyCompletionXpAsync(_todayDate);
            if (awarded > 0)
            {
                XpAwardMessage = $"+{awarded} XP earned for completing all quests!";
            }
        }

        private static string FocusToString(BodyZone z) => z switch
        {
            BodyZone.Lower => "Lower body",
            BodyZone.Upper => "Upper body",
            BodyZone.Core => "Core",
            BodyZone.FullBody => "Full body",
            _ => z.ToString()
        };

    }
}
