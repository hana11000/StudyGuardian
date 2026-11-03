using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;
using StudyGuardian.Data;

namespace StudyGuardian.Core.Tests;

[TestClass]
public class UserSettingsStoreTests
{
    private string _directory=null!;
    [TestInitialize] public void SetUp() => _directory=Path.Combine(Path.GetTempPath(),"StudyGuardian.Tests",Guid.NewGuid().ToString("N"));
    [TestCleanup] public void CleanUp() {if(Directory.Exists(_directory)) Directory.Delete(_directory,true);}
    [TestMethod] public void SettingsPersistThresholdsGlobalRulesAndPrivacy()
    {
        var store=new UserSettingsStore(_directory,EscalationEngineTests.Options);
        store.Save(new(EscalationEngineTests.Options with {BlockSeconds=120},[new("a",RuleKind.Allow,RuleMatchType.Process,"Notion.exe")],new(false,true)));
        var loaded=new UserSettingsStore(_directory,EscalationEngineTests.Options);Assert.AreEqual(120,loaded.Current.Intervention.BlockSeconds);
        Assert.HasCount(1,loaded.Current.GlobalRules);Assert.IsFalse(loaded.Current.Privacy.SendWindowTitle);Assert.IsTrue(loaded.Current.Privacy.SendUrl);
    }
    [TestMethod] public void InvalidRulesDoNotReplaceSavedSettings()
    {
        var store=new UserSettingsStore(_directory,EscalationEngineTests.Options);store.Save(store.Current);
        Assert.ThrowsExactly<ArgumentException>(()=>store.Save(store.Current with {GlobalRules=[new("bad",RuleKind.Block,RuleMatchType.TitleRegex,"[")]}));
        Assert.HasCount(0,new UserSettingsStore(_directory,EscalationEngineTests.Options).Current.GlobalRules);
    }
    [TestMethod] public void CorruptSettingsFallBackWithAnError()
    {
        Directory.CreateDirectory(_directory);File.WriteAllText(Path.Combine(_directory,"preferences.json"),"broken");
        var store=new UserSettingsStore(_directory,EscalationEngineTests.Options);Assert.IsNotNull(store.LoadError);Assert.AreEqual(90,store.Current.Intervention.BlockSeconds);
    }
}
