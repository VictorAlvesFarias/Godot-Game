using Godot;
using Jogo25D.Characters;
using Jogo25D.Effects;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Items
{
    public abstract class ItemDefinition
    {
        #region Properties

        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public ItemType Type { get; init; }
        public bool Stackable { get; init; } = false;
        public int MaxStackSize { get; init; } = 99;
        public float Cooldown { get; init; } = 0.5f;
        public Godot.Collections.Array<BasePropertyData> Properties { get; set; } = new();
        public Godot.Collections.Array<BasePropertyData> Modifiers { get; set; } = new();
        public Godot.Collections.Array<string> Effects { get; set; } = new();

        // Luz em volta de quem segura o item (nulo = o item nao ilumina na mao).
        public Color? HeldLightColor { get; init; }
        public float HeldLightEnergy { get; init; } = 1f;

        // Imagens do item, na pasta com o seu id: a arte no tamanho real de pixel (item no mundo) e o icone do inventario/hotbar.
        public Texture2D Texture => LoadImage(ref _texture, "texture");
        public Texture2D Icon => LoadImage(ref _icon, "icon");

        // Pixels do mundo por pixel da arte: a mesma grade do jogador (character.png em pixel real, desenhada em escala 2).
        public virtual float WorldPixelScale => 2f;

        private const string TexturesDir = "res://Assets/Textures/Items";

        private (bool Loaded, Texture2D Texture) _texture;
        private (bool Loaded, Texture2D Texture) _icon;

        #endregion

        #region Node references

        public PackedScene HitboxScene { get; set; }

        #endregion

        #region Core - Abstract

        public abstract void Use(Player player, ItemData data);

        public virtual void UseAt(Player player, ItemData data, Vector2 position)
        {
        }

        public virtual void OnEquip(Player player, ItemData data)
        {
            foreach (var modifier in Modifiers)
            {
                player.ActiveProperties.Add(modifier);
            }

            foreach (var modifier in data.Modifiers)
            {
                player.ActiveProperties.Add(modifier);
            }
        }

        public virtual void OnUnequip(Player player, ItemData data)
        {
            foreach (var modifier in Modifiers)
            {
                player.ActiveProperties.Remove(modifier);
            }

            foreach (var modifier in data.Modifiers)
            {
                player.ActiveProperties.Remove(modifier);
            }
        }

        #endregion

        #region Core - Timers

        public void TriggerReloadTimer(ItemData data)
        {
            if (!CanReload(data))
            {
                return;
            }

            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();

            if (chargesProp != null)
            {
                data.ReloadTimer = chargesProp.ReloadCooldown;
                _timed = data;
            }
        }

        public void TriggerCooldownTimer(ItemData data)
        {
            if (!CanUse(data))
            {
                return;
            }

            data.CooldownRemainingTimer = Cooldown;
            _timed = data;
        }

        public bool IsEmpty(ItemData data)
        {
            return data == null || string.IsNullOrEmpty(data.Id) || data.Quantity <= 0;
        }

        #endregion

        #region Core - Item

        // Carrega a imagem da pasta do item uma vez. Sem o arquivo, fica sem imagem (nada a substitui) e o log avisa.
        private Texture2D LoadImage(ref (bool Loaded, Texture2D Texture) slot, string name)
        {
            if (slot.Loaded)
            {
                return slot.Texture;
            }

            var path = $"{TexturesDir}/{Id}/{name}.png";

            slot = (true, ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null);

            if (slot.Texture == null)
            {
                GD.PushWarning($"[ItemDefinition] imagem faltando: {path}");
            }

            return slot.Texture;
        }

        public bool CanReload(ItemData data)
        {
            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();

            if (chargesProp == null || chargesProp.InfiniteCharges)
            {
                return false;
            }

            return !IsReloading(data) && data.CurrentCharges < chargesProp.MaxCharges;
        }

        public bool IsReloading(ItemData data)
        {
            return data.ReloadTimer > 0f;
        }

        public void ConsumeCharge(ItemData data)
        {
            data.CurrentCharges = Math.Max(0, data.CurrentCharges - 1);
        }

        public void FinishReload(int chargesAdded, ItemData data)
        {
            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();
            var maxCharges = chargesProp != null ? chargesProp.MaxCharges : chargesAdded;

            data.ReloadTimer = 0f;
            data.CurrentCharges = Math.Min(data.CurrentCharges + chargesAdded, maxCharges);
        }

        #endregion

        #region Core - Virtuals

        public virtual bool CanUse(ItemData data)
        {
            var chargesProp = Resolver.Resolve(Properties.OfType<ChargesPropertyData>().ToList()).FirstOrDefault();
            var infiniteCharges = chargesProp != null ? chargesProp.InfiniteCharges : true;
            var hasCharges = infiniteCharges || data.CurrentCharges > 0;

            return data.CooldownRemainingTimer <= 0f && !IsReloading(data) && hasCharges;
        }

        // Item deste ItemDefinition com temporizador ligado (cada item do inventario tem a sua instancia de definicao): o
        // jogador desconta so esses, sem percorrer o inventario a cada passo.
        private ItemData _timed;

        public void TrackTimers(ItemData data)
        {
            if (data != null && (data.CooldownRemainingTimer > 0 || data.ReloadTimer > 0))
            {
                _timed = data;
            }
        }

        public void TickTimers(float delta)
        {
            if (_timed == null)
            {
                return;
            }

            Update(delta, _timed);

            if (_timed.CooldownRemainingTimer <= 0 && _timed.ReloadTimer <= 0)
            {
                _timed = null;
            }
        }

        public void Update(float delta, ItemData data)
        {
            if (data.CooldownRemainingTimer > 0)
            {
                data.CooldownRemainingTimer -= delta;
            }

            if (data.ReloadTimer > 0)
            {
                data.ReloadTimer -= delta;
            }
        }

        #endregion

        #region Core - Indicator

        public virtual void UpdateIndicator(Player player, ItemData data, float delta) { }

        public virtual void HideIndicator(Player player) { }

        public virtual void DestroyIndicator() { }

        #endregion
    }
}
