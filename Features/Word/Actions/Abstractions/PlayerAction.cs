using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Scripts.Actions
{
    public abstract class PlayerAction
    {
        public bool CanUse { get; set; } = true;
        public bool InCooldown { get; set; } = false;
        public bool IsActive { get; set; } = false;
        public string ActionName { get; set; } = "Habilidade";
        public Godot.Texture2D Icon { get; set; } = null;
        public float Cooldown { get; set; } = 0f;
        public float Duration { get; set; } = 0f;
        public int MaxCharges { get; set; } = 1;
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
                if (!InCooldown)
                    CooldownTimer = 0f;
                CurrentCharges--;
                CanUse = CurrentCharges > 0;
                InCooldown = CurrentCharges < MaxCharges;
                DurationTimer = 0f;

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

        public abstract void OnStartAction(float delta);

        public abstract void OnUpdateWhileActive(float delta);

        public abstract void OnFinishedAction(float delta);

        public abstract void OnEnableAction(float delta);

        public abstract bool OnStartActionValidation(float delta);

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
