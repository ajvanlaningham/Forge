namespace Forge.Views;

/// <summary>Shared top-bar furniture for the four tabs (Decision 20): Settings is a cog, not a tab.</summary>
public static class TabChrome
{
    public static void AddSettingsCog(Page page)
    {
        var cog = new ToolbarItem { Text = "Settings", Order = ToolbarItemOrder.Primary, Priority = 0 };
        if (Application.Current?.Resources.TryGetValue("IconCog", out var icon) == true && icon is ImageSource src)
            cog.IconImageSource = src;
        cog.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(SettingsPage));
        page.ToolbarItems.Add(cog);
    }
}
