using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;
using Jogo25D.Properties;
using Jogo25D.Actions;
using Jogo25D.Effects;
using Jogo25D.Features.World.Resolver.Singletons;

namespace Jogo25D.UI
{
	public partial class HudUI : CanvasLayer
	{
		#region Properties

		public string PlayerGroupName { get; set; } = "players";

		public Label fpsLabel;
		public ProgressBar healthBar;
		public HBoxContainer abilitiesContainer;
		public HBoxContainer effectsContainer;
		public Player localPlayer;
		public readonly List<Panel> abilitySlots = new List<Panel>();
		public readonly List<ProgressBar> abilityFillBars = new List<ProgressBar>();
		public readonly List<TextureRect> abilityIconRects = new List<TextureRect>();
		public readonly List<Label> abilityInnerNameLabels = new List<Label>();
		public readonly List<Label> abilityTimerLabels = new List<Label>();
		public readonly List<Label> abilityChargesLabels = new List<Label>();
		public readonly List<EffectSlotViews> effectSlots = new List<EffectSlotViews>();
		public double pingTimer = 0.0;
		public double pingInterval = 1.0;
		public double lastPingSentTime = 0.0;
		public int currentPing = 0;

		public MinimapUI minimap;

		public const int HotbarSize = 8;
		public readonly Panel[] _hotbarSlotPanels = new Panel[HotbarSize];
		public readonly TextureRect[] _hotbarIconRects = new TextureRect[HotbarSize];
		public readonly Label[] _hotbarNameLabels = new Label[HotbarSize];
		public readonly Label[] _hotbarQtyLabels = new Label[HotbarSize];
		public StyleBoxFlat _hotbarNormalStyle;
		public StyleBoxFlat _hotbarSelectedStyle;

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			fpsLabel = GetNode<Label>("MarginContainer/VBoxContainer/FpsLabel");
			healthBar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/HealthBar");
			abilitiesContainer = GetNode<HBoxContainer>("MarginContainer/VBoxContainer/AbilitiesContainer");
			minimap = GetNode<MinimapUI>("MarginContainer/MinimapPanel/Minimap");

			effectsContainer = new HBoxContainer();
			effectsContainer.AddThemeConstantOverride("separation", 6);
			abilitiesContainer.GetParent().AddChild(effectsContainer);

			var hotbarContainer = GetNode<HBoxContainer>("MarginContainer/HotbarContainer");
			var slot0 = hotbarContainer.GetNode<Panel>("Slot0");

			_hotbarNormalStyle = UISlotStyle.CreateDefault();

			slot0.AddThemeStyleboxOverride("panel", _hotbarNormalStyle);

			_hotbarSelectedStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.12f, 0.12f, 0.18f, 0.96f),
				BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
				BorderColor = new Color(1f, 0.85f, 0.1f, 1f),
			};

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
				_hotbarIconRects[i] = panel.GetNode<TextureRect>("MarginContainer/CenterContainer/IconRect");
				_hotbarNameLabels[i] = panel.GetNode<Label>("MarginContainer/CenterContainer/NameLabel");
				_hotbarQtyLabels[i] = panel.GetNode<Label>("QtyLabel");
			}
			CallDeferred(nameof(FindLocalPlayer));
		}
		public override void _ExitTree()
		{
			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				localPlayer.ItemEquipped -= OnItemEquipped;
				localPlayer.InventoryChanged -= UpdateHotbar;
			}
		}
		public override void _Process(double delta)
		{
			UpdateFpsDisplay(delta);
			UpdateHealthDisplay();

			if (localPlayer != null && IsInstanceValid(localPlayer) && abilitySlots.Count == 0)
			{
				BuildAbilitySlots();
			}

			UpdateAbilitySlots();
			UpdateEffectIcons();
			//UpdateHotbar();
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

			currentPing = (int)(now - lastPingSentTime);
		}

		#endregion

		#region Core - Health

		public void UpdateHealthDisplay()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer))
			{
				FindLocalPlayer();
			}

			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				var maxHealth = localPlayer.GetMaxHealth();

				healthBar.MaxValue = maxHealth;
				healthBar.Value = localPlayer.Data.CurrentHealth;

				var barWidth = maxHealth * 10f;

				healthBar.CustomMinimumSize = new Vector2(barWidth, 30);
			}
			else
			{
				healthBar.Value = 0;
				healthBar.CustomMinimumSize = new Vector2(100, 30);
			}
		}

		public void FindLocalPlayer()
		{
			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				localPlayer.ItemEquipped -= OnItemEquipped;
				localPlayer.InventoryChanged -= UpdateHotbar;
			}

			var worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

			localPlayer = worldManager?.GetLocalPlayer();

			if (localPlayer == null || !IsInstanceValid(localPlayer))
			{
				return;
			}

			if (minimap != null && IsInstanceValid(minimap))
			{
				minimap.SetLocalPlayer(localPlayer);
			}

			localPlayer.ItemEquipped += OnItemEquipped;
			localPlayer.InventoryChanged += UpdateHotbar;

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
			if (localPlayer == null || !IsInstanceValid(localPlayer) || abilitiesContainer == null)
			{
				return;
			}

			var list = localPlayer.GetAllUnlockedAbilities();

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
					{
						c.Visible = false;
					}
				}

				return;
			}

			abilitySlots.Clear();
			abilityFillBars.Clear();
			abilityIconRects.Clear();
			abilityInnerNameLabels.Clear();
			abilityTimerLabels.Clear();
			abilityChargesLabels.Clear();

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

		public AbilitySlotViews CreateAbilitySlot()
		{
			var panel = new Panel();

			panel.Name = "AbilityPanel";
			panel.CustomMinimumSize = new Vector2(48, 48);

			panel.AddThemeStyleboxOverride("panel", UISlotStyle.CreateDefault());

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
			iconRect.TextureFilter = Control.TextureFilterEnum.Nearest;

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

		public Label CreateTimerLabel()
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

		public void UpdateAbilitySlots()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer))
			{
				return;
			}

			var list = localPlayer.GetAllUnlockedAbilities();

			if (list == null || abilityFillBars.Count != list.Count)
			{
				return;
			}

			for (int i = 0; i < list.Count && i < abilityFillBars.Count; i++)
			{
				var action = list[i];
				var bar = abilityFillBars[i];
				var iconRect = i < abilityIconRects.Count ? abilityIconRects[i] : null;
				var innerNameLabel = i < abilityInnerNameLabels.Count ? abilityInnerNameLabels[i] : null;
				var timerLabel = i < abilityTimerLabels.Count ? abilityTimerLabels[i] : null;
				var chargesLabel = i < abilityChargesLabels.Count ? abilityChargesLabels[i] : null;

				if (action == null || bar == null)
				{
					continue;
				}

				var def = ActionDB.Get(action.Id);
				
				if (iconRect != null)
				{
					if (def?.Icon != null)
					{
						iconRect.Texture = def.Icon ;
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
			var panel = new Panel();

			panel.CustomMinimumSize = new Vector2(32, 32);

			panel.AddThemeStyleboxOverride("panel", UISlotStyle.CreateDefault());

			var iconRect = new TextureRect();

			iconRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.TextureFilter = Control.TextureFilterEnum.Nearest;
			iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;

			panel.AddChild(iconRect);

			var timerLabel = new Label();

			timerLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			timerLabel.HorizontalAlignment = HorizontalAlignment.Center;
			timerLabel.VerticalAlignment = VerticalAlignment.Center;
			timerLabel.AddThemeFontSizeOverride("font_size", 11);
			timerLabel.AddThemeColorOverride("font_color", Colors.White);
			timerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
			timerLabel.AddThemeConstantOverride("outline_size", 2);
			timerLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

			panel.AddChild(timerLabel);

			effectsContainer.AddChild(panel);

			return new EffectSlotViews(panel, iconRect, timerLabel);
		}

		public void UpdateEffectIcons()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer) || effectsContainer == null)
			{
				return;
			}

			var effects = localPlayer.GetAllCurrentEffects();

			while (effectSlots.Count < effects.Count)
			{
				effectSlots.Add(CreateEffectSlot());
			}

			while (effectSlots.Count > effects.Count)
			{
				var last = effectSlots[^1];

				effectSlots.RemoveAt(effectSlots.Count - 1);
				last.Panel.QueueFree();
			}

			for (int i = 0; i < effects.Count; i++)
			{
				var def = EffectDB.Get(effects[i].Id);
				var slot = effectSlots[i];

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

		#region Core - Hotbar

		public void UpdateHotbar()
		{
			if (_hotbarNormalStyle == null)
			{
			    return;
			}

			if (localPlayer == null)
			{
				return;
			}

			if (localPlayer.Data?.Inventory == null)
			{
				return;
			}


			for (int i = 0; i < HotbarSize; i++)
			{
				var panel = _hotbarSlotPanels[i];
				if (panel == null)
				{
				    continue;
				}

				var slot = localPlayer.GetSlot(i);
				var isSelected = slot != null && slot.InstanceId == localPlayer.Data.EquippedItemId;
				var hotbarStyle = _hotbarNormalStyle;


                if (isSelected)
				{
					hotbarStyle = _hotbarSelectedStyle;
                }

				panel.AddThemeStyleboxOverride("panel", hotbarStyle);

				var def = ItemDB.Get(slot?.Id);
				var empty = def == null || slot == null;

                if (!empty && def?.Icon != null)
				{
					_hotbarIconRects[i].Texture = def.Icon;
					_hotbarNameLabels[i].Text = "";
				}
				else
				{
					_hotbarIconRects[i].Texture = null;
					_hotbarNameLabels[i].Text = empty ? "" : (def?.Name ?? "");
				}

				if (!empty && def?.Stackable == true && slot.Quantity > 1)
				{
					_hotbarQtyLabels[i].Text = $"x{slot.Quantity}";
                }
				else
				{
					_hotbarQtyLabels[i].Text = "";
				}
			}
		}

		#endregion

	}
}
