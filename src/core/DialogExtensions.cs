using System.Windows.Forms;

namespace SehensWerte.Utils
{
    // Overloads of the WinForms ShowDialog() that remember the last-used path per caller-supplied
    // key, so an existing ShowDialog() call site opts in by adding a key.
    public static class DialogExtensions
    {
        private static string ToKey(string saveKey)
        {
            return "Dialog " + saveKey;
        }

        private static string LoadPath(string saveKey)
        {
            string folder = WindowsRegistry.ReadOrDefault(ToKey(saveKey), "");
            return Directory.Exists(folder) ? folder : "";
        }

        private static void SavePath(string saveKey, string path)
        {
            if (Directory.Exists(path))
            {
                WindowsRegistry.Write(ToKey(saveKey), path);
            }
            else if (Path.GetDirectoryName(path) is string folder)
            {
                WindowsRegistry.Write(ToKey(saveKey), folder);
            }
        }

        public static DialogResult ShowDialog(this FileDialog dialog, string saveKey)
        {
            // Wine's dialog ignores ClientGuid and honours InitialDirectory/SelectedPath
            dialog.InitialDirectory = LoadPath(saveKey);
            dialog.ClientGuid = saveKey.ToGuid();
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                SavePath(saveKey, dialog.FileName);
            }
            return result;
        }

        public static DialogResult ShowDialog(this FolderBrowserDialog dialog, string saveKey)
        {
            // Wine's dialog ignores ClientGuid and honours InitialDirectory/SelectedPath
            dialog.SelectedPath = LoadPath(saveKey);
            dialog.ClientGuid = saveKey.ToGuid();
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                SavePath(saveKey, dialog.SelectedPath);
            }
            return result;
        }
    }
}
