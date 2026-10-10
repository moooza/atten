using Microsoft.UI.Xaml;

namespace Atten.UI.Theme;

public sealed partial class ThemeResources : ResourceDictionary
{
    public ThemeResources()
    {
        InitializeComponent();
        ThemeDictionaries["Light"] = new Light();
        ThemeDictionaries["Dark"] = new Dark();
    }
}
