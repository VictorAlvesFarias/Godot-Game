using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Jogo25D;

public class Program
{
    public static void Main(string[] args)
    {
        var quantidadeInstancias = 3;
        var processos = new List<Process>();

        var executionPath = AppContext.BaseDirectory;
        var projectRoot = Directory.GetParent(executionPath);

        while (projectRoot != null && !projectRoot.GetFiles("project.godot").Any())
        {
            projectRoot = projectRoot.Parent;
        }

        if (projectRoot == null)
            return;

        var projectPath = projectRoot.FullName;

        var godotPath = @"C:\Tools\Godot\godot_console.exe";
        var godotExe = File.Exists(godotPath)
            ? godotPath
            : "godot_console.exe";

        try
        {
            for (int i = 0; i < quantidadeInstancias; i++)
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = godotExe,
                    Arguments = $"--path \"{projectPath}\"",
                    WorkingDirectory = projectPath,
                    UseShellExecute = false
                });

                if (p != null)
                    processos.Add(p);
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
}