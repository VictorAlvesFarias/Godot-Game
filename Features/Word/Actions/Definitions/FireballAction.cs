using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Effects;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using System.Collections.Generic;

namespace Jogo25D.Scripts.Actions
{
    public class FireballAction : PlayerAction
    {
        private const float FireballSpeed    = 750f;
        private const float FireballRange    = 1500f;
        private const float FireballArea     = 50f;
        private const int   FireballDamage   = 10;

        private readonly PackedScene _hitboxScene = GD.Load<PackedScene>("res://Scenes/World/Projectiles/Fireball.tscn");
        

        public FireballAction(Player player) : base(player)
        {
            Duration       = 0.2f;
            Cooldown       = 1f;
            MaxCharges     = 2;
            CurrentCharges = MaxCharges;
            ActionName     = "Fireball";
            Icon = GD.Load<Texture2D>(Assets.Icons.Spells.ICON_SPELL_4);
        }

        public override void OnStartAction(float delta)
        {
            if (_hitboxScene == null) return;

            var direction = (NodePlayer.MousePosition - NodePlayer.GlobalPosition).Normalized();
            var hitbox    = _hitboxScene.Instantiate<ProjectileHitbox>();

            hitbox.Initialize(
                new List<DamageInfo> { new DamageInfo { Amount = FireballDamage, Type = DamageType.Fire, SourcePeerId = (int)NodePlayer.PeerId } },
                new List<EffectDefinition>(),
                NodePlayer
            );

            hitbox.Direction       = direction;
            hitbox.Speed           = FireballSpeed;
            hitbox.Lifetime        = FireballRange / FireballSpeed;
            hitbox.GlobalPosition  = NodePlayer.GlobalPosition + direction * 60f;
            hitbox.Scale           = Vector2.One * (FireballArea / 25f);

            NodePlayer.GetParent().AddChild(hitbox);
        }

        public override void OnFinishedAction(float delta) { }

        public override void OnUpdateWhileActive(float delta) { }

        public override bool OnStartActionValidation(float delta)
            => NodePlayer.InputAbility && CanUse;

        public override void OnEnableAction(float delta) { }
    }
}
