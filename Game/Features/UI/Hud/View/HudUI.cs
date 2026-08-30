using Godot;
using Jogo25D.Actions;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Effects;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Systems;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.UI
{
    public partial class HudUI : ScreenUI
    {
        #region Dinamic properties

        public string PlayerGroupName { get; set; } = "players";
        public bool HealthBarBaselineCaptured { get; set; }
        public Vector2 HealthBarBackBaselineSize { get; set; }
        public double PingTimer { get; set; } = 0.0;
        public double PingInterval { get; set; } = 1.0;
        public double LastPingSentTime { get; set; } = 0.0;
        public int CurrentPing { get; set; } = 0;
        public bool AbilityTemplateMissing { get; set; }
        public bool EffectTemplateMissing { get; set; }

        #endregion

        #region Node references

        public Player LocalPlayer { get; set; }

        #endregion

        #region Node children references

        public Panel MiningHint { get; set; }
        public Panel[] HotbarSlotPanels { get; } = new Panel[8];
        public TextureRect[] HotbarIconRects { get; } = new TextureRect[8];
        public Label[] HotbarNameLabels { get; } = new Label[8];
        public Label[] HotbarQtyLabels { get; } = new Label[8];
        public List<Panel> AbilitySlots { get; } = new List<Panel>();
        public List<ProgressBar> AbilityFillBars { get; } = new List<ProgressBar>();
        public List<TextureRect> AbilityIconRects { get; } = new List<TextureRect>();
        public List<Label> AbilityInnerNameLabels { get; } = new List<Label>();
        public List<Label> AbilityTimerLabels { get; } = new List<Label>();
        public List<Label> AbilityChargesLabels { get; } = new List<Label>();
        public List<EffectSlotViews> EffectSlots { get; } = new List<EffectSlotViews>();
        public StyleBoxFlat HotbarNormalStyle { get; set; }
        public StyleBoxFlat HotbarSelectedStyle { get; set; }

        #endregion

        #region Godot implementation

        // Nao e overlay: e a tela principal do jogo. Precisa virar o Current do router e
        // esconder o menu anterior (CreateCharacter/WorldSelect/...) - senao ele fica visivel
        // por baixo pra sempre. Pause/Inventory/Console etc continuam overlay de verdade,
        // empilhando POR CIMA do Hud sem tirar ele do Current.
        public override bool IsOverlay => false;

        public override void _Ready()
        {
            Game.WhenReady(Initialize);
        }

        public override void _ExitTree()
        {
            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.ItemEquipped -= OnItemEquipped;
                LocalPlayer.InventoryChanged -= UpdateHotbar;
            }
        }
        public override void _Process(double delta)
        {
            UpdateFpsDisplay(delta);
            UpdateHealthDisplay();

            if (LocalPlayer != null && IsInstanceValid(LocalPlayer) && AbilitySlots.Count == 0)
            {
                BuildAbilitySlots();
            }

            UpdateAbilitySlots();
            UpdateEffectIcons();
            UpdateMiningHint();
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.HudUI.AbilityTemplate.Node.Visible = false;

            var hotkeySlot0 = Game.Ui.HudUI.HotkeySlot0.Node;

            ConfigureHotkeyHint(hotkeySlot0, "I", CreateInventoryIcon());
            ConfigureHotkeyHint(DuplicateHotkeySlot(hotkeySlot0), "M", CreateMapIcon());
            ConfigureHotkeyHint(DuplicateHotkeySlot(hotkeySlot0), "K", CreateSkillTreeIcon());

            MiningHint = DuplicateHotkeySlot(hotkeySlot0);

            ConfigureHotkeyHint(MiningHint, "V", GD.Load<Texture2D>(Textures.Items.PICKAXE_STARTING_ICON));

            MiningHint.Visible = false;

            Game.Ui.HudUI.EffectTemplate.Node.Visible = false;

            var hotbarContainer = Game.Ui.HudUI.HotbarContainer.Node;
            var slot0 = Game.Ui.HudUI.Slot0.Node;
            var slot0Selected = Game.Ui.HudUI.Slot0Selected.Node;

            HotbarNormalStyle = slot0.GetThemeStylebox("panel") as StyleBoxFlat;
            HotbarSelectedStyle = slot0Selected.GetThemeStylebox("panel") as StyleBoxFlat;

            slot0Selected.Visible = false;

            for (int i = 0; i < 8; i++)
            {
                Panel panel;
                if (i == 0)
                {
                    panel = slot0;
                }
                else
                {
                    panel = (Panel)slot0.Duplicate();
                    panel.GetNode<Label>("NumLabel").Text = $"{i + 1}";
                    hotbarContainer.AddChild(panel);
                }

                HotbarSlotPanels[i] = panel;
                HotbarIconRects[i] = panel.GetNode<TextureRect>("MarginContainer/CenterContainer/IconRect");
                HotbarNameLabels[i] = panel.GetNode<Label>("MarginContainer/CenterContainer/NameLabel");
                HotbarQtyLabels[i] = panel.GetNode<Label>("QtyLabel");
            }
            CallDeferred(nameof(FindLocalPlayer));
        }

        #endregion

        #region Core - Ping

        public void UpdateFpsDisplay(double delta)
        {
            var fps = Engine.GetFramesPerSecond();
            var fpsText = $"FPS: {fps}";

            if (Multiplayer != null &&
                Multiplayer.MultiplayerPeer != null &&
                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
            {
                try
                {
                    if (!Multiplayer.IsServer())
                    {
                        PingTimer += delta;

                        if (PingTimer >= PingInterval)
                        {
                            PingTimer = 0.0;
                            LastPingSentTime = Time.GetTicksMsec();
                            RpcId(1, nameof(PingPong));
                        }

                        Game.Ui.HudUI.FpsLabel.Node.Text = $"{fpsText} | Ping: {CurrentPing}ms";
                    }
                    else
                    {
                        Game.Ui.HudUI.FpsLabel.Node.Text = $"{fpsText} | Ping: 0ms";
                    }
                }
                catch
                {
                    Game.Ui.HudUI.FpsLabel.Node.Text = fpsText;
                }
            }
            else
            {
                Game.Ui.HudUI.FpsLabel.Node.Text = fpsText;
            }
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
        public void PingPong()
        {
            if (Multiplayer.IsServer())
            {
                var senderId = Multiplayer.GetRemoteSenderId();

                RpcId(senderId, nameof(ReceivePong));
            }
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
        public void ReceivePong()
        {
            double now = Time.GetTicksMsec();

            CurrentPing = (int)(now - LastPingSentTime);
        }

        #endregion

        #region Core - Health

        public void UpdateHealthDisplay()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                FindLocalPlayer();
            }

            var maxHealth = LocalPlayer != null && IsInstanceValid(LocalPlayer) ? LocalPlayer.GetMaxHealth() : (int)50f;
            var currentHealth = LocalPlayer != null && IsInstanceValid(LocalPlayer) ? LocalPlayer.CurrentHealth : maxHealth;

            LayoutHealthBar(maxHealth, currentHealth);
            UpdateLegacyHealthBar(maxHealth, currentHealth);
        }

        private void UpdateLegacyHealthBar(int maxHealth, int currentHealth)
        {
            Game.Ui.HudUI.LegacyHealthBar.Node.MaxValue = maxHealth;
            Game.Ui.HudUI.LegacyHealthBar.Node.Value = currentHealth;

            var barWidth = maxHealth * 10f;

            Game.Ui.HudUI.LegacyHealthBar.Node.CustomMinimumSize = new Vector2(barWidth, 30f);
        }

        private void LayoutHealthBar(int maxHealth, int currentHealth)
        {
            if (!HealthBarBaselineCaptured)
            {
                HealthBarBackBaselineSize = Game.Ui.HudUI.HealthBarBack.Node.PhysicalSize;
                HealthBarBaselineCaptured = true;
            }

            var pxPerHealth = HealthBarBackBaselineSize.X / 50f;
            var growthPx = Mathf.Max(0f, maxHealth - 50f) * pxPerHealth;

            Game.Ui.HudUI.HealthBarBack.Node.PhysicalSize = HealthBarBackBaselineSize + new Vector2(growthPx, 0f);

            Game.Ui.HudUI.HealthBarFill.Node.Ratio = maxHealth > 0 ? Mathf.Clamp((float)currentHealth / maxHealth, 0f, 1f) : 0f;
        }

        public void FindLocalPlayer()
        {
            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.ItemEquipped -= OnItemEquipped;
                LocalPlayer.InventoryChanged -= UpdateHotbar;
            }

            var worldManager = Game.Managers.WorldManager.Node;

            LocalPlayer = worldManager?.GetLocalPlayer();

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return;
            }

            if (Game.Ui.HudUI.Minimap.Node != null && IsInstanceValid(Game.Ui.HudUI.Minimap.Node))
            {
                Game.Ui.HudUI.Minimap.Node.SetLocalPlayer(LocalPlayer);
            }

            LocalPlayer.ItemEquipped += OnItemEquipped;
            LocalPlayer.InventoryChanged += UpdateHotbar;

            UpdateHotbar();
        }

        public void OnItemEquipped(long instanceId)
        {
            UpdateHotbar();
        }

        #endregion

        #region Core - Abilities

        public void BuildAbilitySlots()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || Game.Ui.HudUI.AbilitiesContainer.Node == null || AbilityTemplateMissing)
            {
                return;
            }

            var list = Resolver.Resolve(LocalPlayer.ActiveAbilities, LocalPlayer.ActiveAbilities);

            if (list == null || list.Count == 0)
            {
                AbilitySlots.Clear();
                AbilityFillBars.Clear();
                AbilityIconRects.Clear();
                AbilityInnerNameLabels.Clear();
                AbilityTimerLabels.Clear();
                AbilityChargesLabels.Clear();

                for (int i = Game.Ui.HudUI.AbilitiesContainer.Node.GetChildCount() - 1; i >= 0; i--)
                {
                    if (Game.Ui.HudUI.AbilitiesContainer.Node.GetChild(i) is Control c)
                    {
                        c.Visible = false;
                    }
                }

                return;
            }

            AbilitySlots.Clear();
            AbilityFillBars.Clear();
            AbilityIconRects.Clear();
            AbilityInnerNameLabels.Clear();
            AbilityTimerLabels.Clear();
            AbilityChargesLabels.Clear();

            for (int i = Game.Ui.HudUI.AbilitiesContainer.Node.GetChildCount() - 1; i >= 0; i--)
            {
                var old = Game.Ui.HudUI.AbilitiesContainer.Node.GetChild(i);

                if (old.Name == "AbilityTemplate")
                {
                    continue;
                }

                Game.Ui.HudUI.AbilitiesContainer.Node.RemoveChild(old);

                old.QueueFree();
            }

            for (int i = 0; i < list.Count; i++)
            {
                var slotViews = CreateAbilitySlot();

                if (slotViews == null)
                {
                    AbilityTemplateMissing = true;
                    break;
                }

                Game.Ui.HudUI.AbilitiesContainer.Node.AddChild(slotViews.Panel);

                var fillBar = slotViews.FillBar;

                fillBar.MinValue = 0;
                fillBar.MaxValue = 1;
                fillBar.Value = 0;
                fillBar.FillMode = (int)ProgressBar.FillModeEnum.TopToBottom;

                AbilitySlots.Add(slotViews.Panel);
                AbilityFillBars.Add(fillBar);
                AbilityIconRects.Add(slotViews.IconRect);
                AbilityInnerNameLabels.Add(slotViews.InnerNameLabel);
                AbilityTimerLabels.Add(slotViews.TimerLabel);
                AbilityChargesLabels.Add(slotViews.ChargesLabel);
            }
        }

        public AbilitySlotViews CreateAbilitySlot()
        {
            var template = Game.Ui.HudUI.AbilityTemplate.Node;

            if (template == null)
            {
                GD.PushError("[HudUI.CreateAbilitySlot] Template 'AbilityTemplate' não encontrado em Hud.tscn");

                return null;
            }

            var panel = (Panel)template.Duplicate();

            panel.Visible = true;

            var iconRect = panel.GetNode<TextureRect>("MarginContainer/CenterContainer/IconRect");
            var innerNameLabel = panel.GetNode<Label>("MarginContainer/CenterContainer/NameLabel");
            var fill = panel.GetNode<ProgressBar>("CooldownFill");
            var timerLabel = panel.GetNode<Label>("TimerLabel");
            var chargesLabel = panel.GetNode<Label>("QtyLabel");

            return new AbilitySlotViews(panel, fill, iconRect, innerNameLabel, timerLabel, chargesLabel);
        }

        public void UpdateAbilitySlots()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return;
            }

            var list = Resolver.Resolve(LocalPlayer.ActiveAbilities, LocalPlayer.ActiveAbilities);

            if (list == null || AbilityFillBars.Count != list.Count)
            {
                return;
            }

            for (int i = 0; i < list.Count && i < AbilityFillBars.Count; i++)
            {
                var action = list[i];
                var bar = AbilityFillBars[i];
                var iconRect = i < AbilityIconRects.Count ? AbilityIconRects[i] : null;
                var innerNameLabel = i < AbilityInnerNameLabels.Count ? AbilityInnerNameLabels[i] : null;
                var timerLabel = i < AbilityTimerLabels.Count ? AbilityTimerLabels[i] : null;
                var chargesLabel = i < AbilityChargesLabels.Count ? AbilityChargesLabels[i] : null;

                if (action == null || bar == null)
                {
                    continue;
                }

                var def = ActionFactory.Create(action.Id);

                if (iconRect != null)
                {
                    if (def?.Icon != null)
                    {
                        iconRect.Texture = def.Icon;
                        iconRect.Visible = true;

                        if (innerNameLabel != null)
                        {
                            innerNameLabel.Visible = false;
                        }
                    }
                    else
                    {
                        iconRect.Texture = null;
                        iconRect.Visible = false;

                        if (innerNameLabel != null)
                        {
                            innerNameLabel.Text = def?.ActionName;
                            innerNameLabel.Visible = true;
                        }
                    }
                }

                if (chargesLabel != null)
                {
                    chargesLabel.Text = def?.MaxCharges > 1 ? $"x{action.CurrentCharges}" : "";
                }

                if (action.InCooldown)
                {
                    bar.Value = 1f - (def?.GetCooldownProgress(action) ?? 0f);
                    bar.Visible = true;

                    if (timerLabel != null)
                    {
                        if (action.IsActive)
                        {
                            timerLabel.Text = $"{def?.GetRemainingDuration(action) ?? 0f:F1}s";
                        }
                        else
                        {
                            timerLabel.Text = $"{def?.GetRemainingCooldown(action) ?? 0f:F1}s";
                        }

                        timerLabel.Visible = true;
                    }
                }
                else if (action.IsActive)
                {
                    bar.Value = 1f;
                    bar.Visible = true;

                    if (timerLabel != null)
                    {
                        timerLabel.Text = $"{def?.GetRemainingDuration(action) ?? 0f:F1}s";
                        timerLabel.Visible = true;
                    }
                }
                else
                {
                    bar.Value = 0;
                    bar.Visible = false;

                    if (timerLabel != null)
                    {
                        timerLabel.Text = "";
                        timerLabel.Visible = false;
                    }
                }
            }
        }

        #endregion

        #region Core - Effects

        public EffectSlotViews CreateEffectSlot()
        {
            var template = Game.Ui.HudUI.EffectTemplate.Node;

            if (template == null)
            {
                GD.PushError("[HudUI.CreateEffectSlot] Template 'EffectTemplate' não encontrado em Hud.tscn");

                return null;
            }

            var panel = (Panel)template.Duplicate();

            panel.Visible = true;

            var iconRect = panel.GetNode<TextureRect>("IconRect");
            var timerLabel = panel.GetNode<Label>("TimerLabel");

            Game.Ui.HudUI.EffectsContainer.Node.AddChild(panel);

            return new EffectSlotViews(panel, iconRect, timerLabel);
        }

        public void UpdateEffectIcons()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || Game.Ui.HudUI.EffectsContainer.Node == null || EffectTemplateMissing)
            {
                return;
            }

            var effects = Resolver.Resolve(LocalPlayer.ActiveEffects, LocalPlayer.ActiveEffects);

            while (EffectSlots.Count < effects.Count)
            {
                var slot = CreateEffectSlot();

                if (slot == null)
                {
                    EffectTemplateMissing = true;
                    break;
                }

                EffectSlots.Add(slot);
            }

            while (EffectSlots.Count > effects.Count)
            {
                var last = EffectSlots[^1];

                EffectSlots.RemoveAt(EffectSlots.Count - 1);
                last.Panel.QueueFree();
            }

            for (int i = 0; i < effects.Count && i < EffectSlots.Count; i++)
            {
                var def = EffectDB.Get(effects[i].Id);
                var slot = EffectSlots[i];

                slot.IconRect.Texture = def?.Icon;
                slot.IconRect.Visible = def?.Icon != null;
                slot.Panel.TooltipText = def?.Name ?? "";

                var remaining = def?.GetRemainingDuration(effects[i]) ?? 0f;

                if (remaining > 0f)
                {
                    slot.TimerLabel.Text = $"{remaining:F0}";
                    slot.TimerLabel.Visible = true;
                }
                else
                {
                    slot.TimerLabel.Text = "";
                    slot.TimerLabel.Visible = false;
                }
            }
        }

        #endregion

        #region Core - Hotkeys

        public Panel DuplicateHotkeySlot(Panel template)
        {
            var panel = (Panel)template.Duplicate();

            Game.Ui.HudUI.HotkeysContainer.Node.AddChild(panel);

            return panel;
        }

        public void ConfigureHotkeyHint(Panel panel, string key, Texture2D icon)
        {
            var iconRect = panel.GetNode<TextureRect>("MarginContainer/CenterContainer/IconRect");
            var keyLabel = panel.GetNode<Label>("MarginContainer/CenterContainer/KeyLabel");
            var cornerLabel = panel.GetNode<Label>("CornerLabel");

            if (icon != null)
            {
                iconRect.Texture = icon;
                iconRect.Visible = true;
                keyLabel.Visible = false;
                cornerLabel.Text = key;
                cornerLabel.Visible = true;
            }
            else
            {
                keyLabel.Text = key;
                keyLabel.Visible = true;
                iconRect.Visible = false;
                cornerLabel.Visible = false;
            }
        }

        public void UpdateMiningHint()
        {
            if (MiningHint == null)
            {
                return;
            }

            MiningHint.Visible = LocalPlayer != null
                && IsInstanceValid(LocalPlayer)
                && LocalPlayer.ItemDefinitions.Values.Any(def => def is ToolDefinition);
        }

        private static Texture2D CreateInventoryIcon()
        {
            var image = Image.CreateEmpty(28, 28, false, Image.Format.Rgba8);
            var color = Colors.White;

            image.FillRect(new Rect2I(3, 3, 10, 10), color);
            image.FillRect(new Rect2I(15, 3, 10, 10), color);
            image.FillRect(new Rect2I(3, 15, 10, 10), color);
            image.FillRect(new Rect2I(15, 15, 10, 10), color);

            return ImageTexture.CreateFromImage(image);
        }

        private static Texture2D CreateMapIcon()
        {
            var image = Image.CreateEmpty(28, 28, false, Image.Format.Rgba8);
            var color = Colors.White;

            image.FillRect(new Rect2I(2, 2, 24, 3), color);
            image.FillRect(new Rect2I(2, 23, 24, 3), color);
            image.FillRect(new Rect2I(2, 2, 3, 24), color);
            image.FillRect(new Rect2I(23, 2, 3, 24), color);

            FillCircle(image, new Vector2I(14, 14), 5, color);

            return ImageTexture.CreateFromImage(image);
        }

        private static Texture2D CreateSkillTreeIcon()
        {
            var image = Image.CreateEmpty(28, 28, false, Image.Format.Rgba8);
            var color = Colors.White;

            image.FillRect(new Rect2I(4, 13, 20, 2), color);

            FillCircle(image, new Vector2I(6, 14), 4, color);
            FillCircle(image, new Vector2I(14, 14), 4, color);
            FillCircle(image, new Vector2I(22, 14), 4, color);

            return ImageTexture.CreateFromImage(image);
        }

        private static void FillCircle(Image image, Vector2I center, int radius, Color color)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y > radius * radius)
                    {
                        continue;
                    }

                    var px = center.X + x;
                    var py = center.Y + y;

                    if (px >= 0 && px < image.GetWidth() && py >= 0 && py < image.GetHeight())
                    {
                        image.SetPixel(px, py, color);
                    }
                }
            }
        }

        #endregion

        #region Core - Hotbar

        public void UpdateHotbar()
        {
            if (HotbarNormalStyle == null)
            {
                return;
            }

            if (LocalPlayer == null)
            {
                return;
            }

            if (LocalPlayer?.Inventory == null)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                var panel = HotbarSlotPanels[i];
                if (panel == null)
                {
                    continue;
                }

                var slot = LocalPlayer.GetSlot(i);
                var isSelected = slot != null && slot.InstanceId == LocalPlayer.EquippedItemId;
                var hotbarStyle = HotbarNormalStyle;

                if (isSelected)
                {
                    hotbarStyle = HotbarSelectedStyle;
                }

                panel.AddThemeStyleboxOverride("panel", hotbarStyle);

                var def = ItemFactory.Create(slot?.Id);
                var empty = def == null || slot == null;

                if (!empty && def?.Icon != null)
                {
                    HotbarIconRects[i].Texture = def.Icon;
                    HotbarNameLabels[i].Text = "";
                }
                else
                {
                    HotbarIconRects[i].Texture = null;
                    HotbarNameLabels[i].Text = empty ? "" : (def?.Name ?? "");
                }

                if (!empty && def?.Stackable == true && slot.Quantity > 1)
                {
                    HotbarQtyLabels[i].Text = $"x{slot.Quantity}";
                }
                else
                {
                    HotbarQtyLabels[i].Text = "";
                }
            }
        }

        #endregion
    }
}
