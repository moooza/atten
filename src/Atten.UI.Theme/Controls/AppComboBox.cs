using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

public sealed class AppComboBox : ComboBox
{
    public AppComboBox()
    {
        Loaded += (_, _) => ThemeLooks.Apply(this, "AppComboBoxStyle");
    }
}
