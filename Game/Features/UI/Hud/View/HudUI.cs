using Godot;
using Jogo25D.Actions;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Effects;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Session;
using Jogo25D.Systems;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.UI
{
    public partial class HudUI : ScreenUI
    {
        #region Node children references

        public MinimapUI Minimap { get; private set; }
        public Label FpsLabel { get; private set; }
        public ProgressBar LegacyHealthBar { get; private set; }
        public PanelContainer HealthBar { get; private set; }
        public PhysicalSizeTextureRect HealthBarBack { get; private set; }
        public RatioFillRect HealthBarFill { get; private set; }
        public HBoxContainer AbilitiesContainer { get; private set; }
        public Panel AbilityTemplate { get; private set; }
        public HBoxContainer EffectsContainer { get; private set; }
        public Panel EffectTemplate { get; private set; }
        public VBoxContainer HotkeysContainer { get; private set; }
        public Panel HotkeySlot0 { get; private set; }
        public HBoxContainer HotbarContainer { get; private set; }
        public Panel Slot0 { get; private set; }
        public Panel Slot0Selected { get; private set; }

        #endregion

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
        public Control[] HotbarSelectedMarkers { get; } = new Control[8];
        public List<Panel> AbilitySlots { get; } = new List<Panel>();
        public List<ProgressBar> AbilityFillBars { get; } = new List<ProgressBar>();
        public List<TextureRect> AbilityIconRects { get; } = new List<TextureRect>();
        public List<Label> AbilityInnerNameLabels { get; } = new List<Label>();
        public List<Label> AbilityTimerLabels { get; } = new List<Label>();
        public List<Label> AbilityChargesLabels { get; } = new List<Label>();
        public List<EffectSlotViews> EffectSlots { get; } = new List<EffectSlotViews>();
        public StyleBoxFlat HotbarNormalStyle { get; set; }
        public StyleBoxFlat HotbarSelectedStyle { get; set; }

        public override bool IsOverlay => false;

        private static readonly Color COR_ICONE = new Color(0.93f, 0.72f, 0.31f);
        private static readonly StringName VARIACAO_REDONDO = "SlotRound";
        private static readonly StringName VARIACAO_REDONDO_HOVER = "SlotRoundHover";

        #endregion

        #region Godot implementation

        public override void _Input(InputEvent inputEvent)
        {
            if (inputEvent is not InputEventKey tecla || !tecla.Pressed || tecla.Echo || tecla.Keycode != Key.F3)
            {
                return;
            }

            var fps = FpsLabel;

            if (fps != null)
            {
                fps.Visible = !fps.Visible;
            }

            GetViewport().SetInputAsHandled();
        }

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();

            SessionContext.WorldEntered += OnWorldEntered;
        }

        private void OnWorldEntered()
        {
            RouterContext.Open(this);
        }

        public override void _ExitTree()
        {
            SessionContext.WorldEntered -= OnWorldEntered;

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

            if (LocalPlayer != null && IsInstanceValid(LocalPlayer) && AbilitySlots.Count != CountAbilities())
            {
                BuildAbilitySlots();
            }

            UpdateAbilitySlots();
            UpdateEffectIcons();
            UpdateMiningHint();
        }

        #endregion

        #region Core - Setup

        private void ResolveChildren()
        {
            Minimap = GetNode<MinimapUI>("MarginContainer/TopRightColumn/MinimapPanel/Minimap");
            FpsLabel = GetNode<Label>("MarginContainer/VBoxContainer/FpsLabel");
            LegacyHealthBar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/HealthBlock/HealthFrame/HealthRow/LegacyHealthBar");
            HealthBar = GetNode<PanelContainer>("MarginContainer/VBoxContainer/HealthBar");
            HealthBarBack = GetNode<PhysicalSizeTextureRect>("MarginContainer/VBoxContainer/HealthBar/BarBack");
            HealthBarFill = GetNode<RatioFillRect>("MarginContainer/VBoxContainer/HealthBar/BarFill");
            AbilitiesContainer = GetNode<HBoxContainer>("MarginContainer/VBoxContainer/AbilitiesContainer");
            AbilityTemplate = GetNode<Panel>("MarginContainer/VBoxContainer/AbilitiesContainer/AbilityTemplate");
            EffectsContainer = GetNode<HBoxContainer>("MarginContainer/VBoxContainer/EffectsContainer");
            EffectTemplate = GetNode<Panel>("MarginContainer/VBoxContainer/EffectsContainer/EffectTemplate");
            HotkeysContainer = GetNode<VBoxContainer>("MarginContainer/TopRightColumn/HotkeysContainer");
            HotkeySlot0 = GetNode<Panel>("MarginContainer/TopRightColumn/HotkeysContainer/HotkeySlot0");
            HotbarContainer = GetNode<HBoxContainer>("MarginContainer/HotbarContainer");
            Slot0 = GetNode<Panel>("MarginContainer/HotbarContainer/Slot0");
            Slot0Selected = GetNode<Panel>("MarginContainer/HotbarContainer/Slot0Selected");
        }

        private void Initialize()
        {
            AbilityTemplate.Visible = false;

            var hotkeySlot0 = HotkeySlot0;

            ConfigureHotkeyHint(hotkeySlot0, "I", CreateInventoryIcon());
            ConfigureHotkeyHint(DuplicateHotkeySlot(hotkeySlot0), "M", CreateMapIcon());
            ConfigureHotkeyHint(DuplicateHotkeySlot(hotkeySlot0), "K", CreateSkillTreeIcon());

            MiningHint = DuplicateHotkeySlot(hotkeySlot0);

            ConfigureHotkeyHint(MiningHint, "V", GD.Load<Texture2D>(Textures.Items.PICKAXE_STARTING_ICON));

            MiningHint.Visible = false;

            EffectTemplate.Visible = false;

            var hotbarContainer = HotbarContainer;
            var slot0 = Slot0;
            var slot0Selected = Slot0Selected;

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
                HotbarSelectedMarkers[i] = panel.GetNodeOrNull<Control>("SelectedMarker");

                if (HotbarSelectedMarkers[i] != null)
                {
                    HotbarSelectedMarkers[i].Visible = false;
                }
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

                        FpsLabel.Text = $"{fpsText} | Ping: {CurrentPing}ms";
                    }
                    else
                    {
                        FpsLabel.Text = $"{fpsText} | Ping: 0ms";
                    }
                }
                catch
                {
                    FpsLabel.Text = fpsText;
                }
            }
            else
            {
                FpsLabel.Text = fpsText;
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
            var bar = LegacyHealthBar;

            bar.MaxValue = maxHealth;
            bar.Value = currentHealth;

            var width = UiConstants.HEALTH_BAR_BASE_WIDTH + maxHealth * UiConstants.HEALTH_BAR_PX_PER_HEALTH;

            bar.CustomMinimumSize = new Vector2(width, bar.CustomMinimumSize.Y);
        }

        private void LayoutHealthBar(int maxHealth, int currentHealth)
        {
            if (!HealthBarBaselineCaptured)
            {
                HealthBarBackBaselineSize = HealthBarBack.PhysicalSize;
                HealthBarBaselineCaptured = true;
            }

            var pxPerHealth = HealthBarBackBaselineSize.X / 50f;
            var growthPx = Mathf.Max(0f, maxHealth - 50f) * pxPerHealth;

            HealthBarBack.PhysicalSize = HealthBarBackBaselineSize + new Vector2(growthPx, 0f);

            HealthBarFill.Ratio = maxHealth > 0 ? Mathf.Clamp((float)currentHealth / maxHealth, 0f, 1f) : 0f;
        }

        public void FindLocalPlayer()
        {
            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.ItemEquipped -= OnItemEquipped;
                LocalPlayer.InventoryChanged -= UpdateHotbar;
            }

            LocalPlayer = Players.GetLocal();

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return;
            }

            if (Minimap != null && IsInstanceValid(Minimap))
            {
                Minimap.SetLocalPlayer(LocalPlayer);
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

        public int CountAbilities()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return 0;
            }

            return (LocalPlayer.UnlockedAbilities?.Count ?? 0) + (LocalPlayer.ActiveAbilities?.Count ?? 0);
        }

        public void BuildAbilitySlots()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || AbilitiesContainer == null || AbilityTemplateMissing)
            {
                return;
            }

            var list = Resolver.Resolve(LocalPlayer.UnlockedAbilities, LocalPlayer.ActiveAbilities);

            if (list == null || list.Count == 0)
            {
                AbilitySlots.Clear();
                AbilityFillBars.Clear();
                AbilityIconRects.Clear();
                AbilityInnerNameLabels.Clear();
                AbilityTimerLabels.Clear();
                AbilityChargesLabels.Clear();

                for (int i = AbilitiesContainer.GetChildCount() - 1; i >= 0; i--)
                {
                    if (AbilitiesContainer.GetChild(i) is Control c)
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

            for (int i = AbilitiesContainer.GetChildCount() - 1; i >= 0; i--)
            {
                var old = AbilitiesContainer.GetChild(i);

                if (old.Name == "AbilityTemplate")
                {
                    continue;
                }

                AbilitiesContainer.RemoveChild(old);

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

                AbilitiesContainer.AddChild(slotViews.Panel);

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
            var template = AbilityTemplate;

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

            var list = Resolver.Resolve(LocalPlayer.UnlockedAbilities, LocalPlayer.ActiveAbilities);

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
                    var usesCharges = def?.MaxCharges > 0;

                    chargesLabel.Text = usesCharges ? Mathf.Min(action.CurrentCharges, UiConstants.MAX_CHARGES_SHOWN).ToString() : "";
                    chargesLabel.Visible = usesCharges;
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
            var template = EffectTemplate;

            if (template == null)
            {
                GD.PushError("[HudUI.CreateEffectSlot] Template 'EffectTemplate' não encontrado em Hud.tscn");

                return null;
            }

            var panel = (Panel)template.Duplicate();

            panel.Visible = true;

            var iconRect = panel.GetNode<TextureRect>("IconRect");
            var timerLabel = panel.GetNode<Label>("TimerLabel");

            EffectsContainer.AddChild(panel);

            return new EffectSlotViews(panel, iconRect, timerLabel);
        }

        public void UpdateEffectIcons()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer) || EffectsContainer == null || EffectTemplateMissing)
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

            HotkeysContainer.AddChild(panel);

            foreach (var child in panel.GetChildren())
            {
                if (child is Control control)
                {
                    control.MouseFilter = Control.MouseFilterEnum.Ignore;
                }
            }

            panel.MouseEntered += () => panel.ThemeTypeVariation = VARIACAO_REDONDO_HOVER;
            panel.MouseExited += () => panel.ThemeTypeVariation = VARIACAO_REDONDO;

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
            var image = Image.CreateEmpty(22, 22, false, Image.Format.Rgba8);
            var color = COR_ICONE;

            image.FillRect(new Rect2I(2, 2, 8, 8), color);
            image.FillRect(new Rect2I(12, 2, 8, 8), color);
            image.FillRect(new Rect2I(2, 12, 8, 8), color);
            image.FillRect(new Rect2I(12, 12, 8, 8), color);

            return ImageTexture.CreateFromImage(image);
        }

        private static Texture2D CreateMapIcon()
        {
            var image = Image.CreateEmpty(22, 22, false, Image.Format.Rgba8);
            var color = COR_ICONE;

            image.FillRect(new Rect2I(1, 1, 20, 3), color);
            image.FillRect(new Rect2I(1, 18, 20, 3), color);
            image.FillRect(new Rect2I(1, 1, 3, 20), color);
            image.FillRect(new Rect2I(18, 1, 3, 20), color);

            FillCircle(image, new Vector2I(11, 11), 4, color);

            return ImageTexture.CreateFromImage(image);
        }

        private static Texture2D CreateSkillTreeIcon()
        {
            var image = Image.CreateEmpty(22, 22, false, Image.Format.Rgba8);
            var color = COR_ICONE;

            image.FillRect(new Rect2I(4, 10, 14, 2), color);

            FillCircle(image, new Vector2I(5, 11), 3, color);
            FillCircle(image, new Vector2I(11, 11), 3, color);
            FillCircle(image, new Vector2I(17, 11), 3, color);

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

                if (HotbarSelectedMarkers[i] != null)
                {
                    HotbarSelectedMarkers[i].Visible = isSelected;
                }

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
