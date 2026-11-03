using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StudyGuardian.App.Controls;

namespace StudyGuardian.UiPreview;

public partial class PreviewApp : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length==2 && e.Args[0]=="--render")
        {
            RenderPreview(Path.GetFullPath(e.Args[1]));Shutdown();return;
        }
        MainWindow=new DemoWindow();MainWindow.Show();
    }

    private static void RenderPreview(string directory)
    {
        Directory.CreateDirectory(directory);
        var window=new DemoWindow();window.SetMotion(false);
        var content=(FrameworkElement)window.Content;
        // Render only our synthetic visual tree; no HWND, desktop capture, hooks or session.
        window.Content=null;
        foreach (var dictionary in window.Resources.MergedDictionaries) content.Resources.MergedDictionaries.Add(dictionary);
        content.SetResourceReference(System.Windows.Documents.TextElement.FontFamilyProperty,"M8.Font.Body");
        content.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty,"M8.Text");
        content.SetResourceReference(System.Windows.Documents.TextElement.FontSizeProperty,"M8.Type.Body");
        foreach (var dark in new[]{false,true})
        foreach (var state in Enum.GetValues<AmbientState>())
        {
            window.SetTheme(dark,content.Resources);window.SetState(state);
            Save(content,Path.Combine(directory,$"{(dark?"dark":"light")}-{state.ToString().ToLowerInvariant()}.png"),1184,864);
        }
        window.SetTheme(false,content.Resources);window.SetState(AmbientState.Focus);
        Save(content,Path.Combine(directory,"light-compact.png"),960,720);
        var font=(FontFamily)content.FindResource("M8.Font.Body");
        var faces=new[]{FontWeights.Normal,FontWeights.SemiBold}.Select(weight =>
        {
            var face=new Typeface(font,FontStyles.Normal,weight,FontStretches.Normal);
            if(!face.TryGetGlyphTypeface(out var glyph) || !glyph.FontUri.ToString().Contains("/Assets/Fonts/",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bundled MiSans did not resolve.");
            return new {weight=weight.ToString(),font=glyph.FontUri.ToString()};
        });
        File.WriteAllText(Path.Combine(directory,"render-info.json"),System.Text.Json.JsonSerializer.Serialize(new {desktopCaptured=false,fonts=faces}));
    }
    private static void Save(FrameworkElement content,string path,int width,int height)
    {
        content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(content);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output=File.Create(path);encoder.Save(output);
    }
}
