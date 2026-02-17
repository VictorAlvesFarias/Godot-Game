using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Scripts.Actions
{
    public abstract class PlayerAction
    {
        [Export] public bool CanUse { get; set; } = true;
        [Export] public bool Cooldown { get; set; } = false;
        [Export] public bool IsActive { get; set; } = false;
        [Export] public float CooldownTime { get; set; } = 0f;
        [Export] public float DurationTime { get; set; } = 0f;

        protected float CooldownTimer { get; set; } = 0f;
        protected float DurationTimer { get; set; } = 0f;

        protected Player NodePlayer { get; set; }
        
        public PlayerAction(Player player)
        {
            NodePlayer = player;
        }

        public abstract void Update(float delta);
    }
}
