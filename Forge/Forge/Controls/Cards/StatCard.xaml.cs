using Forge.ViewModels.Controls.Cards;
namespace Forge.Controls.Cards;

public partial class StatCard : ContentView
{
    public StatCard()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty ViewModelProperty =
        BindableProperty.Create(
            nameof(ViewModel),
            typeof(StatCardViewModel),
            typeof(StatCard),
            defaultValue: null,
            propertyChanged: OnViewModelChanged);

    public StatCardViewModel? ViewModel
    {
        get => (StatCardViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (StatCard)bindable;
        control.BindingContext = newValue;
    }
}
