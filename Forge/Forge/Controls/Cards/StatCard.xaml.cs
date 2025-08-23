namespace Forge.Controls.Cards;

public partial class StatCard : ContentView
{
    public StatCard()
    {
        InitializeComponent();
    }

    // Title
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(StatCard), "Quick Stats");
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // Strength
    public static readonly BindableProperty StrengthProperty =
        BindableProperty.Create(nameof(Strength), typeof(int), typeof(StatCard), 10);
    public int Strength
    {
        get => (int)GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    // Dexterity
    public static readonly BindableProperty DexterityProperty =
        BindableProperty.Create(nameof(Dexterity), typeof(int), typeof(StatCard), 10);
    public int Dexterity
    {
        get => (int)GetValue(DexterityProperty);
        set => SetValue(DexterityProperty, value);
    }

    // Constitution
    public static readonly BindableProperty ConstitutionProperty =
        BindableProperty.Create(nameof(Constitution), typeof(int), typeof(StatCard), 10);
    public int Constitution
    {
        get => (int)GetValue(ConstitutionProperty);
        set => SetValue(ConstitutionProperty, value);
    }
}