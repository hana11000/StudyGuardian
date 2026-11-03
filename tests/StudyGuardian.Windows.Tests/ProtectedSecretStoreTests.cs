using System.ComponentModel;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Monitor;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public sealed class ProtectedSecretStoreTests
{
    private string _directory = null!;
    private string _path = null!;
    [TestInitialize] public void SetUp()
    { _directory = Path.Combine(Path.GetTempPath(), "StudyGuardian.Windows.Tests", Guid.NewGuid().ToString("N")); _path = Path.Combine(_directory, "test.dpapi"); }
    [TestCleanup] public void CleanUp() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [TestMethod]
    public void SecretRoundTripsWithoutPlaintextOnDiskAndCanBeReplaced()
    {
        var store = new ProtectedSecretStore(_path);
        const string value = "synthetic-test-key-测试";
        store.Save(value);
        Assert.AreEqual(value, new ProtectedSecretStore(_path).Read());
        Assert.IsFalse(Encoding.UTF8.GetString(File.ReadAllBytes(_path)).Contains(value, StringComparison.Ordinal));
        store.Save("synthetic-replacement"); Assert.AreEqual("synthetic-replacement", store.Read());
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }
    [TestMethod]
    public void MissingAndClearedKeyAreAbsent()
    {
        var store = new ProtectedSecretStore(_path); Assert.IsNull(store.Read());
        store.Save("synthetic-test-key"); store.Save(null);
        Assert.IsFalse(store.Exists); Assert.IsNull(store.Read());
    }
    [TestMethod]
    public void InvalidCiphertextCannotBecomePlaintextKey()
    {
        Directory.CreateDirectory(_directory); File.WriteAllText(_path, "not encrypted");
        Assert.ThrowsExactly<Win32Exception>(() => new ProtectedSecretStore(_path).Read());
    }
}
