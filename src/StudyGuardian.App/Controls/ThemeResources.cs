using System.Windows;

namespace StudyGuardian.App.Controls;

public sealed class ThemeResources : DependencyObject
{
    private ResourceDictionary? _palette;
    public static ThemeResources Current { get; } = new();
    private static readonly DependencyPropertyKey IsDarkPropertyKey=DependencyProperty.RegisterReadOnly(nameof(IsDark),typeof(bool),typeof(ThemeResources),new PropertyMetadata(false));
    public static readonly DependencyProperty IsDarkProperty=IsDarkPropertyKey.DependencyProperty;
    public bool IsDark => (bool)GetValue(IsDarkProperty);

    public void SetDark(bool dark)
    {
        VerifyAccess();
        var palettes=Application.Current.Resources.MergedDictionaries;
        var next=new ResourceDictionary {Source=new Uri($"pack://application:,,,/StudyGuardian.App;component/Themes/{(dark?"Dark":"Light")}.xaml")};
        // Replace a top-level palette so WPF invalidates existing window resources.
        if(_palette is not null && palettes.IndexOf(_palette) is var index && index>=0)palettes[index]=next;
        else palettes.Add(next);
        _palette=next;
        SetValue(IsDarkPropertyKey,dark);
    }
}
