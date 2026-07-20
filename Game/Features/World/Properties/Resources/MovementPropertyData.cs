
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

        // Sem valor base nao-zero aqui de proposito: essa classe e usada
        // tanto pro "base" do player (Data.Properties, com Speed/JumpVelocity
        // explicitos) quanto pra bonus parciais (skill tree, ex: so Speed).
        // Resolver.Resolve soma TODOS os campos de TODA entrada agregada; se
        // o default fosse -750 aqui, cada bonus que so queria mexer em Speed
        // ia somar mais um -750 de JumpVelocity por baixo (pulo absurdamente
        // alto com skill tree investida).
        [Export, GodotDictionaryField]
        public float Speed { get; set; } = 0f;

        [Export, GodotDictionaryField]
        public float JumpVelocity { get; set; } = 0f;

    }
}
