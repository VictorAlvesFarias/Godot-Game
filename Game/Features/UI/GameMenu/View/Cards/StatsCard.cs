using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Properties.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Properties;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.UI
{
    // Card "Stats": nome, vida e a lista de buffs do personagem local.
    public partial class StatsCard : CardUI
    {
        #region Node children references

        public Label CharacterNameLabel { get; private set; }
        public Label CharacterHealthLabel { get; private set; }
        public VBoxContainer BuffsListContainer { get; private set; }
        public Label EmptyLabel { get; private set; }

        #endregion

        #region Dinamic properties

        private static readonly PackedScene PropertyLineScene = GD.Load<PackedScene>("res://Scenes/Ui/GameMenu/Components/PropertyLine.tscn");

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            CharacterNameLabel = GetNode<Label>("Box/MarginContainer/VBoxContainer/CharacterNameLabel");
            CharacterHealthLabel = GetNode<Label>("Box/MarginContainer/VBoxContainer/CharacterHealthLabel");
            BuffsListContainer = GetNode<VBoxContainer>("Box/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer");
            EmptyLabel = GetNode<Label>("Box/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer/EmptyLabel");
        }

        public override void _Process(double delta)
        {
            if (IsOpen)
            {
                UpdateCharacterInfo();
            }
        }

        #endregion

        #region Core - Contrato do card

        public override void Bind(Player player)
        {
            base.Bind(player);

            UpdatePropertiesList();
        }

        public override void OnOpened()
        {
            base.OnOpened();

            UpdatePropertiesList();
        }

        #endregion

        #region Core - Stats

        public void UpdateCharacterInfo()
        {
            if (!HasPlayer)
            {
                return;
            }

            CharacterNameLabel.Text = $"Jogador #{LocalPlayer.PeerId}";
            CharacterHealthLabel.Text = $"Vida: {LocalPlayer.CurrentHealth}/{LocalPlayer.GetMaxHealth()}";
        }

        public void UpdatePropertiesList()
        {
            if (BuffsListContainer == null)
            {
                return;
            }

            foreach (Node child in BuffsListContainer.GetChildren())
            {
                if (child == EmptyLabel)
                {
                    continue;
                }

                BuffsListContainer.RemoveChild(child);

                child.QueueFree();
            }

            Godot.Collections.Array<BasePropertyData> properties = null;

            if (HasPlayer)
            {
                properties = new Godot.Collections.Array<BasePropertyData>();

                foreach (var property in LocalPlayer.ActiveProperties)
                {
                    properties.Add(property);
                }

                foreach (var property in LocalPlayer.ActiveProperties)
                {
                    properties.Add(property);
                }

                var equippedInstance = LocalPlayer.EquippedInstance();

                if (equippedInstance != null)
                {
                    foreach (var property in equippedInstance.Properties)
                    {
                        properties.Add(property);
                    }
                }
            }

            var lines = new List<string>();

            if (properties != null)
            {
                foreach (var damage in Resolver.Resolve(properties.OfType<DamagePropertyData>().ToList()))
                {
                    lines.Add(PropertyDescriptionFactory.Describe(damage));
                }

                foreach (var resistance in Resolver.Resolve(properties.OfType<DamageResistencePropertyData>().ToList()))
                {
                    lines.Add(PropertyDescriptionFactory.Describe(resistance));
                }

                foreach (var multiplier in Resolver.Resolve(properties.OfType<DamageResistenceMultiplierPropertyData>().ToList()))
                {
                    lines.Add(PropertyDescriptionFactory.Describe(multiplier));
                }

                var critList = properties.OfType<CritPropertyData>().ToList();

                if (critList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(critList)));
                }

                var movementList = properties.OfType<MovementPropertyData>().ToList();

                if (movementList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(movementList)));
                }

                var healthList = properties.OfType<HealthPropertyData>().ToList();

                if (healthList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(healthList)));
                }

                var attackList = properties.OfType<AttackPropertyData>().ToList();

                if (attackList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(attackList)));
                }

                var dashList = properties.OfType<DashPropertyData>().ToList();

                if (dashList.Count > 0)
                {
                    lines.Add(PropertyDescriptionFactory.Describe(Resolver.Resolve(dashList)));
                }
            }

            lines.RemoveAll(string.IsNullOrEmpty);

            EmptyLabel.Visible = lines.Count == 0;

            foreach (var text in lines)
            {
                var line = PropertyLineScene.Instantiate<Label>();

                line.Text = text;

                BuffsListContainer.AddChild(line);
            }
        }

        #endregion
    }
}
