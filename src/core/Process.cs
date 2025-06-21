using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace SehensWerte.Utils
{
    public class Process
    {
        public static string AssemblyPath
        {
            get
            {
                return Path.GetDirectoryName(
                    Uri.UnescapeDataString(
                        new UriBuilder(Assembly.GetExecutingAssembly()?.Location ?? "")
                        .Path)) ?? "";
            }
        }

        public static string Platform
        {
            get
            {
                bool isMono = Type.GetType("Mono.Runtime") != null;
                bool isWine = RunningOnWine();
                string cpu = RuntimeInformation.OSArchitecture.ToString();
                return (isMono ? "Mono/" : isWine ? "Wine/" : "Windows/") + $"{Environment.OSVersion.VersionString}/{cpu}";
            }
        }

        private static bool RunningOnWine()
        {
            // Uses the wine registry entry. This may be unreliable?
            Microsoft.Win32.RegistryKey? wineKey = null;
            try
            {
                using (wineKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Wine")) { }
            }
            catch { }
            return wineKey != null;
        }


        public static int Run(string exeName, string parameters, string stdin, MemoryStream stdout)
        {
            return Run(exeName, parameters, "", Encoding.Default.GetBytes(stdin), stdout, new MemoryStream());
        }

        public static int Run(string exeName, string parameters, byte[] stdin, MemoryStream stdout)
        {
            return Run(exeName, parameters, "", stdin, stdout, new MemoryStream());
        }

        public static int Run(string exeName, string parameters, string workingfolder, byte[] stdin, MemoryStream stdout)
        {
            return Run(exeName, parameters, workingfolder, stdin, stdout, new MemoryStream());
        }

        public static int Run(string exeName, string parameters, string workingFolder, string stdin, out string stdout)
        {
            string stderr;
            return Run(exeName, parameters, workingFolder, stdin, out stdout, out stderr);
        }

        public static int Run(string exeName, string parameters, string workingFolder, string stdin, out string stdout, out string stderr, Dictionary<string, string>? environment = null, bool utf8 = false)
        {
            MemoryStream memoryStream = new MemoryStream();
            MemoryStream memoryStream2 = new MemoryStream();
            int result = Run(exeName, parameters, workingFolder, utf8 ? Encoding.UTF8.GetBytes(stdin) : Encoding.Default.GetBytes(stdin), memoryStream, memoryStream2, environment);
            stdout = utf8 ? Encoding.UTF8.GetString(memoryStream.ToArray()) : Encoding.ASCII.GetString(memoryStream.ToArray());
            stderr = utf8 ? Encoding.UTF8.GetString(memoryStream2.ToArray()) : Encoding.ASCII.GetString(memoryStream2.ToArray());
            return result;
        }

        public static int Run(string executableName, string parameters, string workingfolder, byte[] stdin, MemoryStream stdout, MemoryStream stderr, Dictionary<string, string>? environment = null)
        {
            if (!File.Exists(executableName))
            {
                executableName = FindExeInPath(executableName);
                if (!File.Exists(executableName))
                {
                    throw new FileNotFoundException();
                }
            }
            ProcessStartInfo info = new ProcessStartInfo(executableName, parameters);
            info.CreateNoWindow = true;
            info.ErrorDialog = false;
            info.RedirectStandardError = true;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.WorkingDirectory = workingfolder;
            if (environment != null)
            {
                foreach (var item in environment)
                {
                    info.EnvironmentVariables[item.Key] = item.Value;
                }
            }
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo = info;
            process.Start();

            Thread thread = new Thread(new ThreadStart(new OutputCapture(process, stdout, stderr).Run));
            thread.Start();

            if (!process.HasExited)
            {
                if (stdin != null)
                {
                    process.StandardInput.AutoFlush = true;
                    process.StandardInput.BaseStream.Write(stdin, 0, stdin.Length);
                    process.StandardInput.Close();
                }
                process.WaitForExit();
            }

            thread.Join();
            return process.ExitCode;
        }

        public static string FindExeInPath(string exe)
        {
            exe = Environment.ExpandEnvironmentVariables(exe);
            if (!File.Exists(exe))
            {
                if (Path.GetDirectoryName(exe) == "")
                {
                    string[] paths = (Environment.GetEnvironmentVariable("PATH") ?? "")!.Split(';');
                    foreach (var text in paths.Select(x => x.Trim()))
                    {
                        string full = Path.Combine(text, exe);
                        if (text != "" && File.Exists(full))
                        {
                            return Path.GetFullPath(full);
                        }
                    }
                }
                throw new FileNotFoundException(exe);
            }
            return Path.GetFullPath(exe);
        }

        public static void RunTestsInAssembly(string assemblyName)
        {
            var path = System.IO.Path.IsPathFullyQualified(assemblyName)
                ? assemblyName
                : System.IO.Path.Combine(Process.AssemblyPath, assemblyName);
            foreach (var t in Assembly.LoadFrom(path).GetTypes()
                .Where(t => t.CustomAttributes.Any(a => a.AttributeType.Name == "TestClassAttribute")))
            {
                var methods = t.GetMethods().Where(m =>
                    m.GetCustomAttributes().Any(a => a.GetType().Name == "TestMethodAttribute"));
                var testClassInstance = Activator.CreateInstance(t, null);
                foreach (var method in methods)
                {
                    var dataRows = method.GetCustomAttributes()
                        .Where(attr => attr.GetType().Name == "DataRowAttribute")
                        .ToList();
                    if (dataRows.Count > 0)
                    {
                        foreach (var row in dataRows)
                        {
                            var props = row.GetType().GetProperty("Data");
                            var args = (object[]?)props?.GetValue(row);
                            Debug.WriteLine($"Running test {method.Name}({string.Join(", ", args ?? Array.Empty<object>())})...");
                            method.Invoke(testClassInstance, args);
                            Debug.WriteLine($"Completed test {method.Name}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Running test {method.Name}...");
                        method.Invoke(testClassInstance, null);
                        Debug.WriteLine($"Completed test {method.Name}");
                    }
                }
            }
        }

        public static void RunAllTests()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    var location = assembly.Location;
                    string fileName = Path.GetFileNameWithoutExtension(location);
                    if (!string.IsNullOrEmpty(location)
                        && !(new[] { "System.", "Microsoft.", "Windows." })
                        .Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        RunTestsInAssembly(location);
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        public static int GetPhysicalCoreCount()
        {
            // not number of logical cores
            try
            {
                int coreCount = 0;
                var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    coreCount += Convert.ToInt32(obj["NumberOfCores"]);
                }
                return coreCount == 0 ? 1 : coreCount;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        public static long GetTotalPhysicalMemoryBytes()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                long totalCapacity = 0;
                foreach (var obj in searcher.Get())
                {
                    totalCapacity += Convert.ToInt64(obj["Capacity"]);
                }
                return totalCapacity;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        private class OutputCapture
        {
            private System.Diagnostics.Process m_Process;
            private MemoryStream m_StdOut;
            private MemoryStream m_StdErr;

            public OutputCapture(System.Diagnostics.Process process, MemoryStream stdout, MemoryStream stderr)
            {
                m_Process = process;
                m_StdOut = stdout;
                m_StdErr = stderr;
            }

            public void Run()
            {
                while (!m_Process.HasExited)
                {
                    Fetch();
                    Thread.Sleep(0);
                }
                Fetch();
            }

            private void Fetch()
            {
                int byteCount;
                while ((byteCount = m_Process.StandardOutput.BaseStream.ReadByte()) >= 0)
                {
                    if (m_StdOut != null)
                    {
                        m_StdOut.WriteByte((byte)byteCount);
                    }
                }
                while ((byteCount = m_Process.StandardError.BaseStream.ReadByte()) >= 0)
                {
                    if (m_StdErr != null)
                    {
                        m_StdErr.WriteByte((byte)byteCount);
                    }
                }
            }
        }
    }
}
