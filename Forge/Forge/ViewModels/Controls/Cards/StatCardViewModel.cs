using Forge.Resources.Strings;

namespace Forge.ViewModels.Controls.Cards
{
    public class StatCardViewModel : BaseViewModel
    {
        private string _title = AppResources.Home_StatCard_Title;
        private int _strength;
        private int _dexterity;
        private int _constitution;

        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public int Strength { get => _strength; set => SetProperty(ref _strength, value); }
        public int Dexterity { get => _dexterity; set => SetProperty(ref _dexterity, value); }
        public int Constitution { get => _constitution; set => SetProperty(ref _constitution, value); }
    }
}
