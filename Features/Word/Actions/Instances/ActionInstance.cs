using Godot;
using Jogo25D.Characters;

namespace Jogo25D.Actions
{
    public class ActionInstance
    {

        public ActionDefinition Definition { get; set; }

        public Player Owner { get; set; }

        public int CurrentCharges { get; set; }
        public bool CanUse { get; set; } = true;
        public bool InCooldown { get; set; } = false;
        public bool IsActive { get; set; } = false;
        public float CooldownTimer { get; set; } = 0f;
        public float DurationTimer { get; set; } = 0f;
        public Vector2 DashDirection { get; set; } = Vector2.Zero;
        public CpuParticles2D DashParticles { get; set; }

        public string ActionName => Definition?.ActionName ?? "";
        public Texture2D Icon => Definition?.Icon;
        public int MaxCharges => Definition?.MaxCharges ?? 1;

        public ActionInstance(ActionDefinition definition, Player player)
        {
            Definition = definition;
            Owner = player;
            CurrentCharges = definition.MaxCharges;
            CanUse = definition.MaxCharges > 0;
        }

        public void Update(float delta)
        {
            DurationTimer += delta;

            Definition.OnPassiveUpdate(Owner, this, delta);

            if (Definition.OnStartActionValidation(Owner, this, delta))
            {
                IsActive = true;
                if (!InCooldown)
                {
                    CooldownTimer = 0f;
                }
                CurrentCharges--;
                CanUse = CurrentCharges > 0;
                InCooldown = CurrentCharges < MaxCharges;
                DurationTimer = 0f;

                Definition.OnStartAction(Owner, this, delta);
            }

            if (IsActive)
            {
                Definition.OnUpdateWhileActive(Owner, this, delta);

                if (DurationTimer >= Definition.Duration)
                {
                    IsActive = false;
                    Definition.OnFinishedAction(Owner, this, delta);
                }
            }

            if (InCooldown)
            {
                CooldownTimer += delta;

                if (CooldownTimer >= Definition.Cooldown)
                {
                    CurrentCharges = Mathf.Min(CurrentCharges + 1, MaxCharges);
                    CooldownTimer = 0f;

                    Definition.OnEnableAction(Owner, this, delta);

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

        public float GetCooldownProgress()
        {
            if (Definition.Cooldown > 0f && InCooldown)
            {
                return Mathf.Clamp(CooldownTimer / Definition.Cooldown, 0f, 1f);
            }
            return 0f;
        }

        public float GetDurationProgress()
        {
            if (Definition.Duration > 0f && IsActive)
            {
                return Mathf.Clamp(DurationTimer / Definition.Duration, 0f, 1f);
            }
            return 0f;
        }

        public float GetRemainingDuration()
        {
            if (IsActive && Definition.Duration > 0f)
            {
                return Mathf.Max(0f, Definition.Duration - DurationTimer);
            }
            return 0f;
        }

        public float GetRemainingCooldown()
        {
            if (InCooldown && Definition.Cooldown > 0f)
            {
                return Mathf.Max(0f, Definition.Cooldown - CooldownTimer);
            }
            return 0f;
        }

    }
}