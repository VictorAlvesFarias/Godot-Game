using Godot;
using Jogo25D.Entities;
using Jogo25D.Save;

namespace Jogo25D.Enemies
{
    // Morcego: voa sem gravidade (colide com o terreno), vagueia pelas cavernas e, ao ver um jogador, paira ao lado e acima
    // dele e da rasantes, com dano por contato. Morre caindo e piscando, e solta Asa de morcego. Nasce no subterraneo da
    // dimensao invertida (EnemySpawner).
    [Unload(UnloadMode.Global)]
    [SaveScene("bat", "res://Scenes/World/Enemies/Bat.tscn")]
    public partial class Bat : Enemy
    {
        #region Properties

        public const int BatMaxHealth = 20;

        // Comportamento (px do mundo, segundos). Uma celula tem 16 px.
        private const float WanderSpeed = 45f;
        private const float WanderTurnMin = 1.5f;
        private const float WanderTurnMax = 3f;
        private const float HoverSpeed = 120f;
        private const float Acceleration = 420f;
        private static readonly Vector2 HoverOffset = new(40f, -56f);
        private const float DiveSpeed = 260f;
        private const float DiveDuration = 0.55f;
        private const float DiveIntervalMin = 1.8f;
        private const float DiveIntervalMax = 2.8f;
        private const float StunAfterHit = 0.25f;
        private const float DeathFallTime = 0.8f;

        public override int MaxHealth => BatMaxHealth;
        protected override float SightRange => 12 * 16f;
        protected override float LoseRange => 20 * 16f;
        protected override int ContactDamage => 6;
        protected override float ContactKnockback => 200f;
        protected override string DropItemId => "bat_wing";

        private float _gravity;
        private Vector2 _wanderDirection;
        private float _wanderTimer;
        private float _diveTimer;
        private float _diving;
        private Vector2 _diveDirection;
        private float _stun;
        private float _hoverSide = 1f;
        private float _deathTimer;
        private Tween _blink;

        #endregion

        #region Core - Ganchos

        protected override void OnReady()
        {
            _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
            _diveTimer = (float)GD.RandRange(DiveIntervalMin, DiveIntervalMax);
            PickWanderDirection();
        }

        protected override string AnimationFor(Vector2 velocity, bool grounded)
        {
            return "fly";
        }

        protected override void OnNoticed()
        {
            _hoverSide = Target != null && GlobalPosition.X < Target.GlobalPosition.X ? -1f : 1f;
            _diveTimer = Mathf.Min(_diveTimer, 1.2f);
        }

        // O rasante ja e o ataque: o contato no meio dele nao recomeca a animacao.
        protected override void OnContactHit(Characters.Player player)
        {
            if (_diving <= 0f)
            {
                PlayAction("attack");
            }

            _diving = 0f;
        }

        // Todos os peers: cai piscando; o servidor solta o drop depois da queda.
        protected override void OnDie()
        {
            PlayForced("hurt");

            _blink?.Kill();
            _blink = CreateTween().SetLoops();
            _blink.TweenProperty(this, "modulate:a", 0.2f, 0.08f);
            _blink.TweenProperty(this, "modulate:a", 1f, 0.08f);
        }

        protected override void DyingStep(float dt)
        {
            Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, 300f * dt), Mathf.Min(Velocity.Y + _gravity * dt, 600f));

            MoveAndSlide();

            _deathTimer += dt;

            if (_deathTimer >= DeathFallTime)
            {
                FinishDeath();
            }
        }

        public override void ApplyKnockback(Vector2 direction, float force)
        {
            if (Dying || !IsAuthority() || direction == Vector2.Zero || force <= 0f)
            {
                return;
            }

            Velocity = direction.Normalized() * force;
            _stun = StunAfterHit;
            _diving = 0f;
        }

        #endregion

        #region Core - IA (servidor)

        protected override void Step(float dt)
        {
            _stun = Mathf.Max(0f, _stun - dt);

            if (_stun > 0f)
            {
                Velocity = Velocity.MoveToward(Vector2.Zero, Acceleration * dt);
            }
            else if (_diving > 0f)
            {
                Dive(dt);
            }
            else if (Target != null)
            {
                Hover(dt);
            }
            else
            {
                Wander(dt);
            }

            MoveAndSlide();

            // Bateu no terreno vagando: troca de direcao.
            if (Target == null && _diving <= 0f && GetSlideCollisionCount() > 0)
            {
                PickWanderDirection();
            }

            if (Mathf.Abs(Velocity.X) > 1f)
            {
                Face(Velocity.X);
            }

            PlayMotion("fly");
        }

        private void Wander(float dt)
        {
            _wanderTimer -= dt;

            if (_wanderTimer <= 0f)
            {
                PickWanderDirection();
            }

            Velocity = Velocity.MoveToward(_wanderDirection * WanderSpeed, Acceleration * dt);
        }

        private void PickWanderDirection()
        {
            _wanderDirection = Vector2.Right.Rotated((float)GD.RandRange(0, Mathf.Tau));
            _wanderTimer = (float)GD.RandRange(WanderTurnMin, WanderTurnMax);
        }

        // Paira ao lado e acima do alvo; de tempos em tempos parte para o rasante.
        private void Hover(float dt)
        {
            var spot = Target.GlobalPosition + new Vector2(HoverOffset.X * _hoverSide, HoverOffset.Y);

            Velocity = Velocity.MoveToward((spot - GlobalPosition).LimitLength(HoverSpeed), Acceleration * dt);

            _diveTimer -= dt;

            if (_diveTimer > 0f)
            {
                return;
            }

            _diveDirection = (Target.GlobalPosition - GlobalPosition).Normalized();
            _diving = DiveDuration;
            _diveTimer = (float)GD.RandRange(DiveIntervalMin, DiveIntervalMax);
            _hoverSide = -_hoverSide;

            PlayAction("attack");
        }

        // Rasante: reto na direcao em que o alvo estava; termina pelo tempo ou ao bater em algo.
        private void Dive(float dt)
        {
            _diving -= dt;

            Velocity = _diveDirection * DiveSpeed;

            if (GetSlideCollisionCount() > 0)
            {
                _diving = 0f;
            }
        }

        #endregion
    }
}
