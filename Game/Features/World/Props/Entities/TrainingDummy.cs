using Godot;
using Jogo25D.Characters;
using Jogo25D.Hitboxes;
using Jogo25D.Items;
using Jogo25D.Save;

namespace Jogo25D.Props
{
    // Boneco de treino: recebe os golpes das armas sem morrer e mostra o dano de cada acerto, o DPS e o total da sequencia.
    // O servidor calcula o dano (com o critico da arma) e manda cada acerto para todos; cada peer conta a sequencia com os
    // mesmos acertos. Nada disso e salvo.
    [SaveScene("training_dummy", "res://Scenes/World/Props/TrainingDummy.tscn")]
    public partial class TrainingDummy : Prop, IHitTarget
    {
        #region Properties

        // A sequencia termina depois deste tempo sem acerto.
        private const ulong ComboTimeoutMsec = 3000;
        private const float PopupDuration = 0.8f;
        private const float PopupRise = 52f;

        private static readonly Color HitColor = new(1f, 1f, 1f);
        private static readonly Color CritColor = new(1f, 0.7f, 0.15f);

        private bool _combo;
        private ulong _comboStart;
        private ulong _lastHit;
        private int _comboTotal;

        #endregion

        #region Node children references

        public Node2D Labels { get; set; }
        public Label StatsLabel { get; set; }
        public Label HitTemplate { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            base._Ready();

            Labels = GetNodeOrNull<Node2D>("Labels");
            StatsLabel = GetNodeOrNull<Label>("Labels/StatsLabel");
            HitTemplate = GetNodeOrNull<Label>("Labels/HitTemplate");

            if (StatsLabel != null)
            {
                StatsLabel.Visible = false;
            }

            if (HitTemplate != null)
            {
                HitTemplate.Visible = false;
            }

            SetProcess(false);
        }

        // So roda com a sequencia ativa: atualiza o DPS e encerra a sequencia depois da pausa.
        public override void _Process(double delta)
        {
            var now = Time.GetTicksMsec();

            if (now - _lastHit > ComboTimeoutMsec)
            {
                _combo = false;

                if (StatsLabel != null)
                {
                    StatsLabel.Visible = false;
                }

                SetProcess(false);

                return;
            }

            UpdateStats(now);
        }

        #endregion

        #region Core - Acerto

        // Servidor: critico como no jogador, sem resistencias; o boneco nao morre, nao e empurrado e nao recebe efeitos.
        public void ReceiveHit(DamageInfo damage, Player source)
        {
            if (damage == null || Multiplayer != null && Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
            {
                return;
            }

            var crit = damage.CritChance > 0f && GD.Randf() < damage.CritChance;
            var amount = Mathf.Max(0, (int)(damage.Amount * (crit ? 1f + damage.CritDamage : 1f)));

            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
            {
                HitBroadcast(amount, crit);

                return;
            }

            Rpc(nameof(HitBroadcast), amount, crit);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void HitBroadcast(int amount, bool crit)
        {
            var now = Time.GetTicksMsec();

            if (!_combo || now - _lastHit > ComboTimeoutMsec)
            {
                _combo = true;
                _comboStart = now;
                _comboTotal = 0;
            }

            _comboTotal += amount;
            _lastHit = now;

            ShowHit(amount, crit);
            UpdateStats(now);
            SetProcess(true);
        }

        // Total e DPS da sequencia (o tempo vai do primeiro acerto ate agora, com no minimo 1 s).
        public int ComboTotal => _combo ? _comboTotal : 0;

        public float ComboDps(ulong now)
        {
            return _combo ? _comboTotal / Mathf.Max(1f, (now - _comboStart) / 1000f) : 0f;
        }

        public bool InCombo => _combo;

        #endregion

        #region Core - Visual

        private void UpdateStats(ulong now)
        {
            if (StatsLabel == null)
            {
                return;
            }

            StatsLabel.Text = $"DPS {ComboDps(now):0}\nTotal {_comboTotal}";
            StatsLabel.Visible = true;
        }

        // Numero do acerto subindo e sumindo; o critico maior, em outra cor e com "!".
        private void ShowHit(int amount, bool crit)
        {
            if (HitTemplate == null || Labels == null)
            {
                return;
            }

            var label = (Label)HitTemplate.Duplicate();

            label.Visible = true;
            label.Text = crit ? $"{amount}!" : $"{amount}";
            label.AddThemeColorOverride("font_color", crit ? CritColor : HitColor);

            if (crit)
            {
                label.AddThemeFontSizeOverride("font_size", HitTemplate.GetThemeFontSize("font_size") + 8);
            }

            var start = HitTemplate.Position + new Vector2((float)GD.RandRange(-12, 12), 0f);

            label.Position = start;

            Labels.AddChild(label);

            var tween = label.CreateTween();

            tween.TweenProperty(label, "position", start + Vector2.Up * PopupRise, PopupDuration);
            tween.Parallel().TweenProperty(label, "modulate:a", 0f, PopupDuration);
            tween.TweenCallback(Callable.From(label.QueueFree));
        }

        #endregion
    }
}
