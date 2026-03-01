using Godot;
using System;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Scripts.Actions;
using Jogo25D.Weapons;
using Jogo25D.Constants;

namespace Jogo25D.UI
{
	/// <summary>
	/// HUD unificado com FPS, Health, Weapon display e habilidades.
	/// </summary>
	public partial class HUD : CanvasLayer
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
		private readonly List<Label> abilityTimerLabels = new List<Label>();
		private readonly List<Label> abilityNameLabels = new List<Label>();
		private double pingTimer = 0.0;
		private double pingInterval = 1.0;
		private double lastPingSentTime = 0.0;
		private int currentPing = 0;

		private Minimap minimap;

		public override void _Ready()
		{
			fpsLabel = GetNode<Label>(NodePaths.Hud.FpsLabel);
			healthBar = GetNode<ProgressBar>(NodePaths.Hud.HealthBar);
			healthBarLabel = GetNode<Label>(NodePaths.Hud.HealthBarLabel);
			weaponLabel = GetNode<Label>(NodePaths.Hud.EquippedWeaponLabel);
			abilitiesContainer = GetNode<HBoxContainer>(NodePaths.Hud.AbilitiesContainer);
			minimap = GetNode<Minimap>(NodePaths.Hud.Minimap);

			CallDeferred(nameof(FindLocalPlayer));
		}
		public override void _ExitTree()
		{
			if (inventory != null && IsInstanceValid(inventory))
			{
				inventory.ItemEquipped -= OnItemEquipped;
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
					UpdateWeaponDisplay();
				}
			}
		}

		private void OnItemEquipped(Item item, int index)
		{
			UpdateWeaponDisplay();
		}

		private void UpdateWeaponDisplay()
		{
			if (inventory == null || !IsInstanceValid(inventory))
			{
				weaponLabel.Text = "Arma: Nenhuma";
				return;
			}

			var equippedItem = inventory.GetEquippedItem();

			if (equippedItem == null || (equippedItem.Type != ItemType.WeaponMelee && equippedItem.Type != ItemType.WeaponRanged))
			{
				weaponLabel.Text = "Arma: Nenhuma";
				return;
			}

			var weapon = localPlayer?.CurrentWeaponSystem;
			if (weapon != null && IsInstanceValid(weapon))
			{
				var reloadPrefix = weapon.IsReloading() ? $"{weapon.GetRemainingReloadTime():F1}s " : "";

				if (weapon.InfiniteCharges)
				{
					weaponLabel.Text = $"{reloadPrefix}{weapon.WeaponName} ∞";
				}
				else
				{
					weaponLabel.Text = $"{reloadPrefix}{weapon.WeaponName} {weapon.CurrentCharges}/{weapon.InventoryCharges}";
				}
			}
			else
			{
				weaponLabel.Text = equippedItem.ItemName;
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
				abilityTimerLabels.Clear();
				abilityNameLabels.Clear();
				for (int i = abilitiesContainer.GetChildCount() - 1; i >= 0; i--)
				{
					if (abilitiesContainer.GetChild(i) is Control c)
						c.Visible = false;
				}
				return;
			}

			abilitySlots.Clear();
			abilityFillBars.Clear();
			abilityTimerLabels.Clear();
			abilityNameLabels.Clear();

			int existingChildren = abilitiesContainer.GetChildCount();

			for (int i = 0; i < list.Count; i++)
			{
				Panel slot;
				ProgressBar fillBar;
				Label timerLabel;
				Label nameLabel;

				if (i < existingChildren)
				{
					var child = abilitiesContainer.GetChild(i);
					if (child is VBoxContainer vbox)
					{
						slot = vbox.GetNodeOrNull<Panel>(NodePaths.Hud.AbilityPanelName);
						if (slot == null)
							slot = vbox.GetChild<Panel>(0);
						fillBar = slot.GetNodeOrNull<ProgressBar>(NodePaths.Hud.AbilityCooldownFillName);
						timerLabel = slot.GetNodeOrNull<Label>(NodePaths.Hud.AbilityTimerLabelName);
						nameLabel = vbox.GetNodeOrNull<Label>(NodePaths.Hud.AbilityNameLabelName);
						if (nameLabel == null)
						{
							nameLabel = CreateAbilityNameLabel();
							nameLabel.Name = NodePaths.Hud.AbilityNameLabelName;
							vbox.AddChild(nameLabel);
						}
					}
					else if (child is Panel panel)
					{
						slot = panel;
						slot.Name = NodePaths.Hud.AbilityPanelName;
						fillBar = panel.GetNodeOrNull<ProgressBar>(NodePaths.Hud.AbilityCooldownFillName);
						timerLabel = panel.GetNodeOrNull<Label>(NodePaths.Hud.AbilityTimerLabelName);
						var wrapper = new VBoxContainer();
						abilitiesContainer.RemoveChild(panel);
						wrapper.AddChild(panel);
						nameLabel = CreateAbilityNameLabel();
						nameLabel.Name = "AbilityNameLabel";
						wrapper.AddChild(nameLabel);
						abilitiesContainer.AddChild(wrapper);
						abilitiesContainer.MoveChild(wrapper, i);
					}
					else
					{
						var slotViews = CreateAbilitySlot();
						abilitiesContainer.AddChild(slotViews.Wrapper);
						slot = slotViews.Panel;
						fillBar = slotViews.FillBar;
						timerLabel = slotViews.TimerLabel;
						nameLabel = slotViews.NameLabel;
					}
					slot.Visible = true;
					fillBar ??= slot.GetNodeOrNull<ProgressBar>("CooldownFill");
					timerLabel ??= slot.GetNodeOrNull<Label>("TimerLabel");
					if (timerLabel == null)
					{
						timerLabel = CreateTimerLabel();
						slot.AddChild(timerLabel);
					}
				}
				else
				{
					var slotViews = CreateAbilitySlot();
					abilitiesContainer.AddChild(slotViews.Wrapper);
					slot = slotViews.Panel;
					fillBar = slotViews.FillBar;
					timerLabel = slotViews.TimerLabel;
					nameLabel = slotViews.NameLabel;
				}

				fillBar.MinValue = 0;
				fillBar.MaxValue = 1;
				fillBar.Value = 1;
				fillBar.FillMode = (int)ProgressBar.FillModeEnum.BottomToTop;

				abilitySlots.Add(slot);
				abilityFillBars.Add(fillBar);
				abilityTimerLabels.Add(timerLabel);
				abilityNameLabels.Add(nameLabel);
			}

			// Remove slots extras do editor (se tinha mais que a lista)
			while (abilitiesContainer.GetChildCount() > list.Count)
			{
				var extra = abilitiesContainer.GetChild(abilitiesContainer.GetChildCount() - 1);
				abilitiesContainer.RemoveChild(extra);
				extra.QueueFree();
			}
		}

		private AbilitySlotViews CreateAbilitySlot()
		{
			var panel = new Panel();
			panel.Name = NodePaths.Hud.AbilityPanelName;
			panel.CustomMinimumSize = new Vector2(48, 48);

			var styleBg = new StyleBoxFlat();
			styleBg.BgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
			styleBg.BorderWidthLeft = styleBg.BorderWidthTop = styleBg.BorderWidthRight = styleBg.BorderWidthBottom = 2;
			styleBg.BorderColor = new Color(0.4f, 0.4f, 0.5f);
			styleBg.SetCornerRadiusAll(4);
			panel.AddThemeStyleboxOverride("panel", styleBg);

			var fill = new ProgressBar();
			fill.Name = NodePaths.Hud.AbilityCooldownFillName;
			fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			fill.OffsetLeft = 4;
			fill.OffsetTop = 4;
			fill.OffsetRight = -4;
			fill.OffsetBottom = -4;
			fill.ShowPercentage = false;
			fill.MinValue = 0;
			fill.MaxValue = 1;
			fill.Value = 1;
			fill.FillMode = (int)ProgressBar.FillModeEnum.BottomToTop;

			var styleFill = new StyleBoxFlat();
			styleFill.BgColor = new Color(0.25f, 0.6f, 0.9f, 0.9f);
			styleFill.SetCornerRadiusAll(2);
			fill.AddThemeStyleboxOverride("fill", styleFill);

			panel.AddChild(fill);
			var timerLabel = CreateTimerLabel();
			panel.AddChild(timerLabel);

			var nameLabel = CreateAbilityNameLabel();
			nameLabel.Name = NodePaths.Hud.AbilityNameLabelName;

			var wrapper = new VBoxContainer();
			wrapper.AddChild(panel);
			wrapper.AddChild(nameLabel);

			return new AbilitySlotViews(wrapper, panel, fill, timerLabel, nameLabel);
		}

		private Label CreateAbilityNameLabel()
		{
			var label = new Label();
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", Colors.White);
			label.AddThemeColorOverride("font_outline_color", Colors.Black);
			label.AddThemeConstantOverride("outline_size", 1);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.Text = "";
			return label;
		}

		private Label CreateTimerLabel()
		{
			var label = new Label();
			label.Name = NodePaths.Hud.AbilityTimerLabelName;
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
			if (list == null || abilityFillBars.Count != list.Count || abilityTimerLabels.Count != list.Count || abilityNameLabels.Count != list.Count)
				return;

			for (int i = 0; i < list.Count && i < abilityFillBars.Count; i++)
			{
				var action = list[i];
				var bar = abilityFillBars[i];
				var timerLabel = abilityTimerLabels[i];
				var nameLabel = abilityNameLabels[i];
				if (action == null || bar == null)
					continue;

				float value;
				string timerText;
				Color fillColor;

				if (action.IsActive)
				{
					value = 1f - action.GetDurationProgress();
					fillColor = Colors.White;
					timerText = $"{action.GetRemainingDuration():F1}s";
				}
				else if (action.InCooldown)
				{
					value = action.GetCooldownProgress();
					fillColor = new Color(0.4f, 0.4f, 0.45f, 0.9f); // cinza no CD
					timerText = $"{action.GetRemainingCooldown():F1}s";
				}
				else
				{
					value = 1f;
					fillColor = new Color(0.25f, 0.5f, 0.9f, 0.95f); // azul quando carregado
					timerText = "";
				}

				bar.Value = value;

				var styleFill = (StyleBoxFlat)bar.GetThemeStylebox("fill").Duplicate();
				styleFill.BgColor = fillColor;
				bar.AddThemeStyleboxOverride("fill", styleFill);

				if (timerLabel != null)
				{
					timerLabel.Text = timerText;
					timerLabel.Visible = !string.IsNullOrEmpty(timerText);
				}

				if (nameLabel != null)
				{
					var nameText = action.MaxCharges > 1 ? $"{action.ActionName} {action.CurrentCharges}/{action.MaxCharges}" : action.ActionName;
					nameLabel.Text = nameText;
				}
			}
		}

		#endregion
	}
}
