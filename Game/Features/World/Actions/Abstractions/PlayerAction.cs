using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Properties;
using System.Collections.Generic;

namespace Jogo25D.Actions
{
    public abstract class ActionDefinition
    {

        #region Properties

        public string Id { get; init; } = "";
        public string ActionName { get; init; } = "Habilidade";
        public Texture2D Icon { get; set; }
        public PackedScene HitboxScene { get; set; }
        public float Cooldown { get; init; } = 0f;
        public float Duration { get; init; } = 0f;
        public int MaxCharges { get; init; } = 1;
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();
        public Godot.Collections.Array<BasePropertyData> Modifiers { get; set; } = new();
        public Godot.Collections.Array<string> Effects { get; set; } = new();

        #endregion

        #region Core - Abstract

        public abstract bool OnStartActionValidation(Player player, ActionDefinitionData instance, float delta);

        public abstract void OnStartAction(Player player, ActionDefinitionData instance, float delta);

        #endregion

        #region Core - Virtuals

        public virtual void OnCreate(Player player, ActionDefinitionData instance) { }

        public virtual void OnUpdateWhileActive(Player player, ActionDefinitionData instance, float delta) { }

        public virtual void OnFinishedAction(Player player, ActionDefinitionData instance, float delta) { }

        public virtual void OnEnableAction(Player player, ActionDefinitionData instance, float delta) { }

        public virtual void OnPassiveUpdate(Player player, ActionDefinitionData instance, float delta) { }

        #endregion

        #region Core - Utils

        public void Update(float delta, Player player, ActionDefinitionData data)
        {
            data.DurationTimer += delta;

            OnPassiveUpdate(player, data, delta);

            if (OnStartActionValidation(player, data, delta))
            {
                data.IsActive = true;

                if (!data.InCooldown)
                {
                    data.CooldownTimer = 0f;
                }

                data.CurrentCharges--;
                data.CanUse = data.CurrentCharges > 0;
                data.InCooldown = data.CurrentCharges < MaxCharges;
                data.DurationTimer = 0f;

                OnStartAction(player, data, delta);
            }

            if (data.IsActive)
            {
                OnUpdateWhileActive(player, data, delta);

                if (data.DurationTimer >= Duration)
                {
                    data.IsActive = false;
                    OnFinishedAction(player, data, delta);
                }
            }

            if (data.InCooldown)
            {
                data.CooldownTimer += delta;

                if (data.CooldownTimer >= Cooldown)
                {
                    data.CurrentCharges = Mathf.Min(data.CurrentCharges + 1, MaxCharges);
                    data.CooldownTimer = 0f;

                    OnEnableAction(player, data, delta);

                    if (data.CurrentCharges >= MaxCharges)
                    {
                        data.InCooldown = false;
                        data.CanUse = true;
                    }
                    else
                    {
                        data.InCooldown = true;
                        data.CanUse = data.CurrentCharges > 0;
                    }
                }
            }
        }

        public float GetCooldownProgress(ActionDefinitionData data)
        {
            if (Cooldown > 0f && data.InCooldown)
            {
                return Mathf.Clamp(data.CooldownTimer / Cooldown, 0f, 1f);
            }
            return 0f;
        }

        public float GetRemainingDuration(ActionDefinitionData data)
        {
            if (data.IsActive && Duration > 0f)
            {
                return Mathf.Max(0f, Duration - data.DurationTimer);
            }
            return 0f;
        }

        public float GetRemainingCooldown(ActionDefinitionData data)
        {
            if (data.InCooldown && Cooldown > 0f)
            {
                return Mathf.Max(0f, Cooldown - data.CooldownTimer);
            }
            return 0f;
        }

        #endregion

        #region Core - Effects

        protected Godot.Collections.Array<EffectDefinitionData> CreateEffects(EffectTriggerType type)
        {
            var effects = new Godot.Collections.Array<EffectDefinitionData>();

            foreach (var effectId in Effects)
            {
                if (EffectDB.Get(effectId)?.Type == type)
                {
                    effects.Add(EffectDB.CreateInstance(effectId));
                }
            }

            return effects;
        }

        #endregion
    }
}