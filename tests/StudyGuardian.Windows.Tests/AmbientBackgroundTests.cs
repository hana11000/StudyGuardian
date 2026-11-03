using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App.Controls;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public sealed class AmbientBackgroundTests
{
    [TestMethod]
    public Task AnimationStopIsObservableByDependentUiWithoutAnotherInteraction() => ExitWorkflowTests.RunUi(() =>
    {
        var ambient=Create(out var host);using var source=new TestPresentationSource(host);
        ambient.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var indicator=new Border();indicator.SetBinding(FrameworkElement.TagProperty,new Binding(nameof(AmbientBackground.IsAnimating)) {Source=ambient});
        Assert.AreEqual(ambient.IsAnimating,indicator.Tag);
        ambient.MotionEnabled=false;
        Assert.AreEqual(false,indicator.Tag,"Dependent halo must stop when the ambient lifecycle changes.");
        ambient.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task SelectedPillHasAnOffsetKeyboardFocusOutlineInBothThemes() => ExitWorkflowTests.RunUi(() =>
    {
        foreach(var dark in new[]{false,true})
        {
            _=Create(out var resources);
            if(dark) resources.Resources.MergedDictionaries[1]=Theme("Dark");
            var pill=new RadioButton {Content="50",Width=100,Height=40,IsChecked=true,Style=(Style)resources.FindResource("M8.PillChoice")};
            var host=new Border {Resources=resources.Resources,Padding=new Thickness(8),Background=(Brush)resources.FindResource("M8.Background"),Child=pill};
            host.Measure(new Size(116,56));host.Arrange(new Rect(0,0,116,56));host.UpdateLayout();
            var before=Pixels(host);
            var key=(DependencyPropertyKey)typeof(UIElement).GetField("IsKeyboardFocusedPropertyKey",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
            pill.SetValue(key,true);host.UpdateLayout();var after=Pixels(host);
            var changedOutside=false;
            for(var y=0;y<56;y++)for(var x=0;x<116;x++)
                if((x<8 || x>=108 || y<8 || y>=48) && before[(y*116+x)*4]!=after[(y*116+x)*4])changedOutside=true;
            Assert.IsTrue(changedOutside,"Selected pill needs a focus outline separated from its fill.");
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task SelectedInputTextRetainsReadableContrastInBothThemes() => ExitWorkflowTests.RunUi(() =>
    {
        foreach(var dark in new[]{false,true})
        foreach(var name in new[]{"M8.Input","M8.GoalInput"})
        {
            _=Create(out var root);if(dark)root.Resources.MergedDictionaries[1]=Theme("Dark");
            var input=new TextBox {Style=(Style)root.FindResource(name)};((StackPanel)root).Children.Add(input);
            var canvas=((SolidColorBrush)root.FindResource("M8.Background")).Color;
            var selection=((SolidColorBrush)input.SelectionBrush).Color;
            var text=((SolidColorBrush)input.SelectionTextBrush).Color;
            var alpha=input.SelectionOpacity*selection.A/255;
            var composite=Color.FromRgb((byte)(selection.R*alpha+canvas.R*(1-alpha)),(byte)(selection.G*alpha+canvas.G*(1-alpha)),(byte)(selection.B*alpha+canvas.B*(1-alpha)));
            var a=Luminance(composite);var b=Luminance(text);
            Assert.IsTrue((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)>=4.5,"Selected text must remain readable.");
        }
        return Task.CompletedTask;
    });

    private static ResourceDictionary Theme(string name)=>new() {Source=new Uri($"pack://application:,,,/StudyGuardian.App;component/Themes/{name}.xaml")};
    private static byte[] Pixels(Visual visual)
    {
        var bitmap=new RenderTargetBitmap(116,56,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var bytes=new byte[116*56*4];bitmap.CopyPixels(bytes,116*4,0);return bytes;
    }
    private static double Luminance(Color c)
    {
        static double Linear(byte value) {var x=value/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
        return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);
    }
    [TestMethod]
    public Task MotionStopsOnDisableHideAndUnloadThenCanRestart() => ExitWorkflowTests.RunUi(() =>
    {
        var ambient=Create(out var host);
        using var source=new TestPresentationSource(host);
        ambient.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreEqual(SystemParameters.ClientAreaAnimation,ambient.IsAnimating);
        ambient.MotionEnabled=false; Assert.IsFalse(ambient.IsAnimating);
        ambient.MotionEnabled=true; Assert.AreEqual(SystemParameters.ClientAreaAnimation,ambient.IsAnimating);
        ambient.Visibility=Visibility.Hidden; Assert.IsFalse(ambient.IsAnimating);
        ambient.Visibility=Visibility.Visible; Assert.AreEqual(SystemParameters.ClientAreaAnimation,ambient.IsAnimating);
        ambient.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Assert.IsFalse(ambient.IsAnimating);
        ambient.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));Assert.AreEqual(SystemParameters.ClientAreaAnimation,ambient.IsAnimating);
        ambient.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ThemeSwapUpdatesExistingControlsWithoutChangingGlobalTheme() => ExitWorkflowTests.RunUi(() =>
    {
        var legacy=Application.Current.FindResource("BackgroundBrush");
        var ambient=Create(out var host); ambient.MotionEnabled=false;ambient.State=AmbientState.Block;
        var button=new Button {Style=(Style)host.FindResource("M8.PrimaryButton")};
        ((StackPanel)host).Children.Add(button);host.Measure(new Size(600,500));host.Arrange(new Rect(0,0,600,500));host.UpdateLayout();
        var light=((SolidColorBrush)button.Background).Color;
        host.Resources.MergedDictionaries[1]=new ResourceDictionary {Source=new Uri("pack://application:,,,/StudyGuardian.App;component/Themes/Dark.xaml")};
        ambient.IsDark=true;host.UpdateLayout();
        Assert.AreNotEqual(light,((SolidColorBrush)button.Background).Color);
        Assert.AreEqual(AmbientState.Block,ambient.State);
        Assert.AreSame(legacy,Application.Current.FindResource("BackgroundBrush"));
        Assert.IsFalse(ambient.IsAnimating);
        return Task.CompletedTask;
    });

    private static AmbientBackground Create(out FrameworkElement host)
    {
        var root=new StackPanel();host=root;
        foreach(var name in new[]{"Tokens","Light","Controls"}) root.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri($"pack://application:,,,/StudyGuardian.App;component/Themes/{name}.xaml")});
        var ambient=new AmbientBackground {Width=600,Height=400};root.Children.Add(ambient);
        root.Measure(new Size(600,500));root.Arrange(new Rect(0,0,600,500));root.UpdateLayout();return ambient;
    }

    // A managed presentation root makes IsVisible realistic without creating a native window.
    private sealed class TestPresentationSource : PresentationSource, IDisposable
    {
        private Visual? _root;
        private bool _disposed;
        public TestPresentationSource(Visual root) { AddSource(); RootVisual=root; }
        public override Visual RootVisual
        {
            get=>_root!;
            set {var previous=_root;_root=value;RootChanged(previous,value);}
        }
        public override bool IsDisposed=>_disposed;
        protected override CompositionTarget? GetCompositionTargetCore()=>null;
        public void Dispose() {RootVisual=null!;RemoveSource();_disposed=true;}
    }
}
