using System.Collections.ObjectModel;
using System.Windows.Input;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.ViewModels
{
    /// <summary>A notice pinned to the quest board: one program, or a rumour of one to come.</summary>
    public sealed class QuestNotice
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Meta { get; init; }
        public string? Description { get; init; }
        public string? Gear { get; init; }
        public string Rank { get; init; } = "";
        public bool IsActive { get; init; }
        public bool IsRumour { get; init; }
        public string? ActiveText { get; init; }
        /// <summary>A slight tilt so the notices look hand-pinned, not laid out by a grid.</summary>
        public double Tilt { get; init; }

        public bool CanAccept => !IsActive && !IsRumour;
        public string ButtonText => IsActive ? "In progress" : "Accept quest";
        public bool HasDescription => Description is not null;
        public bool HasGear => Gear is not null;
        public bool HasActiveText => ActiveText is not null;
    }

    /// <summary>
    /// The guild's quest board: training programs posted as notices. Accepting one starts it as
    /// the active 4-week block.
    /// </summary>
    public sealed class QuestBoardViewModel : BaseViewModel
    {
        private static readonly double[] Tilts = { -1.4, 0.9, -0.6, 1.2, -1.0 };

        private readonly IProgramService _programs;

        public ObservableCollection<QuestNotice> Notices { get; } = new();

        public ICommand AcceptCommand { get; }

        public QuestBoardViewModel(IProgramService programs)
        {
            _programs = programs;
            Title = "Quest Board";
            AcceptCommand = new Command<string>(async id => await AcceptAsync(id));
        }

        public async Task LoadAsync()
        {
            var view = await _programs.GetViewAsync(DateOnly.FromDateTime(DateTime.Today));
            var activeId = view?.Program.Definition.Id;
            var programs = await _programs.GetProgramsAsync();

            Notices.Clear();
            var i = 0;
            // The active quest is pinned first.
            foreach (var p in programs.OrderByDescending(p => p.Id == activeId))
            {
                var active = p.Id == activeId;
                Notices.Add(new QuestNotice
                {
                    Id = p.Id,
                    Title = p.Name,
                    Meta = $"{p.TrainingDaysPerWeek} days a week · {p.Minutes ?? "?"} min · {p.Goal}",
                    Description = p.Description,
                    Gear = p.Equipment.Count == 0 ? null : "Gear: " + string.Join(", ", p.Equipment.Select(Humanize)),
                    Rank = Rank(p.Difficulty),
                    IsActive = active,
                    ActiveText = active && view is not null
                        ? view.BlockComplete ? "Completed — accept again to repeat" : $"Underway · week {view.Week} of {ProgramDefinition.WeeksPerBlock}"
                        : null,
                    Tilt = Tilts[i++ % Tilts.Length]
                });
            }

            // Rumours: the goal variants that are planned but not written yet.
            foreach (var (title, goal) in new[] { ("Block 1 · Strength", "Heavier, fewer reps"), ("Block 1 · Conditioning", "Circuits and work capacity") })
            {
                Notices.Add(new QuestNotice
                {
                    Id = "",
                    Title = title,
                    Meta = $"Rumoured · {goal}",
                    Description = "Word in the guild is this quest is coming. Check back.",
                    IsRumour = true,
                    Tilt = Tilts[i++ % Tilts.Length]
                });
            }
        }

        private async Task AcceptAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var notice = Notices.FirstOrDefault(n => n.Id == id);
            if (notice is null || notice.IsActive) return;

            var ok = await Shell.Current.DisplayAlert("Accept this quest?",
                $"Start \"{notice.Title}\" at week 1, today. Your current block ends; your workout history is kept.",
                "Accept", "Not yet");
            if (!ok) return;

            await _programs.StartProgramAsync(id, DateOnly.FromDateTime(DateTime.Today));
            await LoadAsync();
            await Shell.Current.GoToAsync("//train");
        }

        private static string Rank(int difficulty) => difficulty switch
        {
            <= 1 => "Rank: Novice",
            2 => "Rank: Journeyman",
            _ => "Rank: Veteran"
        };

        private static string Humanize(string equipment)
            => equipment switch
            {
                "boxStep" => "box",
                "pullUpBar" => "pull-up bar",
                "squatRack" => "rack",
                "cableMachine" => "cables",
                _ => equipment
            };
    }
}
