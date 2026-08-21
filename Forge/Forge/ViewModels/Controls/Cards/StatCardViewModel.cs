using Forge.Constants;
using Forge.Resources.Strings;

namespace Forge.ViewModels.Controls.Cards
{
    public class StatCardViewModel : BaseViewModel
    {
        private string _title = AppResources.Home_StatCard_Title;
        private int _strength;
        private int _dexterity;
        private int _constitution;
        private int _level = 1;
        private int _xp = 0;
        private double _xpProgress; // 0..1
        private int _xpIntoLevel;
        private int _xpToNextLevel = GameMath.XpPerLevel;


        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public int Strength { get => _strength; set => SetProperty(ref _strength, value); }
        public int Dexterity { get => _dexterity; set => SetProperty(ref _dexterity, value); }
        public int Constitution { get => _constitution; set => SetProperty(ref _constitution, value); }
        public int Level { get => _level; set => SetProperty(ref _level, value); }
        public int Xp { get => _xp; set => SetProperty(ref _xp, value); }
        public double XpProgress { get => _xpProgress; set => SetProperty(ref _xpProgress, value); }

        /// <summary>XP earned since the current level began.</summary>
        public int XpIntoLevel
        {
            get => _xpIntoLevel;
            set { if (SetProperty(ref _xpIntoLevel, value)) OnPropertyChanged(nameof(XpLabel)); }
        }

        /// <summary>XP still needed to reach the next level.</summary>
        public int XpToNextLevel { get => _xpToNextLevel; set => SetProperty(ref _xpToNextLevel, value); }

        /// <summary>Progress within the current level, e.g. "450 / 1050 XP".</summary>
        public string XpLabel => $"{XpIntoLevel} / {GameMath.XpPerLevel} XP";

        /// <summary>
        /// Set every XP-derived value from a single lifetime XP total.
        /// </summary>
        /// <remarks>
        /// Deliberately the only place this maths happens. Home and Stats both show this card,
        /// and computing level and progress separately in each is how they drift apart.
        /// </remarks>
        public void ApplyXp(int lifetimeXp)
        {
            Xp = lifetimeXp;
            Level = GameMath.LevelFromXp(lifetimeXp);
            XpIntoLevel = GameMath.XpIntoLevel(lifetimeXp);
            XpToNextLevel = GameMath.XpToNextLevel(lifetimeXp);
            // Progress within THIS level. Passing the lifetime total pins the bar at 100%
            // forever once the first level is cleared.
            XpProgress = GameMath.LevelProgress(XpIntoLevel);
        }
    }
}
