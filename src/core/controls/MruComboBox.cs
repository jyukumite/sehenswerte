using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Utils;

namespace SehensWerte.Controls
{
    // Editable ComboBox whose drop-down list is a most-recently-used history
    // persisted to the registry (WindowsRegistry, REG_MULTI_SZ).
    // Set RegistryKey to load the history; call CommitMru() when the current
    // text has been "used" (dialog OK, Enter, focus leave, ...).
    public class MruComboBox : ComboBox
    {
        public int MaxEntries { get; set; } = 10;

        private string? m_RegistryKey;
        public string? RegistryKey
        {
            get => m_RegistryKey;
            set
            {
                m_RegistryKey = value;
                SetItems(LoadList());
            }
        }

        public MruComboBox()
        {
            DropDown += (o, e) => SetItems(LoadList()); // pick up entries committed by other instances
        }

        public void CommitMru()
        {
            string text = Text;
            if (m_RegistryKey != null && text.Length > 0)
            {
                List<string> list = Promote(LoadList(), text, MaxEntries);
                WindowsRegistry.Write(m_RegistryKey, list.ToArray());
                SetItems(list);
            }
        }

        internal static List<string> Promote(IEnumerable<string> existing, string entry, int maxEntries)
        {
            List<string> result = new List<string>() { entry };
            result.AddRange(existing.Where(x => x != entry));
            return result.Take(maxEntries).ToList();
        }

        private List<string> LoadList()
        {
            string[]? entries = null;
            if (m_RegistryKey != null)
            {
                WindowsRegistry.Read(m_RegistryKey, out entries);
            }
            return entries == null ? new List<string>() : entries.ToList();
        }

        private void SetItems(List<string> list)
        {
            string keep = Text;
            BeginUpdate();
            Items.Clear();
            Items.AddRange(list.ToArray());
            Text = keep;
            EndUpdate();
        }
    }

    [TestClass]
    public class MruComboBoxTest
    {
        [TestMethod]
        public void TestPromote()
        {
            CollectionAssert.AreEqual(
                new List<string>() { "a" },
                MruComboBox.Promote(new string[0], "a", 10));
            CollectionAssert.AreEqual(
                new List<string>() { "c", "a", "b" },
                MruComboBox.Promote(new[] { "a", "b" }, "c", 10));
            CollectionAssert.AreEqual(
                new List<string>() { "b", "a", "c" },
                MruComboBox.Promote(new[] { "a", "b", "c" }, "b", 10));
            CollectionAssert.AreEqual(
                new List<string>() { "d", "a", "b" },
                MruComboBox.Promote(new[] { "a", "b", "c" }, "d", 3));
            CollectionAssert.AreEqual(
                new List<string>() { "a", "b", "c" },
                MruComboBox.Promote(new[] { "a", "b", "c" }, "a", 3));
        }
    }
}
