using Godot;
using Jogo25D.Entities;
using Jogo25D.Save;

namespace Jogo25D.Enemies
{
    // Slime: anda aos pulos, persegue o jogador mais perto pulando atras dele, da dano por contato e solta Gosma ao morrer.
    // Nasce na superficie da dimensao invertida (EnemySpawner).
    [Unload(UnloadMode.Global)]
    [SaveScene("slime", "res://Scenes/World/Enemies/Slime.tscn")]
    public partial class Slime : Enemy
    {
        #region Properties

        public const int SlimeMaxHealth = 30;

        // Comportamento (px do mundo, segundos). Uma celula tem 16 px.
        private const float WanderHopMin = 2f;
        private const float WanderHopMax = 3.5f;
        private const float ChaseHopInterval = 0.9f;
        private const float NoticeDelay = 0.4f;
        private static readonly Vector2 WanderHop = new(60f, -220f);
        private static readonly Vector2 ChaseHop = new(140f, -320f);
        private const float MaxFallSpeed = 900f;
        private const float GroundFriction = 900f;
        private const float StunAfterHit = 0.3f;

        public override int MaxHealth => SlimeMaxHealth;
        protected override float SightRange => 14 * 16f;
        protected override float LoseRange => 24 * 16f;
        protected override int ContactDamage => 8;
        protected override float ContactKnockback => 260f;
        protected override string DropItemId => "slime_gel";
        protected override int DropMax => 2;

        private float _gravity;
        private float _hopTimer;
        private float _stun;

        #endregion

        #region Core - Ganchos

        protected override void OnReady()
        {
            _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
            _hopTimer = (float)GD.RandRange(0.3, WanderHopMax);
        }

        protected override string AnimationFor(Vector2 velocity, bool grounded)
        {
            return grounded && velocity.Y >= 0f ? "idle" : "hop";
        }

        // Acabou de avistar um jogador: reage logo, sem esperar o resto do intervalo do pulo a toa.
        protected override void OnNoticed()
        {
            _hopTimer = Mathf.Min(_hopTimer, NoticeDelay);
        }

        protected override void OnDie()
        {
            PlayForced("death");
        }

        public override void ApplyKnockback(Vector2 direction, float force)
        {
            if (Dying || !IsAuthority() || direction == Vector2.Zero || force <= 0f)
            {
                return;
            }

            var push = direction.Normalized() * force;

            Velocity = new Vector2(push.X, Mathf.Min(push.Y, -force * 0.35f));
            _stun = StunAfterHit;
        }

        #endregion

        #region Core - IA (servidor)

        protected override void Step(float dt)
        {
            Think(dt);
            Move(dt);
        }

        private void Think(float dt)
        {
            _stun = Mathf.Max(0f, _stun - dt);

            if (!Grounded() || _stun > 0f)
            {
                return;
            }

            _hopTimer -= dt;

            if (_hopTimer > 0f)
            {
                return;
            }

            if (Target != null)
            {
                var side = Mathf.Sign(Target.GlobalPosition.X - GlobalPosition.X);

                Velocity = new Vector2((side == 0 ? 1 : side) * ChaseHop.X, ChaseHop.Y);
                _hopTimer = ChaseHopInterval;
            }
            else
            {
                Velocity = new Vector2((GD.Randf() < 0.5f ? -1 : 1) * WanderHop.X, WanderHop.Y);
                _hopTimer = (float)GD.RandRange(WanderHopMin, WanderHopMax);
            }

            PlayMotion("hop");
        }

        // No chao, ou encaixado parado contra o terreno (canto de degrau, vao estreito): pode pular de novo para sair dali.
        private bool Grounded()
        {
            return IsOnFloor() || GetSlideCollisionCount() > 0 && GetRealVelocity().LengthSquared() < 1f;
        }

        private void Move(float dt)
        {
            var velocity = Velocity;

            velocity.Y = Mathf.Min(velocity.Y + _gravity * dt, MaxFallSpeed);

            if (IsOnFloor() && velocity.Y >= 0f)
            {
                velocity.X = Mathf.MoveToward(velocity.X, 0f, GroundFriction * dt);
            }

            Velocity = velocity;

            MoveAndSlide();

            if (velocity.X != 0f)
            {
                Face(velocity.X);
            }

            if (IsOnFloor() && Velocity.Y >= 0f)
            {
                PlayMotion("idle");
            }
        }

        #endregion
    }
}
