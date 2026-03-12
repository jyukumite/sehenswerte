using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO.Pipes;
using System.Text;

namespace SehensWerte.Utils
{
    public class Process
    {
        public static string AssemblyPath =>
                Path.GetDirectoryName(
                    Uri.UnescapeDataString(
                        new UriBuilder(Assembly.GetExecutingAssembly()?.Location ?? "")
                        .Path)) ?? "";

        public static string ExePath => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

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

        private static string[] CommandLineArgs => Environment.GetCommandLineArgs()[1..];

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

        public class RunningProcess : IDisposable
        {
            public System.Diagnostics.Process Process;
            public StreamWriter Stdin;
            public StreamReader Stdout;
            public StreamReader Stderr;

            internal RunningProcess(System.Diagnostics.Process p)
            {
                Process = p;
                Stdin = p.StandardInput;
                Stdout = p.StandardOutput;
                Stderr = p.StandardError;
            }

            public void Dispose()
            {
                Stop();
                Process.Dispose();
            }

            public void Stop()
            {
                try { Stdin.Close(); } catch { }
                try { Process.Kill(); } catch { }
            }
        }

        public static RunningProcess Start(
            string executableName,
            string parameters,
            string workingFolder,
            Dictionary<string, string>? environment = null)
        {
            ProcessStartInfo info = GetProcessStartInfo(executableName, parameters, workingFolder, environment);
            var process = new System.Diagnostics.Process { StartInfo = info };
            process.Start();
            return new RunningProcess(process);
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
            ProcessStartInfo info = GetProcessStartInfo(executableName, parameters, workingfolder, environment);
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

        private static ProcessStartInfo GetProcessStartInfo(string executableName, string parameters, string workingfolder, Dictionary<string, string>? environment)
        {
            ProcessStartInfo info = new ProcessStartInfo(executableName, parameters)
            {
                CreateNoWindow = true,
                ErrorDialog = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = workingfolder
            };
            if (environment != null)
            {
                foreach (var item in environment)
                {
                    info.EnvironmentVariables[item.Key] = item.Value;
                }
            }

            return info;
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


        // static class Program
        // {
        //     [STAThread]
        //     static void Main(string[] argv)
        //     {
        //         Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        //         Application.EnableVisualStyles();
        //         Application.SetCompatibleTextRenderingDefault(false);
        //         if (Process.RunAsWorker()) return; // ******* used with Process.RunWorker

        // public static OutputObject StaticRunFunction(InputObject chunk)
        // {

        // public static void RunSomething(IEnumerable<string> input, Action<CsvLog.Entry> onLog)
        // {
        //     int chunk = 50;
        //     int processes = 8;
        //     var chunks =
        //         input
        //         .Select((o, index) => new { o, index })
        //         .GroupBy(x => x.index / chunk)
        //         .Select(g => g.Select(x => x.o).ToList())
        //         .ToList();
        // 
        //     chunks.ParallelForEach(o => run(o), threadCount: processes);
        //     void run(List<string> input)
        //     {
        //         try
        //         {
        //             var output = Process.RunWorker(new InputObject(input), StaticRunFunction);

        public static bool RunAsWorker()
        {
            var (args, _) = ParseCommandLine(new Dictionary<string, CommandLineEntry>
            {
                { "worker", new(HasValue: false) },
                { "class", new(HasValue: true) },
                { "method", new(HasValue: true) },
                { "in", new(HasValue: true) },
                { "out", new(HasValue: true) },
            });
            if (!args.ContainsKey("worker")) return false;

            try
            {
                args.TryGetValue("class", out string? className);
                args.TryGetValue("method", out string? methodName);
                args.TryGetValue("in", out string? inTypeName);
                args.TryGetValue("out", out string? outTypeName);

                if (className == null || methodName == null || inTypeName == null || outTypeName == null)
                {
                    return false;
                }

                Type? resolveType(string name)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType(name, throwOnError: false, ignoreCase: false);
                        if (t != null) return t;
                    }
                    return null;
                }

                Type tIn = resolveType(inTypeName) ?? throw new Exception($"Cannot resolve type '{inTypeName}'.");
                Type tOut = resolveType(outTypeName) ?? throw new Exception($"Cannot resolve type '{outTypeName}'.");

                Type targetClass = resolveType(className) ?? throw new Exception($"Cannot resolve class '{className}'.");

                MethodInfo? method = targetClass.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

                if (method == null)
                {
                    throw new MissingMethodException($"Method '{methodName}' not found in class '{className}'.");
                }

                var parms = method.GetParameters();
                if (parms.Length != 1 || parms[0].ParameterType != tIn)
                {
                    throw new Exception($"Method must have signature: {tOut.Name} {methodName}({tIn.Name} arg)");
                }

                object? instance = method.IsStatic ? null : Activator.CreateInstance(targetClass);

                string xml = Console.In.ReadToEnd();
                object? inputObj = typeof(StringExtensions)
                    .GetMethod(nameof(StringExtensions.FromXml))!
                    .MakeGenericMethod(tIn)
                    .Invoke(null, new object?[] { xml, null, null });

                object? result = method.Invoke(instance, new object?[] { inputObj });

                string outputXml = (string)typeof(StringExtensions)
                    .GetMethod(nameof(StringExtensions.ToXml))!
                    .MakeGenericMethod(tOut)
                    .Invoke(null, new object?[] { result!, null, false })!;

                Console.Out.Write(outputXml);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return true;
            }
        }

        public static TOut? RunWorker<TIn, TOut>(TIn input, Func<TIn, TOut> func)
        {
            if (func.Method.DeclaringType == null)
            {
                throw new Exception("Function must be a static method on a class.");
            }

            string exe = ExePath;
            string className = func.Method.DeclaringType.FullName!;
            string methodName = func.Method.Name;
            string inTypeName = typeof(TIn).FullName!;
            string outTypeName = typeof(TOut).FullName!;

            string xmlIn = input.ToXml();
            MemoryStream stdout = new MemoryStream();

            string parameters =
                $"--worker --class \"{className}\" --method \"{methodName}\" --in \"{inTypeName}\" --out \"{outTypeName}\"";

            int exitCode = Run(exe, parameters, xmlIn, stdout);

            if (exitCode != 0)
            {
                throw new Exception($"Worker process exited with {exitCode}.");
            }

            string xmlOut = Encoding.UTF8.GetString(stdout.ToArray());
            return xmlOut.FromXml<TOut>();
        }

        public static string PipeName => ExePath + "-" + Guid.NewGuid().ToString("N");

        public static string ReadPipe(string pipeName, int timeoutMs = 10000)
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            pipe.Connect(timeoutMs);
            return new StreamReader(pipe, Encoding.UTF8).ReadToEnd();
        }

        public static void WritePipe(string pipeName, string value)
        {
            using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1,
                PipeTransmissionMode.Byte, PipeOptions.None);
            pipe.WaitForConnection();
            var bytes = Encoding.UTF8.GetBytes(value);
            pipe.Write(bytes, 0, bytes.Length);
        }

        // Fork the current exe as a child process, passing args
        // as XML via a named pipe (--argpipe <name>). Blocks until the child exits.
        // pair with ReadForkArgs at startup
        public static void ForkWithPipe<TArgs>(TArgs args, string? exeName = null)
        {
            string pipeName = PipeName;
            string exePath = exeName ?? ExePath;
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--argpipe \"{pipeName}\"",
                UseShellExecute = false
            };
            var process = System.Diagnostics.Process.Start(startInfo);
            WritePipe(pipeName, args.ToXml());
            process?.WaitForExit();
        }

        // Read typed fork args from the process command line (--argpipe <name>).
        // Returns null if --argpipe is not present.
        public static TArgs? ReadForkArgs<TArgs>() where TArgs : new()
        {
            var (parsed, _) = ParseCommandLine(CommandLineArgs, new Dictionary<string, CommandLineEntry> { { "argpipe", new(HasValue: true) } });
            if (!parsed.TryGetValue("argpipe", out var pipeArg) || pipeArg == null)
            {
                return default;
            }
            try { return ReadPipe(pipeArg).FromXml<TArgs>(); }
            catch { return default; }
        }

        public record CommandLineEntry(bool HasValue, string? Default = null);

        // Parse command line arguments.
        // settings: maps arg name (without "--") to (HasValue, Default)
        //   HasValue: if true, the next token is consumed as the arg's value
        //   Default: used when the arg is absent (null = no default)
        // Returns: named args mapped to their value (or null for flags), and remainder (non-flag) args separately.
        public static (Dictionary<string, string?> Named, string[] Remainder) ParseCommandLine(Dictionary<string, CommandLineEntry> settings)
        {
            return ParseCommandLine(CommandLineArgs, settings);
        }

        public static (Dictionary<string, string?> Named, string[] Remainder) ParseCommandLine(
            string[] argv,
            Dictionary<string, CommandLineEntry> settings)
        {
            var named = new Dictionary<string, string?>();
            var remainder = new List<string>();

            foreach (var kvp in settings)
            {
                if (kvp.Value.Default != null)
                {
                    named[kvp.Key] = kvp.Value.Default;
                }
            }

            for (int loop = 0; loop < argv.Length;)
            {
                if (argv[loop].StartsWith("--"))
                {
                    string name = argv[loop].Substring(2);
                    loop++;
                    if (settings.TryGetValue(name, out var setting))
                    {
                        named[name] = setting.HasValue && loop < argv.Length ? argv[loop++] : null;
                    }
                }
                else
                {
                    remainder.Add(argv[loop]);
                    loop++;
                }
            }

            return (named, remainder.ToArray());
        }
    }
}
