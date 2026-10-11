using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

/// <summary>
/// Themed radio button. Screens pick one option; this control has no domain rules.
/// </summary>
public sealed class AppRadio : RadioButton
{
    public AppRadio()
    {
        Loaded += (_, _) => ThemeLooks.Apply(this, "AppRadio");
    }
}
