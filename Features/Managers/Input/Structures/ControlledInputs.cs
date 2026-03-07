using Godot;

namespace Jogo25D.Systems
{
    [GlobalClass]
    public partial class ControlledInputs : RefCounted
    {
        [Export] public float MoveX { get; set; }
        [Export] public float MoveY { get; set; }
        [Export] public bool Jump { get; set; }
        [Export] public bool Dash { get; set; }
        [Export] public bool Attack { get; set; }
        [Export] public bool Reload { get; set; }
        [Export] public bool Ability { get; set; }
        [Export] public bool Ability2Held { get; set; }
        [Export] public bool Ability2JustReleased { get; set; }
        [Export] public bool ScrollNext { get; set; }
        [Export] public bool ScrollPrev { get; set; }
        [Export] public Vector2 MousePosition { get; set; }
    }
}