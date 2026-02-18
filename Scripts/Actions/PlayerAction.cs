using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Scripts.Actions
{
    public abstract class PlayerAction
    {
        [Export] public bool CanUse { get; set; } = true;
        [Export] public bool InCooldown { get; set; } = false;
        [Export] public bool IsActive { get; set; } = false;
        [Export] public string ActionName { get; set; } = "Habilidade";
        [Export] public float Cooldown { get; set; } = 0f;
        [Export] public float Duration { get; set; } = 0f;
        /// <summary>Número máximo de cargas da habilidade. 1 = comportamento tradicional (1 uso por cooldown).</summary>
        [Export] public int MaxCharges { get; set; } = 1;

        /// <summary>Cargas disponíveis para usar (decresce ao usar, aumenta quando cooldown restaura uma carga).</summary>
        public int CurrentCharges { get; protected set; } = 1;

        protected float CooldownTimer { get; set; } = 0f;
        protected float DurationTimer { get; set; } = 0f;
        protected Player NodePlayer { get; set; }

        public PlayerAction(Player player)
        {
            NodePlayer = player;
            CurrentCharges = MaxCharges;
        }

        public virtual void Update(float delta)
        {
            DurationTimer += delta;

            if (this.OnStartActionValidation(delta))
            {
                IsActive = true;
                CurrentCharges--;
                CanUse = CurrentCharges > 0;
                InCooldown = CurrentCharges < MaxCharges;
                DurationTimer = 0f;
                CooldownTimer = 0f;

                this.OnStartAction(delta);
            }

            if (IsActive)
            {
                this.OnUpdateWhileActive(delta);

                if (DurationTimer >= Duration)
                {
                    IsActive = false;

                    this.OnFinishedAction(delta);
                }
            }

            if (InCooldown)
            {
                CooldownTimer += delta;

                if (CooldownTimer >= Cooldown)
                {
                    CurrentCharges = Mathf.Min(CurrentCharges + 1, MaxCharges);
                    CooldownTimer = 0f;

                    this.OnEnableAction(delta);

                    if (CurrentCharges >= MaxCharges)
                    {
                        InCooldown = false;
                        CanUse = true;
                    }
                    else
                    {
                        InCooldown = true;
                        CanUse = CurrentCharges > 0;
                    }
                }
            }
        }

        public virtual void OnStartAction(float delta)
        {

        }

        public virtual void OnUpdateWhileActive(float delta)
        {

        }

        public virtual void OnFinishedAction(float delta)
        {

        }

        public virtual void OnEnableAction(float delta)
        {

        }

        public virtual bool OnStartActionValidation(float delta)
        {
            return false;
        }
        
        public float GetCooldownProgress()
        {
            if (Cooldown > 0f && InCooldown)
            {
                return Mathf.Clamp(CooldownTimer / Cooldown, 0f, 1f);
            }

            return 0f;
        }

        public float GetDurationProgress()
        {
            if (Duration > 0f && IsActive)
            {
                return Mathf.Clamp(DurationTimer / Duration, 0f, 1f);
            }

            return 0f;
        }

        public float GetRemainingDuration()
        {
            if (IsActive && Duration > 0f)
            {
                return Mathf.Max(0f, Duration - DurationTimer);
            }

            return 0f;
        }

        public float GetRemainingCooldown()
        {
            if (InCooldown && Cooldown > 0f)
            {
                return Mathf.Max(0f, Cooldown - CooldownTimer);
            }

            return 0f;
        }
    }
}
