using System;
using System.Diagnostics;
using System.IO;

// taken from Mono.Cecil
class ShellService
{

    public class ProcessOutput
    {

        public int ExitCode;
        public string StdOut;
        public string StdErr;

        public ProcessOutput(int exitCode, string stdout, string stderr)
        {
            ExitCode = exitCode;
            StdOut = stdout;
            StdErr = stderr;
        }

        public override string ToString()
        {
            return StdOut + StdErr;
        }
    }

    static ProcessOutput RunProcess(string target, params string[] arguments)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = target,
                Arguments = string.Join(" ", arguments),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            },
        };

        process.Start();

        process.OutputDataReceived += (_, args) => stdout.Write(args.Data);
        process.ErrorDataReceived += (_, args) => stderr.Write(args.Data);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        return new ProcessOutput(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    static string Quote(string file)
    {
        return "\"" + file + "\"";
    }

    public static ProcessOutput PEVerify(string source)
    {
        return RunProcess(WinSdkTool("peverify"), "/nologo /verbose", Quote(source));
    }

    public static ProcessOutput PEDump(string source)
    {
        return RunProcess("pedump", "--verify code,metadata", Quote(source));
    }

    static string NetFrameworkTool(string tool)
    {
#if NET_CORE
			return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", tool + ".exe");
#else
        return Path.Combine(
            Path.GetDirectoryName(typeof(object).Assembly.Location),
            tool + ".exe");
#endif
    }

    static string WinSdkTool(string tool)
    {
        var sdks = new[] {
                @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools",
                @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7 Tools",
                @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.2 Tools",
                @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools",
                @"Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools",
                @"Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools",
                @"Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools",
                @"Microsoft SDKs\Windows\v7.0A\Bin",
            };

        foreach (var sdk in sdks)
        {
            var pgf = IntPtr.Size == 8
                ? Environment.GetEnvironmentVariable("ProgramFiles(x86)")
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var exe = Path.Combine(
                Path.Combine(pgf, sdk),
                tool + ".exe");

            if (File.Exists(exe))
                return exe;
        }

        return tool;
    }
}