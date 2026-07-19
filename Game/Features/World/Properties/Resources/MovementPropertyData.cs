
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
    public partial class MovementPropertyData : BasePropertyData
    {

        [Export, GodotDictionaryField]
        public float Speed { get; set; } = 300.0f;

        [Export, GodotDictionaryField]
        public float JumpVelocity { get; set; } = -750.0f;

    }
}
