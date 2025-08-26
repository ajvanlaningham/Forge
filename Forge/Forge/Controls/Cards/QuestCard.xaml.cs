using System.Windows.Input;

namespace Forge.Controls.Cards
{
    public partial class QuestCard : ContentView
    {
        public QuestCard()
        {
            InitializeComponent();
        }

        public static readonly BindableProperty IsCompletedProperty =
            BindableProperty.Create(nameof(IsCompleted), typeof(bool), typeof(QuestCard), false);

        public bool IsCompleted
        {
            get => (bool)GetValue(IsCompletedProperty);
            set => SetValue(IsCompletedProperty, value);
        }

        public static readonly BindableProperty ToggleCommandProperty =
            BindableProperty.Create(nameof(ToggleCommand), typeof(ICommand), typeof(QuestCard));

        public ICommand? ToggleCommand
        {
            get => (ICommand?)GetValue(ToggleCommandProperty);
            set => SetValue(ToggleCommandProperty, value);
        }
    }
}