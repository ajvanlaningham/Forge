using System.Collections.ObjectModel;

namespace Forge.Models
{
    public sealed class GearGroup : ObservableCollection<GearItem>
    {
        public string Title { get; }
        public GearGroup(string title)
        {
            Title = title;
        }
    }
}
