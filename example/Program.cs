namespace SehensWerte
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] argv)
        {
            // Headless test runner (works under Wine): `Use.exe runtest [classSubstr] [methodSubstr]`.
            // Runs matching MSTest [TestClass]/[TestMethod] methods via Utils.Process.RunTests, prints
            // PASS/FAIL per test, and exits 0 iff all matched tests passed. Results also go to
            // runtest-results.txt (cwd) since a WinExe's stdout is not always visible under Wine.
            if (argv.Length >= 1 && argv[0] == "runtest")
            {
                // Touch the assemblies under test so they are loaded before the scan.
                _ = typeof(SehensWerte.Controls.Sehens.TraceData).Assembly;
                _ = typeof(SehensWerte.Maths.Interpolate).Assembly;

                var lines = new List<string>();
                void Emit(string s) { lines.Add(s); Console.WriteLine(s); Console.Error.WriteLine(s); }

                var r = SehensWerte.Utils.Process.RunTests(
                    classFilter: argv.Length > 1 ? argv[1] : null,
                    methodFilter: argv.Length > 2 ? argv[2] : null,
                    report: Emit,
                    detail: lines.Add);

                Emit($"--- {r.Passed} passed, {r.Failed} failed, {r.Matched} matched ---");
                try { System.IO.File.WriteAllLines("runtest-results.txt", lines); } catch { }
                Environment.Exit((r.Failed == 0 && r.Matched > 0) ? 0 : 1);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TestForm(argv));
        }
    }
}
