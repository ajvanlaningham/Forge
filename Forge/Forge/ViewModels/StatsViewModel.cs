using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using Forge.Contstants;
using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services.Interfaces;
using Forge.ViewModels.Controls.Cards;

namespace Forge.ViewModels
{
    public class StatsViewModel : BaseViewModel
    {
        private readonly IStatsService _stats;

        public StatsViewModel(IStatsService statsService)
        {
            _stats = statsService;

            Title = AppResources.StatsPage_Title;

            StatsCard = new StatCardViewModel
            {
                Title = AppResources.StatsPage_StatsCard,
                Level = 1,
                Xp = 0,
                Strength = 0,
                Dexterity = 0,
                Constitution = 0
            };

            RefreshCommand = new AsyncRelayCommand(InitializeAsync);
        }

        public async Task InitializeAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;

                await _stats.InitAsync();

                var coreStats = await _stats.GetCoreStatsAsyncFromDb();
                var userStats = await _stats.GetUserStatsAsync();

                UserLevel = userStats.Level;
                UserXp = userStats.Xp;

                StrengthScore = coreStats.FirstOrDefault(s => s.Kind == StatKind.Strength)?.Score ?? 1;
                DexterityScore = coreStats.FirstOrDefault(s => s.Kind == StatKind.Dexterity)?.Score ?? 1;
                ConstitutionScore = coreStats.FirstOrDefault(s => s.Kind == StatKind.Constitution)?.Score ?? 1;

                StatsCard.Level = UserLevel;
                StatsCard.Xp = UserXp;
                StatsCard.XpProgress = GameMath.LevelProgress(UserXp);
                StatsCard.Strength = StrengthScore;
                StatsCard.Dexterity = DexterityScore;
                StatsCard.Constitution = ConstitutionScore;
            }
            finally
            {
                IsBusy = false;
            }
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

        public ICommand RefreshCommand { get; }
    }
}
