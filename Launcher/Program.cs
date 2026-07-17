using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE80;

namespace Launcher;

public class Program
{
    public static void Main(string[] args)
    {
        var quantidadeInstancias = 1;
        var processos = new List<Process>();
        var executionPath = AppContext.BaseDirectory;
        var searchDir = Directory.GetParent(executionPath);
        var projectRoot = (DirectoryInfo)null;

        while (searchDir != null)
        {
            var candidate = Path.Combine(searchDir.FullName, "Game", "project.godot");

            if (File.Exists(candidate))
            {
                projectRoot = new DirectoryInfo(Path.Combine(searchDir.FullName, "Game"));
                break;
            }

            searchDir = searchDir.Parent;
        }

        if (projectRoot == null)
        {
            return;
        }

        var projectPath = projectRoot.FullName;
        var godotPath = @"C:\Tools\Godot\godot_console.exe";
        var godotExe = File.Exists(godotPath) ? godotPath : "godot_console.exe";
        var dte = Debugger.IsAttached ? FindDebuggingDte() : null;

        try
        {
            for (int i = 0; i < quantidadeInstancias; i++)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = godotExe,
                    Arguments = $"--path \"{projectPath}\" --maximized",
                    WorkingDirectory = projectPath,
                    UseShellExecute = false
                };

                var p = Process.Start(startInfo);

                if (p == null)
                    continue;

                processos.Add(p);

                if (dte != null)
                    TryAttachDebugger(dte, p.Id);
            }

            while (processos.All(p => !p.HasExited))
            {
                System.Threading.Thread.Sleep(200);
            }
        }
        finally
        {
            foreach (var p in processos)
            {
                try
                {
                    if (!p.HasExited)
                        p.Kill(true);
                }
                catch
                {
                }
            }
        }
    }

    #region Core - Attach automático de debugger

    private static void TryAttachDebugger(DTE2 dte, int processId)
    {
        try
        {
            foreach (EnvDTE.Process proc in dte.Debugger.LocalProcesses)
            {
                if (proc.ProcessID == processId)
                {
                    proc.Attach();
                    return;
                }
            }
        }
        catch
        {
            // Attach é best-effort; se falhar, a instância continua
            // rodando normalmente, só sem debug.
        }
    }

    private static DTE2 FindDebuggingDte()
    {
        var currentProcessId = Environment.ProcessId;

        foreach (var dte in EnumerateRunningDtes())
        {
            try
            {
                foreach (EnvDTE.Process proc in dte.Debugger.DebuggedProcesses)
                {
                    if (proc.ProcessID == currentProcessId)
                        return dte;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<DTE2> EnumerateRunningDtes()
    {
        var results = new List<DTE2>();

        if (GetRunningObjectTable(0, out var rot) != 0 || rot == null)
            return results;

        rot.EnumRunning(out var enumMoniker);
        enumMoniker.Reset();

        var moniker = new IMoniker[1];

        while (enumMoniker.Next(1, moniker, IntPtr.Zero) == 0)
        {
            if (CreateBindCtx(0, out var bindCtx) != 0)
                continue;

            string displayName;

            try
            {
                moniker[0].GetDisplayName(bindCtx, null, out displayName);
            }
            catch
            {
                continue;
            }

            if (!displayName.StartsWith("!VisualStudio.DTE"))
                continue;

            if (rot.GetObject(moniker[0], out var obj) == 0 && obj is DTE2 dte)
            {
                results.Add(dte);
            }
        }

        return results;
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

    #endregion
}
