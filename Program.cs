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
        var quantidadeInstancias = 1; 
        var executionPath = AppContext.BaseDirectory;
        var projectRoot = Directory.GetParent(executionPath);

        while (projectRoot != null && !projectRoot.GetFiles("project.godot").Any())
        {
            projectRoot = projectRoot.Parent;
        }

        if (projectRoot == null)
        {
            return;
        }

        var projectPath = projectRoot.FullName;
        var godotPath = @"C:\Tools\Godot\godot_console.exe";
        var godotExe = File.Exists(godotPath) ? godotPath : "godot_console.exe";
        var fullCommand = $"/c \"\"{godotExe}\" --path \"{projectPath}\"\"";
        var processosAtivos = new List<Process>();

        try
        {
            Console.WriteLine($"[C#] Launcher Ativo. Abrindo {quantidadeInstancias} instâncias...");

            for (int i = 0; i < quantidadeInstancias; i++)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = fullCommand,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = projectPath
                };

                var p = Process.Start(startInfo);

                if (p != null)
                {
                    processosAtivos.Add(p);
                }

                Console.WriteLine($"[C#] Instância {i + 1} iniciada.");
            }

            Console.WriteLine("[C#] Monitorando instâncias. O console fechará quando todos os jogos fecharem.");

            while (processosAtivos.Any(p => !p.HasExited))
            {
                System.Threading.Thread.Sleep(500);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"\n[C#] ERRO: {e.Message}");
            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }
        finally
        {
            foreach (var p in processosAtivos)
            {
                if (p != null && !p.HasExited)
                {
                    p.Kill(true);
                }
            }
        }

        Environment.Exit(0);
    }
}