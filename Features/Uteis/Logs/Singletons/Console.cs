using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Features.Uteis.Logs.Systems
{
    public static class Console
    {
        public static void Log(string value)
        {
            GD.Print($"{value}");
        }
    }
}
