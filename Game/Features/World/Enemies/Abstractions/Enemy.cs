using Godot;
using Jogo25D.Characters;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using Jogo25D.Save;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;

namespace Jogo25D.Enemies
{
    // Base dos inimigos. O servidor decide tudo (IA, movimento, vida, ataques, morte) e manda o estado aos clientes que ja
    // entraram no mundo; os clientes so acompanham e desenham. Cuida do que e comum: vida salva, golpes recebidos (com
    // critico), dano por contato com intervalo por jogador, alvo, rede, direcao do sprite e morte com drop. Cada inimigo
    // implementa a IA (Step) e a morte (OnDie).
    public abstract partial class Enemy : CharacterBody2D, IHitTarget
    {
        #region Properties

        // Estado mandado aos clientes por segundo.
        private const float SyncInterval = 1f / 15f;

        [Save, GodotDictionaryField]
        public int Health { get; set; }

        public bool Dying { get; private set; }

        public abstract int MaxHealth { get; }
        protected abstract float SightRange { get; }
        protected abstract float LoseRange { get; }
        protected abstract int ContactDamage { get; }
        protected abstract float ContactKnockback { get; }
        protected virtual ulong ContactCooldownMsec => 800;
        protected abstract string DropItemId { get; }
        protected virtual int DropMin => 1;
        protected virtual int DropMax => 1;

        // Animacao quando nada forcado esta tocando, pelo movimento (servidor e clientes).
        protected abstract string AnimationFor(Vector2 velocity, bool grounded);

        // Jogador perseguido (servidor), ou nulo.
        protected Player Target { get; private set; }

        private readonly Dictionary<Player, ulong> _lastContact = new();
        private string _forcedAnimation;
        private float _syncTimer;

        // Cliente: ultimo estado recebido.
        private Vector2 _remotePosition;
        private Vector2 _remoteVelocity;
        private bool _hasRemote;

        #endregion

        #region Node children references

        public AnimatedSprite2D Sprite { get; set; }
        public Area2D Contact { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            AddToGroup("enemies");

            Sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
            Contact = GetNodeOrNull<Area2D>("Contact");
            _remotePosition = GlobalPosition;

            if (Health <= 0)
            {
                Health = MaxHealth;
            }

            if (Sprite != null)
            {
                Sprite.AnimationFinished += OnAnimationFinished;
                Sprite.Play(AnimationFor(Vector2.Zero, true));
            }

            OnReady();
        }

        public override void _PhysicsProcess(double delta)
        {
            var dt = (float)delta;

            if (!IsAuthority())
            {
                FollowRemote(dt);

                return;
            }

            if (Dying)
            {
                DyingStep(dt);
                SyncToClients(dt);

                return;
            }

            var previous = Target;

            Target = ResolveTarget();

            if (Target != null && previous == null)
            {
                OnNoticed();
            }

            Step(dt);
            HitPlayersInContact();
            SyncToClients(dt);
        }

        #endregion

        #region Core - Ganchos

        protected virtual void OnReady()
        {
        }

        // Servidor: IA e movimento de um passo de fisica.
        protected abstract void Step(float dt);

        // Servidor: acabou de avistar um jogador.
        protected virtual void OnNoticed()
        {
        }

        // Todos os peers: a vida zerou (animacao ou efeito da morte). O servidor chama FinishDeath quando ela acaba.
        protected abstract void OnDie();

        // Servidor: passo de fisica durante a morte.
        protected virtual void DyingStep(float dt)
        {
        }

        // Servidor: encostou num jogador e deu dano.
        protected virtual void OnContactHit(Player player)
        {
            PlayAction("attack");
        }

        #endregion

        #region Core - Alvo e contato (servidor)

        // Jogador vivo mais perto na mesma dimensao: entra no alvo dentro de SightRange e sai depois de LoseRange.
        private Player ResolveTarget()
        {
            var dimensionId = Dimension.IdOf(this);
            Player best = null;
            var bestDistance = float.MaxValue;

            foreach (var player in Players.All())
            {
                if (player is NPC || !IsInstanceValid(player) || player.CurrentHealth <= 0 || Dimension.IdOf(player) != dimensionId)
                {
                    continue;
                }

                var distance = player.GlobalPosition.DistanceTo(GlobalPosition);
                var range = player == Target ? LoseRange : SightRange;

                if (distance <= range && distance < bestDistance)
                {
                    best = player;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // Jogadores dentro da area de contato levam dano e empurrao, com um intervalo por jogador.
        private void HitPlayersInContact()
        {
            if (Contact == null)
            {
                return;
            }

            var now = Time.GetTicksMsec();

            foreach (var body in Contact.GetOverlappingBodies())
            {
                if (body is not Player player || player is NPC || player.CurrentHealth <= 0)
                {
                    continue;
                }

                if (_lastContact.TryGetValue(player, out var last) && now - last < ContactCooldownMsec)
                {
                    continue;
                }

                _lastContact[player] = now;

                player.ReceiveDamage(new DamageInfo { Amount = ContactDamage, Type = DamageType.Physical });
                player.ApplyKnockback(player.GlobalPosition - GlobalPosition + Vector2.Up * 8f, ContactKnockback);

                Face(player.GlobalPosition.X - GlobalPosition.X);
                OnContactHit(player);
            }
        }

        #endregion

        #region Core - Dano recebido (servidor)

        // Critico como no jogador, sem resistencias; nao recebe efeitos.
        public void ReceiveHit(DamageInfo damage, Player source)
        {
            if (damage == null || Dying || !IsAuthority())
            {
                return;
            }

            var crit = damage.CritChance > 0f && GD.Randf() < damage.CritChance;
            var amount = Mathf.Max(0, (int)(damage.Amount * (crit ? 1f + damage.CritDamage : 1f)));

            Health = Mathf.Max(0, Health - amount);

            if (Health > 0)
            {
                Broadcast(nameof(HurtBroadcast), Health);

                return;
            }

            Broadcast(nameof(DieBroadcast));
        }

        public virtual void ApplyKnockback(Vector2 direction, float force)
        {
            if (Dying || !IsAuthority() || direction == Vector2.Zero || force <= 0f)
            {
                return;
            }

            Velocity = direction.Normalized() * force;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void HurtBroadcast(int health)
        {
            Health = health;

            PlayForced("hurt");
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void DieBroadcast()
        {
            Health = 0;
            Dying = true;
            Velocity = Vector2.Zero;

            OnDie();
        }

        // Servidor: solta o drop e some para todos (o banco esquece o inimigo).
        protected void FinishDeath()
        {
            if (!IsAuthority() || IsQueuedForDeletion())
            {
                return;
            }

            var drop = ItemFactory.CreateInstance(DropItemId);

            if (drop != null)
            {
                drop.Quantity = GD.RandRange(DropMin, DropMax);
                EntitySpawner.SpawnWorldItemRequest(drop, GlobalPosition + Vector2.Up * 4f, Dimension.IdOf(this));
            }

            EntitySpawner.DespawnRequest(EntityRecord.InstanceIdOf(this));
        }

        #endregion

        #region Core - Rede

        protected bool IsAuthority()
        {
            return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
        }

        // Servidor: chama localmente e manda aos clientes que ja entraram no mundo (os conectados ainda entrando nao tem o
        // inimigo; recebem o estado atual no catch-up da entrada).
        private void Broadcast(string method, params Variant[] args)
        {
            Call(method, args);
            SendToJoined(method, args);
        }

        private void SendToJoined(string method, params Variant[] args)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                return;
            }

            foreach (var peerId in Multiplayer.GetPeers())
            {
                if (Players.FindByPeerId(peerId) != null)
                {
                    RpcId(peerId, method, args);
                }
            }
        }

        private void SyncToClients(float dt)
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                return;
            }

            _syncTimer -= dt;

            if (_syncTimer > 0f)
            {
                return;
            }

            _syncTimer = SyncInterval;

            SendToJoined(nameof(SyncStateReceive), GlobalPosition, Velocity, Sprite?.FlipH ?? false);
        }

        // Confiavel: chega na mesma ordem do despawn (um estado nao chega depois de o inimigo sumir no cliente).
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void SyncStateReceive(Vector2 position, Vector2 velocity, bool flip)
        {
            _remotePosition = position;
            _remoteVelocity = velocity;
            _hasRemote = true;

            if (Sprite == null)
            {
                return;
            }

            Sprite.FlipH = flip;

            if (_forcedAnimation == null && !Dying)
            {
                Play(AnimationFor(velocity, Mathf.Abs(velocity.Y) <= 1f));
            }
        }

        // Cliente: anda pela velocidade recebida e puxa a posicao para a do servidor.
        private void FollowRemote(float dt)
        {
            if (!_hasRemote)
            {
                return;
            }

            _remotePosition += _remoteVelocity * dt;
            GlobalPosition = GlobalPosition.Lerp(_remotePosition, Mathf.Min(1f, dt * 12f));
        }

        #endregion

        #region Core - Animacao

        // A arte olha para a direita: espelha quando o inimigo vai (ou olha) para a esquerda.
        protected void Face(float directionX)
        {
            if (Sprite != null && directionX != 0f)
            {
                Sprite.FlipH = directionX < 0f;
            }
        }

        // Animacao de movimento: so troca quando nada forcado (ataque, dano, morte) esta tocando.
        protected void PlayMotion(string animation)
        {
            if (_forcedAnimation == null)
            {
                Play(animation);
            }
        }

        private void Play(string animation)
        {
            if (Sprite != null && Sprite.Animation != animation)
            {
                Sprite.Play(animation);
            }
        }

        // Toca ate o fim antes de o movimento voltar a escolher a animacao.
        protected void PlayForced(string animation)
        {
            _forcedAnimation = animation;

            Sprite?.Play(animation);
            Sprite?.SetFrameAndProgress(0, 0f);
        }

        protected bool PlayingForced => _forcedAnimation != null;

        // Servidor: animacao de acao (ataque) em todos os peers. Dano e morte vem dos proprios broadcasts.
        protected void PlayAction(string animation)
        {
            Broadcast(nameof(ActionReceive), animation);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ActionReceive(string animation)
        {
            if (!Dying)
            {
                PlayForced(animation);
            }
        }

        private void OnAnimationFinished()
        {
            var finished = _forcedAnimation;

            _forcedAnimation = null;

            if (finished == "death")
            {
                FinishDeath();

                return;
            }

            if (!Dying)
            {
                Play(AnimationFor(Velocity, IsOnFloor() || !IsAuthority()));
            }
        }

        #endregion
    }
}
