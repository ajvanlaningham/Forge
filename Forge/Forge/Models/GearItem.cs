using Forge.ViewModels;

namespace Forge.Models
{
    public sealed class GearItem : BaseViewModel
    {
        private bool _owned;

        public required Equipment Equipment { get; init; }
        public required string Name { get; init; }

        public bool Owned
        {
            get => _owned;
            set => SetProperty(ref _owned, value);
        }
    }
}