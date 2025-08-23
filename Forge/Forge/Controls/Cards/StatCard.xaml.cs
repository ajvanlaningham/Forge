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
            propertyName: nameof(ViewModel),
            returnType: typeof(StatCardViewModel),
            declaringType: typeof(StatCard),
            defaultValueCreator: bindable => new StatCardViewModel(), // <-- never null
            propertyChanged: OnViewModelChanged);

    public StatCardViewModel ViewModel
    {
        get => (StatCardViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (StatCard)bindable;
        control.BindingContext = newValue; 
    }
}
