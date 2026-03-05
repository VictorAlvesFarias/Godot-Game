using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Scripts.Actions;

namespace Jogo25D.UI
{
	public partial class HudUI : CanvasLayer
	{
		public string PlayerGroupName { get; set; } = "players";

		private Label fpsLabel;
		private ProgressBar healthBar;
		private Label healthBarLabel;
		private Label weaponLabel;
		private HBoxContainer abilitiesContainer;
		private Inventory inventory;
		private Player localPlayer;
		private readonly List<Panel> abilitySlots = new List<Panel>();
		private readonly List<ProgressBar> abilityFillBars = new List<ProgressBar>();
		private readonly List<TextureRect> abilityIconRects = new List<TextureRect>();
		private readonly List<Label> abilityInnerNameLabels = new List<Label>();
		private readonly List<Label> abilityTimerLabels = new List<Label>();
		private readonly List<Label> abilityChargesLabels = new List<Label>();
		private double pingTimer = 0.0;
		private double pingInterval = 1.0;
		private double lastPingSentTime = 0.0;
		private int currentPing = 0;

		private MinimapUI minimap;

		private const int HotbarSize = 8;
		private readonly Panel[] _hotbarSlotPanels = new Panel[HotbarSize];
		private readonly TextureRect[] _hotbarIconRects = new TextureRect[HotbarSize];
		private readonly Label[] _hotbarNameLabels = new Label[HotbarSize];
		private readonly Label[] _hotbarQtyLabels = new Label[HotbarSize];
		private StyleBoxFlat _hotbarNormalStyle;
		private StyleBoxFlat _hotbarSelectedStyle;

		public override void _Ready()
		{
			fpsLabel = GetNode<Label>("MarginContainer/VBoxContainer/FpsLabel");
			healthBar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/HealthBar");
			healthBarLabel = GetNode<Label>("MarginContainer/VBoxContainer/HealthBar/HealthBarLabel");
			weaponLabel = GetNode<Label>("MarginContainer/VBoxContainer/EquippedWeaponLabel");
			abilitiesContainer = GetNode<HBoxContainer>("MarginContainer/VBoxContainer/AbilitiesContainer");
			minimap = GetNode<MinimapUI>("MarginContainer/MinimapPanel/Minimap");

			var hotbarContainer = GetNode<HBoxContainer>("MarginContainer/HotbarContainer");
			var slot0 = hotbarContainer.GetNode<Panel>("Slot0");

			_hotbarNormalStyle   = slot0.GetThemeStylebox("panel") as StyleBoxFlat;
			_hotbarSelectedStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.12f, 0.12f, 0.18f, 0.96f),
				BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
				BorderColor = new Color(1f, 0.85f, 0.1f, 1f),
			};
			_hotbarSelectedStyle.SetCornerRadiusAll(3);

			for (int i = 0; i < HotbarSize; i++)
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

				_hotbarSlotPanels[i] = panel;
				_hotbarIconRects[i]  = panel.GetNode<TextureRect>("MarginContainer/CenterContainer/IconRect");
				_hotbarNameLabels[i] = panel.GetNode<Label>("MarginContainer/CenterContainer/NameLabel");
				_hotbarQtyLabels[i]  = panel.GetNode<Label>("QtyLabel");
			}
			CallDeferred(nameof(FindLocalPlayer));
		}
		public override void _ExitTree()
		{
			if (inventory != null && IsInstanceValid(inventory))
			{
				inventory.ItemEquipped -= OnItemEquipped;
				inventory.InventoryChanged -= UpdateHotbar;
			}
		}
		public override void _Process(double delta)
		{
			UpdateFpsDisplay(delta);
			UpdateHealthDisplay();
			UpdateWeaponDisplay();

			if (localPlayer != null && IsInstanceValid(localPlayer) && abilitySlots.Count == 0)
				BuildAbilitySlots();

			UpdateAbilitySlots();
			UpdateHotbar();
		}

		#region FPS Display

		private void UpdateFpsDisplay(double delta)
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
						pingTimer += delta;

						if (pingTimer >= pingInterval)
						{
							pingTimer = 0.0;
							lastPingSentTime = Time.GetTicksMsec();
							RpcId(1, nameof(PingPong));
						}

						fpsLabel.Text = $"{fpsText} | Ping: {currentPing}ms";
					}
					else
					{
						fpsLabel.Text = $"{fpsText} | Ping: 0ms";
					}
				}
				catch
				{
					fpsLabel.Text = fpsText;
				}
			}
			else
			{
				fpsLabel.Text = fpsText;
			}
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
		private void PingPong()
		{
			if (Multiplayer.IsServer())
			{
				var senderId = Multiplayer.GetRemoteSenderId();

				RpcId(senderId, nameof(ReceivePong));
			}
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
		private void ReceivePong()
		{
			double now = Time.GetTicksMsec();

			currentPing = (int)(now - lastPingSentTime);
		}

		#endregion

		#region Health Display

		private void UpdateHealthDisplay()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer))
			{
				FindLocalPlayer();
			}

			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				healthBar.MaxValue = localPlayer.MaxHealth;
				healthBar.Value = localPlayer.CurrentHealth;
				healthBarLabel.Text = $"{localPlayer.CurrentHealth}/{localPlayer.MaxHealth}";

				var barWidth = localPlayer.MaxHealth * 10f;

				healthBar.CustomMinimumSize = new Vector2(barWidth, 30);
			}
			else
			{
				healthBar.Value = 0;
				healthBarLabel.Text = "0/0";
				healthBar.CustomMinimumSize = new Vector2(100, 30);
			}
		}

		private void FindLocalPlayer()
		{
			var players = GetTree().GetNodesInGroup("players");
			var localPeerId = 1;
			var hasMultiplayer = false;

			if (Multiplayer != null && Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
			{
				try
				{
					localPeerId = Multiplayer.GetUniqueId();
					hasMultiplayer = true;
				}
				catch
				{
					hasMultiplayer = false;
				}
			}

			foreach (Node node in players)
			{
				if (node is Player player)
				{
					if (!hasMultiplayer || player.GetMultiplayerAuthority() == localPeerId)
					{
						localPlayer = player;

						if (minimap != null && IsInstanceValid(minimap))
						{
							minimap.SetLocalPlayer(player);
						}

						FindLocalPlayerInventory();

						break;
					}
				}
			}
		}

		#endregion

		#region Weapon Display

		private void FindLocalPlayerInventory()
		{
			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				inventory = localPlayer.Inventory;
				if (inventory != null && IsInstanceValid(inventory))
				{
					inventory.ItemEquipped += OnItemEquipped;
					inventory.InventoryChanged += UpdateHotbar;
					UpdateWeaponDisplay();
					UpdateHotbar();
				}
			}
		}

		private void OnItemEquipped(int slotIndex)
		{
			UpdateWeaponDisplay();
		}

		private void UpdateWeaponDisplay()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer))
			{
				weaponLabel.Text = "Arma: Nenhuma";
				return;
			}

			var instance = localPlayer.EquippedInstance;
			if (instance == null || instance.IsEmpty() || instance.Definition is not WeaponDefinition)
			{
				weaponLabel.Text = "Arma: Nenhuma";
				return;
			}

			var chargesProp  = instance.Properties.OfType<ChargesProperty>().FirstOrDefault();
			var def          = instance.Definition;
			var reloadPrefix = instance.IsReloading ? $"{instance.GetRemainingReloadTime():F1}s " : "";

			if (chargesProp == null || chargesProp.InfiniteCharges)
			{
				weaponLabel.Text = $"{reloadPrefix}{def.Name} ∞";
			}
			else
			{
				int ammo = inventory?.CountAmmoByChargeType(chargesProp.ChargeType) ?? 0;
				weaponLabel.Text = $"{reloadPrefix}{def.Name} {instance.CurrentCharges}/{ammo}";
			}
		}

		#endregion

		#region Ability slots

		private void BuildAbilitySlots()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer) || abilitiesContainer == null)
				return;

			var list = localPlayer.UnlockedAbilities;
			if (list == null || list.Count == 0)
			{
				abilitySlots.Clear();
				abilityFillBars.Clear();
				abilityIconRects.Clear();
				abilityInnerNameLabels.Clear();
				abilityTimerLabels.Clear();
				abilityChargesLabels.Clear();
				for (int i = abilitiesContainer.GetChildCount() - 1; i >= 0; i--)
				{
					if (abilitiesContainer.GetChild(i) is Control c)
						c.Visible = false;
				}
				return;
			}

			abilitySlots.Clear();
			abilityFillBars.Clear();
			abilityIconRects.Clear();
			abilityInnerNameLabels.Clear();
			abilityTimerLabels.Clear();
			abilityChargesLabels.Clear();

			// Remove filhos extras antes de recriar
			while (abilitiesContainer.GetChildCount() > 0)
			{
				var old = abilitiesContainer.GetChild(0);
				abilitiesContainer.RemoveChild(old);
				old.QueueFree();
			}

			for (int i = 0; i < list.Count; i++)
			{
				var slotViews = CreateAbilitySlot();
				abilitiesContainer.AddChild(slotViews.Panel);

				var fillBar = slotViews.FillBar;
				fillBar.MinValue = 0;
				fillBar.MaxValue = 1;
				fillBar.Value = 0;
				fillBar.FillMode = (int)ProgressBar.FillModeEnum.TopToBottom;

				abilitySlots.Add(slotViews.Panel);
				abilityFillBars.Add(fillBar);
				abilityIconRects.Add(slotViews.IconRect);
				abilityInnerNameLabels.Add(slotViews.InnerNameLabel);
				abilityTimerLabels.Add(slotViews.TimerLabel);
				abilityChargesLabels.Add(slotViews.ChargesLabel);
			}
		}

		private AbilitySlotViews CreateAbilitySlot()
		{
			var panel = new Panel();
			panel.Name = "AbilityPanel";
			panel.CustomMinimumSize = new Vector2(48, 48);

			var styleBg = new StyleBoxFlat();
			styleBg.BgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
			styleBg.BorderWidthLeft = styleBg.BorderWidthTop = styleBg.BorderWidthRight = styleBg.BorderWidthBottom = 2;
			styleBg.BorderColor = new Color(0.4f, 0.4f, 0.5f);
			styleBg.SetCornerRadiusAll(4);
			panel.AddThemeStyleboxOverride("panel", styleBg);

			// MarginContainer > CenterContainer > IconRect + InnerNameLabel
			var margin = new MarginContainer();
			margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			margin.AddThemeConstantOverride("margin_left", 4);
			margin.AddThemeConstantOverride("margin_top", 4);
			margin.AddThemeConstantOverride("margin_right", 4);
			margin.AddThemeConstantOverride("margin_bottom", 4);
			margin.MouseFilter = Control.MouseFilterEnum.Ignore;
			panel.AddChild(margin);

			var center = new CenterContainer();
			center.MouseFilter = Control.MouseFilterEnum.Ignore;
			margin.AddChild(center);

			var iconRect = new TextureRect();
			iconRect.Name = "IconRect";
			iconRect.CustomMinimumSize = new Vector2(40, 40);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
			center.AddChild(iconRect);

			var innerNameLabel = new Label();
			innerNameLabel.Name = "NameLabel";
			innerNameLabel.AddThemeFontSizeOverride("font_size", 8);
			innerNameLabel.AddThemeColorOverride("font_color", Colors.White);
			innerNameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
			innerNameLabel.AddThemeConstantOverride("outline_size", 1);
			innerNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			innerNameLabel.VerticalAlignment = VerticalAlignment.Center;
			innerNameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
			innerNameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			innerNameLabel.Visible = false;
			center.AddChild(innerNameLabel);

			// Overlay preto de cooldown — fica ACIMA do ícone, abaixo do timer
			var fill = new ProgressBar();
			fill.Name = "CooldownFill";
			fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			fill.ShowPercentage = false;
			fill.MinValue = 0;
			fill.MaxValue = 1;
			fill.Value = 0;
			fill.FillMode = (int)ProgressBar.FillModeEnum.TopToBottom;
			fill.Visible = false;
			fill.MouseFilter = Control.MouseFilterEnum.Ignore;

			var styleBgTransparent = new StyleBoxFlat();
			styleBgTransparent.BgColor = new Color(0, 0, 0, 0);
			fill.AddThemeStyleboxOverride("background", styleBgTransparent);

			var styleFillBlack = new StyleBoxFlat();
			styleFillBlack.BgColor = new Color(0, 0, 0, 0.65f);
			fill.AddThemeStyleboxOverride("fill", styleFillBlack);

			panel.AddChild(fill);

			var timerLabel = CreateTimerLabel();
			panel.AddChild(timerLabel);

			// Cargas (QtyLabel) — canto inferior direito, igual ao hotbar
			var chargesLabel = new Label();
			chargesLabel.Name = "QtyLabel";
			chargesLabel.LayoutMode = 1;
			chargesLabel.AnchorLeft = 1;
			chargesLabel.AnchorTop = 1;
			chargesLabel.AnchorRight = 1;
			chargesLabel.AnchorBottom = 1;
			chargesLabel.OffsetLeft = -33;
			chargesLabel.OffsetTop = -16;
			chargesLabel.OffsetRight = -3;
			chargesLabel.OffsetBottom = -3;
			chargesLabel.GrowHorizontal = Control.GrowDirection.Begin;
			chargesLabel.GrowVertical = Control.GrowDirection.Begin;
			chargesLabel.HorizontalAlignment = HorizontalAlignment.Right;
			chargesLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.3f, 1f));
			chargesLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
			chargesLabel.AddThemeConstantOverride("outline_size", 2);
			chargesLabel.AddThemeFontSizeOverride("font_size", 10);
			chargesLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			panel.AddChild(chargesLabel);

			return new AbilitySlotViews(panel, fill, iconRect, innerNameLabel, timerLabel, chargesLabel);
		}

		private Label CreateTimerLabel()
		{
			var label = new Label();
			label.Name = "TimerLabel";
			label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", Colors.White);
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
			label.AddThemeConstantOverride("outline_size", 2);
			label.Text = "";
			label.MouseFilter = Control.MouseFilterEnum.Ignore;
			return label;
		}

		private void UpdateAbilitySlots()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer))
				return;

			var list = localPlayer.UnlockedAbilities;
			if (list == null || abilityFillBars.Count != list.Count)
				return;

			for (int i = 0; i < list.Count && i < abilityFillBars.Count; i++)
			{
				var action = list[i];
				var bar = abilityFillBars[i];
				var iconRect = i < abilityIconRects.Count ? abilityIconRects[i] : null;
				var innerNameLabel = i < abilityInnerNameLabels.Count ? abilityInnerNameLabels[i] : null;
				var timerLabel = i < abilityTimerLabels.Count ? abilityTimerLabels[i] : null;
				var chargesLabel = i < abilityChargesLabels.Count ? abilityChargesLabels[i] : null;
				if (action == null || bar == null)
					continue;

				// Ícone ou nome inline
				if (iconRect != null)
				{
					if (action.Icon != null)
					{
						iconRect.Texture = action.Icon;
						iconRect.Visible = true;
						if (innerNameLabel != null) innerNameLabel.Visible = false;
					}
					else
					{
						iconRect.Texture = null;
						iconRect.Visible = false;
						if (innerNameLabel != null)
						{
							innerNameLabel.Text = action.ActionName;
							innerNameLabel.Visible = true;
						}
					}
				}

				// Cargas — canto inferior direito
				if (chargesLabel != null)
					chargesLabel.Text = action.MaxCharges > 1 ? $"x{action.CurrentCharges}" : "";

				// Overlay de cooldown — prioriza InCooldown para evitar salto visual ao reusar
				if (action.InCooldown)
				{
					bar.Value = 1f - action.GetCooldownProgress();
					bar.Visible = true;
					if (timerLabel != null)
					{
						timerLabel.Text = action.IsActive
							? $"{action.GetRemainingDuration():F1}s"
							: $"{action.GetRemainingCooldown():F1}s";
						timerLabel.Visible = true;
					}
				}
				else if (action.IsActive)
				{
					bar.Value = 1f;
					bar.Visible = true;
					if (timerLabel != null) { timerLabel.Text = $"{action.GetRemainingDuration():F1}s"; timerLabel.Visible = true; }
				}
				else
				{
					bar.Value = 0;
					bar.Visible = false;
					if (timerLabel != null) { timerLabel.Text = ""; timerLabel.Visible = false; }
				}
			}
		}

		#endregion

		#region Hotbar

		private void UpdateHotbar()
		{
			if (_hotbarNormalStyle == null) return;

			int equippedIndex = inventory?.GetEquippedSlotIndex() ?? -1;

			for (int i = 0; i < HotbarSize; i++)
			{
				var panel = _hotbarSlotPanels[i];
				if (panel == null) continue;

				bool isSelected = i == equippedIndex;
				panel.AddThemeStyleboxOverride("panel",
					 isSelected ? _hotbarSelectedStyle : _hotbarNormalStyle);

				var slot  = inventory?.GetSlot(i);
				bool empty = slot == null || slot.IsEmpty();

				if (!empty && slot.Definition?.Icon != null)
				{
					_hotbarIconRects[i].Texture  = slot.Definition.Icon;
					_hotbarNameLabels[i].Text    = "";
				}
				else
				{
					_hotbarIconRects[i].Texture  = null;
					_hotbarNameLabels[i].Text    = empty ? "" : (slot.Definition?.Name ?? "");
				}

				_hotbarQtyLabels[i].Text = (!empty && slot.Definition?.Stackable == true && slot.Quantity > 1)
					? $"x{slot.Quantity}" : "";
			}
		}

		#endregion
	}
}
