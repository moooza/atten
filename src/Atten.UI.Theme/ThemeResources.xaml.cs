using Microsoft.UI.Xaml;

namespace Atten.UI.Theme;

public sealed partial class ThemeResources : ResourceDictionary
{
    public ThemeResources()
    {
        Theme.RegisterIconFont();
        InitializeComponent();
        ThemeDictionaries["Light"] = new Light();
        ThemeDictionaries["Dark"] = new Dark();
    }
}
