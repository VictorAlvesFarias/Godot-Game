using Godot;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Properties;
using Jogo25D.Effects;

namespace Jogo25D.Actions
{
    public abstract class ActionDefinition
    {

        public string Id { get; init; } = "";
        public string ActionName { get; init; } = "Habilidade";
        public Texture2D Icon { get; set; }
        public PackedScene HitboxScene { get; set; }

        public float Cooldown { get; init; } = 0f;
        public float Duration { get; init; } = 0f;
        public int MaxCharges { get; init; } = 1;

        public List<BaseProperty> Properties { get; set; } = new();
        public List<EffectDefinition> OnHitEffects { get; set; } = new();
        public List<EffectDefinition> OnUseEffects { get; set; } = new();

        public virtual void OnCreate(Player player, ActionInstance instance)
        {
        }

        public abstract bool OnStartActionValidation(Player player, ActionInstance instance, float delta);
        public abstract void OnStartAction(Player player, ActionInstance instance, float delta);

        public virtual void OnUpdateWhileActive(Player player, ActionInstance instance, float delta)
        {
        }

        public virtual void OnFinishedAction(Player player, ActionInstance instance, float delta)
        {
        }

        public virtual void OnEnableAction(Player player, ActionInstance instance, float delta)
        {
        }

        public virtual void OnPassiveUpdate(Player player, ActionInstance instance, float delta)
        {
        }

    }
}