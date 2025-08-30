using Forge.ViewModels.Controls.Cards;

namespace Forge.Controls.Cards;

public partial class WeeklyConditioningCard : ContentView
{
    public WeeklyConditioningCard()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty ViewModelProperty =
        BindableProperty.Create(
            propertyName: nameof(ViewModel),
            returnType: typeof(WeeklyConditioningCardViewModel),
            declaringType: typeof(WeeklyConditioningCard),
            defaultValueCreator: _ => new WeeklyConditioningCardViewModel(),
            propertyChanged: OnViewModelChanged);

    public WeeklyConditioningCardViewModel ViewModel
    {
        get => (WeeklyConditioningCardViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (WeeklyConditioningCard)bindable;
        control.BindingContext = newValue;
    }
}
