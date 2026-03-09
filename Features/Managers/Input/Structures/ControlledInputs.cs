using Godot;

namespace Jogo25D.Systems
{
    public class ControlledInputs
    {
        public float MoveX { get; set; }
        public float MoveY { get; set; }
        public bool Jump { get; set; }
        public bool Dash { get; set; }
        public bool Attack { get; set; }
        public bool Reload { get; set; }
        public bool Ability { get; set; }
        public bool Ability2Held { get; set; }
        public bool Ability2JustReleased { get; set; }
        public bool ScrollNext { get; set; }
        public bool ScrollPrev { get; set; }
        public Vector2 MousePosition { get; set; }
    }
}