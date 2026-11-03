using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App;
using StudyGuardian.App.Controls;
using StudyGuardian.Core;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public class ProductionThemeTests
{
    [TestMethod]
    public Task MinimizedWindowStopsDecorativeAmbientMotion() => ExitWorkflowTests.RunUi(() =>
    {
        var window=new Window {Style=(Style)Application.Current.FindResource(typeof(Window))};
        try
        {
            window.ApplyTemplate();
            AmbientBackground? Find(DependencyObject node)
            {
                if(node is AmbientBackground ambient)return ambient;
                for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(Find(VisualTreeHelper.GetChild(node,i)) is { } found)return found;
                return null;
            }
            var background=Find(window)!;Assert.IsNotNull(background);
            window.WindowState=WindowState.Minimized;
            Assert.IsFalse(background.MotionEnabled);
            window.WindowState=WindowState.Normal;
            Assert.IsTrue(background.MotionEnabled);
        }
        finally {window.Close();}
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task ThemeSwitchPreservesTypedGoalRulesAndChangesExistingInput() => ExitWorkflowTests.RunUi(async () =>
    {
        ThemeResources.Current.SetDark(false);
        Window? host=null;
        try
        {
            var root=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
            var json=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};
            var setup=new SessionSetupControl();
            host=new Window {Content=setup};
            setup.Initialize(root["session"]!.Deserialize<SessionUiOptions>(json)!,root["ai"]!.Deserialize<AiOptions>(json)!);
            var goal=(TextBox)setup.FindName("GoalBox");goal.Text="学习增值税";
            StudyRule[] rules=[new("synthetic-rule",RuleKind.Allow,RuleMatchType.Domain,"example.com")];
            setup.ApplyPolicy(new("增值税",rules,5));
            setup.Measure(new Size(640,800));setup.Arrange(new Rect(0,0,640,800));setup.UpdateLayout();
            var light=((SolidColorBrush)goal.Foreground).Color;
            ThemeResources.Current.SetDark(true);
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            setup.UpdateLayout();
            Assert.AreNotEqual(light,((SolidColorBrush)Application.Current.FindResource("M8.Text")).Color,"Application palette must change.");
            Assert.AreNotEqual(light,((SolidColorBrush)goal.Foreground).Color,"Existing controls must follow theme changes.");
            Assert.AreEqual(((SolidColorBrush)goal.Foreground).Color,((SolidColorBrush)Application.Current.FindResource("TextBrush")).Color,"Legacy presentation brushes must follow the palette too.");
            Assert.IsTrue(setup.TryGetOptions(out var options));
            Assert.AreEqual("学习增值税",options!.Goal);
            Assert.AreEqual("example.com",options.Rules.Single().Pattern);
            Assert.AreEqual("增值税",options.Policy!.GoalSummary);
        }
        finally {host?.Close();ThemeResources.Current.SetDark(false);}
    });
}
