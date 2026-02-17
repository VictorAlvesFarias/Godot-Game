using Godot;

namespace Jogo25D.Systems
{
    public class InputControls
    {
        public float InputX { get; set; }
        public float InputY { get; set; }
        public bool InputJump { get; set; }
        public bool InputDash { get; set; }
        public bool InputAttack { get; set; }
        public Vector2 MousePosition { get; set; }
        public bool IsOwner { get; set; }
        public Vector2 InitialPosition { get; set; }

        public InputControls()
        {
            InputX = 0f;
            InputY = 0f;
            InputJump = false;
            InputDash = false;
            InputAttack = false;
            MousePosition = Vector2.Zero;
            IsOwner = false;
            InitialPosition = Vector2.Zero;
        }
    }
}
