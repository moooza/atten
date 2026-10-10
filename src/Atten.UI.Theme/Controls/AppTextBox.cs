using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

public sealed class AppTextBox : TextBox
{
    public AppTextBox()
    {
        Loaded += (_, _) => ThemeLooks.Apply(this, "AppTextBoxStyle");
    }
}
