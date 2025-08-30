using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services.Interfaces;
using System.Windows.Input;
using System.Diagnostics;
using Forge.ViewModels.Controls.Cards;

namespace Forge.ViewModels
{
    public class QuestsViewModel : BaseViewModel
    {
        private readonly IQuestService _quests;
        private readonly IConditioningWeekService _conditioning;

        private DateOnly _todayDate;

        public event EventHandler<int>? DailyXpAwarded;

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

        private WeeklyConditioningCardViewModel _weeklyCard = new();
        public WeeklyConditioningCardViewModel WeeklyCard
        {
            get => _weeklyCard;
            private set => SetProperty(ref _weeklyCard, value);
        }

        public QuestsViewModel(IQuestService quests, IConditioningWeekService conditioning)
        {
            _quests = quests;
            _conditioning = conditioning;

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

                Today = await _quests.GetDailyQuestsAsync(_todayDate, ct);
                var sw = Stopwatch.StartNew();

                _todayDate = DateOnly.FromDateTime(DateTime.Today);

                //placeholderfor init
                Today = new DailyQuests
                {
                    Date = _todayDate,
                    BodyFocus = BodyZone.FullBody,
                    Strength = new Quest { Kind = QuestKind.Strength, BodyFocus = BodyZone.FullBody, Title = AppResources.QuestPage_StrengthQuest_Title },
                    Mobility = new Quest { Kind = QuestKind.Mobility, BodyFocus = BodyZone.FullBody, Title = AppResources.QuestPage_MobilityQuest_Title },
                    Conditioning = new Quest { Kind = QuestKind.Conditioning, BodyFocus = BodyZone.FullBody, Title = AppResources.QuestPage_ConditioningQuest_Title }
                };

                // Kick off all reads
                var tDaily = _quests.GetDailyQuestsAsync(_todayDate, ct);
                var tS = _quests.IsQuestCompletedAsync(_todayDate, QuestKind.Strength, ct);
                var tM = _quests.IsQuestCompletedAsync(_todayDate, QuestKind.Mobility, ct);
                var tC = _quests.IsQuestCompletedAsync(_todayDate, QuestKind.Conditioning, ct);
                var tWeek = _conditioning.GetWeekProgressAsync(_todayDate, ct);
                
                await Task.WhenAll(tDaily, tS, tM, tC, tWeek);
                
                Today = tDaily.Result;

                // Recovery panel
                IsRecoveryDay = IsRecovery(_todayDate);
                var focusText = FocusToString(Today?.BodyFocus ?? BodyZone.FullBody);
                DayFocusDisplay = IsRecoveryDay
                    ? string.Format(AppResources.QuestPage_RecoveryHeader_Format, focusText) // "Recovery ({0})"
                    : FocusToString(Today?.BodyFocus ?? BodyZone.FullBody);

                // Seed completion state
                IsStrengthCompleted = tS.Result;
                IsMobilityCompleted = tM.Result;
                IsConditioningCompleted = tC.Result;

                // --- Initialize Weekly Conditioning Card ---
                var prog = tWeek.Result;
                WeeklyCard.Title = AppResources.QuestPage_ConditioningQuest_Title;
                WeeklyCard.Minutes = prog.Minutes;
                WeeklyCard.GoalMinutes = prog.GoalMinutes;
                WeeklyCard.WeekRangeText = $"{prog.WeekStart:MMM d} – {prog.WeekEnd:MMM d}";
                WeeklyCard.HintText = AppResources.QuestPage_ConditioningQuest_Hint;
                WeeklyCard.ShowHint = true;

                WeeklyCard.AddMinutesCommand = new Command<object>(async p =>
                {
                    if (p is null) return;
                    if (!int.TryParse(p.ToString(), out var m) || m <= 0) return;
                    var updated = await _conditioning.AddConditioningMinutesAsync(_todayDate, m, ct);
                    WeeklyCard.Minutes = updated.Minutes;
                    WeeklyCard.GoalMinutes = updated.GoalMinutes;
                    WeeklyCard.WeekRangeText = $"{updated.WeekStart:MMM d} – {updated.WeekEnd:MMM d}";
                    //TODO: Add toast notification when completed
                });
                Debug.WriteLine($"QuestsViewModel.InitializeAsync completed in {sw.ElapsedMilliseconds} ms");
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
            if (isDone) await _quests.UncompleteQuestAsync(_todayDate, kind);
            else await _quests.CompleteQuestAsync(_todayDate, kind);

            switch (kind)
            {
                case QuestKind.Strength: IsStrengthCompleted = !isDone; break;
                case QuestKind.Mobility: IsMobilityCompleted = !isDone; break;
                case QuestKind.Conditioning: IsConditioningCompleted = !isDone; break;
            }

            // If all three are now complete, award XP once
            var awarded = await _quests.TryAwardDailyCompletionXpAsync(_todayDate);
            if (awarded > 0)
            {
                XpAwardMessage = $"+{awarded} XP earned for completing all quests!";
                DailyXpAwarded?.Invoke(this, awarded); // notify page to redirect
            }
        }

        private static string FocusToString(BodyZone z) => z switch
        {
            BodyZone.Lower => AppResources.BodyZone_Lower,
            BodyZone.Upper => AppResources.BodyZone_Upper,
            BodyZone.Core => AppResources.BodyZone_Core,
            BodyZone.FullBody => AppResources.BodyZone_FullBody,
            _ => z.ToString()
        };
    }
}
