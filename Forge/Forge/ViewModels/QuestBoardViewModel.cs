using System.Collections.ObjectModel;
using System.Windows.Input;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.ViewModels
{
    /// <summary>A notice pinned to the quest board: one quest (all its versions), or a rumour of one.</summary>
    public sealed class QuestNotice
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Discipline { get; init; }
        public required string Meta { get; init; }
        public string? Description { get; init; }
        public string? Versions { get; init; }
        public string? Needs { get; init; }
        public string Tier { get; init; } = "";
        public bool IsActive { get; init; }
        public bool IsRumour { get; init; }
        public string? ActiveText { get; init; }
        /// <summary>A slight tilt so the notices look hand-pinned, not laid out by a grid.</summary>
        public double Tilt { get; init; }

        public bool CanAccept => !IsRumour;
        public string ButtonText => IsActive ? "Switch version" : "Accept quest";
        public bool HasDescription => Description is not null;
        public bool HasVersions => Versions is not null;
        public bool HasNeeds => Needs is not null;
        public bool HasActiveText => ActiveText is not null;
    }

    /// <summary>
    /// The quest board, on the Quest tab below the quest underway. Each quest (e.g. Copper Helm · Hearth) is one notice; accepting it
    /// asks which version — implement × schedule — pre-sorted by what's in My Gear.
    /// </summary>
    public sealed class QuestBoardViewModel : BaseViewModel
    {
        private static readonly double[] Tilts = { -1.4, 0.9, -0.6, 1.2, -1.0 };

        private readonly IProgramService _programs;
        private readonly ITrainingService _training;
        private IReadOnlyList<ProgramDefinition> _catalog = Array.Empty<ProgramDefinition>();
        private Equipment _homeGear;

        public ObservableCollection<QuestNotice> Notices { get; } = new();
        public ICommand AcceptCommand { get; }
        public event EventHandler? QuestAccepted;

        public QuestBoardViewModel(IProgramService programs, ITrainingService training)
        {
            _programs = programs;
            _training = training;
            Title = "Quest Board";
            AcceptCommand = new Command<string>(async id => await AcceptAsync(id));
        }

        public async Task LoadAsync()
        {
            var view = await _programs.GetViewAsync(DateOnly.FromDateTime(DateTime.Today));
            var active = view?.Program.Definition;
            _catalog = await _programs.GetProgramsAsync();
            _homeGear = await _training.GetEquipmentAsync(TrainingLocation.Home);

            Notices.Clear();
            var i = 0;
            // One notice per quest; the active quest is pinned first.
            foreach (var quest in _catalog.GroupBy(p => p.Quest).OrderByDescending(g => g.Key == active?.Quest))
            {
                var first = quest.First();
                var isActive = quest.Key == active?.Quest;
                var implements = quest.Select(p => p.Implement).Distinct().ToList();
                var days = quest.Select(p => p.TrainingDaysPerWeek).Distinct().OrderBy(d => d).ToList();
                var fits = quest.Any(p => Fits(p, _homeGear));

                Notices.Add(new QuestNotice
                {
                    Id = quest.Key,
                    Title = first.QuestName,
                    Discipline = $"{first.Discipline} · {first.Goal}",
                    Meta = $"{string.Join(" or ", days)} days a week · {first.Minutes ?? "?"} min",
                    Versions = "Versions: " + string.Join(" · ", implements),
                    Description = Describe(first),
                    Needs = fits ? null : "Needs gear you haven't marked in My Gear (the Gym has everything)",
                    Tier = $"{first.Metal} tier · forges the {first.QuestName}",
                    IsActive = isActive,
                    ActiveText = isActive && view is not null
                        ? view.BlockComplete
                            ? $"Completed · {active!.VersionName}"
                            : $"Underway · {active!.VersionName} · week {view.Week}: {ProgramDefinition.WeekName(view.Week)}"
                        : null,
                    Tilt = Tilts[i++ % Tilts.Length]
                });
            }

            // Rumours: quests planned in this tier but not written yet.
            foreach (var (title, discipline) in new[] { ("Copper Hammer", "Anvil · Strength"), ("Copper Boots", "Bellows · Conditioning") })
            {
                Notices.Add(new QuestNotice
                {
                    Id = "",
                    Title = title,
                    Discipline = discipline,
                    Meta = "Rumoured",
                    Description = "Word in the guild is this quest is coming. Check back.",
                    IsRumour = true,
                    Tilt = Tilts[i++ % Tilts.Length]
                });
            }
        }

        private static string Describe(ProgramDefinition p) => p.Discipline switch
        {
            "Hearth" => "A balanced block: strength and muscle together, with movement every day. Four weeks — Kindling, Tempering, Quenching, Honing.",
            "Anvil" => "Heavy, hard, repeated blows. A strength block.",
            _ => "Breath and work capacity. A conditioning block."
        };

        /// <summary>True when every piece of gear the version is written for is owned at Home.</summary>
        private static bool Fits(ProgramDefinition p, Equipment owned)
            => p.Equipment.All(name => !Enum.TryParse<Equipment>(name, ignoreCase: true, out var e) || owned.HasFlag(e));

        private async Task AcceptAsync(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            var versions = _catalog.Where(p => p.Quest == questId)
                .OrderByDescending(p => Fits(p, _homeGear))       // what your gear can do first
                .ThenBy(p => p.Implement).ThenByDescending(p => p.TrainingDaysPerWeek)
                .ToList();
            if (versions.Count == 0) return;

            var labels = versions.Select(p => p.VersionName + (Fits(p, _homeGear) ? "  ✓ fits your gear" : "")).ToArray();
            var pick = await Shell.Current.DisplayActionSheet($"{versions[0].QuestName} — choose your version", "Cancel", null, labels);
            var index = Array.IndexOf(labels, pick);
            if (index < 0) return;
            var chosen = versions[index];

            var current = (await _programs.GetActiveAsync())?.Definition;
            if (current?.Id == chosen.Id)
            {
                await Shell.Current.DisplayAlert("Already underway", $"You're already on {chosen.QuestName} · {chosen.VersionName}.", "OK");
                return;
            }

            var ok = await Shell.Current.DisplayAlert("Accept this quest?",
                $"Start {chosen.QuestName} · {chosen.VersionName} at week 1 (Kindling), today. Your current block ends; your workout history is kept.",
                "Accept", "Not yet");
            if (!ok) return;

            await _programs.StartProgramAsync(chosen.Id, DateOnly.FromDateTime(DateTime.Today));
            await LoadAsync();
            QuestAccepted?.Invoke(this, EventArgs.Empty);
        }
    }
}
