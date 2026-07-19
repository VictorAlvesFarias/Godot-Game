using Godot;
using Jogo25D.Properties;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Features.World.Properties.Resources
{
    public partial class HealthPropertyData : BasePropertyData
    {
        [Export, GodotDictionaryField]
        public int MaxHealth { get; set; } = 50;
    }
}
