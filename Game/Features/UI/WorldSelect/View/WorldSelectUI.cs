using Godot;
using System.Collections.Generic;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class WorldSelectUI : CanvasLayer
	{
		#region Node references

		public LineEdit SearchInput { get; set; }
		public VBoxContainer ListContainer { get; set; }
		public Button CreateWorldButton { get; set; }
		public Button MultiplayerButton { get; set; }
		public Button BackButton { get; set; }
		public WorldManager NetworkManager { get; set; }
		public Jogo25D.Systems.SaveManager Saves { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;
			Visible = false;

			SearchInput = GetNode<LineEdit>("MarginContainer/Root/SearchInput");
			ListContainer = GetNode<VBoxContainer>("MarginContainer/Root/ListScroll/ListContainer");
			CreateWorldButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateWorldButton");
			MultiplayerButton = GetNode<Button>("MarginContainer/Root/ButtonRow/MultiplayerButton");
			BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
			Saves = GetTree().Root.GetNodeOrNull<Jogo25D.Systems.SaveManager>(Jogo25D.Systems.SaveManager.DEFAULT_NODE_PATH);

			CreateWorldButton.Pressed += OnCreateWorldPressed;
			MultiplayerButton.Pressed += OnMultiplayerPressed;
			BackButton.Pressed += OnBackPressed;
		}

		#endregion

		#region Core - Setup

		private Button CreateWorldRow(string title, string subtitle, System.Action onPressed)
		{
			var row = new Button();

			row.Text = subtitle == null ? title : $"{title}\n{subtitle}";
			row.Alignment = HorizontalAlignment.Left;
			row.FocusMode = Control.FocusModeEnum.None;
			row.CustomMinimumSize = new Vector2(0, 44);
			row.AddThemeFontSizeOverride("font_size", 16);
			row.AddThemeColorOverride("font_color", Colors.White);

			var normalStyle = new StyleBoxFlat();
			normalStyle.BgColor = new Color(1f, 1f, 1f, 0.06f);
			normalStyle.BorderColor = new Color(1f, 1f, 1f, 0.15f);
			normalStyle.SetBorderWidthAll(1);
			normalStyle.SetCornerRadiusAll(4);
			normalStyle.ContentMarginLeft = 14;
			normalStyle.ContentMarginRight = 14;
			normalStyle.ContentMarginTop = 10;
			normalStyle.ContentMarginBottom = 10;

			row.AddThemeStyleboxOverride("normal", normalStyle);
			row.AddThemeStyleboxOverride("disabled", normalStyle);

			var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
			hoverStyle.BgColor = new Color(0.62f, 0.36f, 0.92f, 0.15f);
			hoverStyle.BorderColor = new Color(0.62f, 0.36f, 0.92f, 0.4f);

			var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
			pressedStyle.BgColor = new Color(0.62f, 0.36f, 0.92f, 0.25f);

			row.AddThemeStyleboxOverride("hover", hoverStyle);
			row.AddThemeStyleboxOverride("pressed", pressedStyle);
			row.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

			if (onPressed != null)
			{
				row.Pressed += onPressed;
			}

			return row;
		}

		private Control CreateWorldRowWithDelete(string title, string subtitle, System.Action onSelect, System.Action onDelete)
		{
			var wrapper = new HBoxContainer();

			wrapper.AddThemeConstantOverride("separation", 8);

			var selectButton = CreateWorldRow(title, subtitle, onSelect);

			selectButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			wrapper.AddChild(selectButton);

			var deleteButton = new Button
			{
				Text = "Excluir",
				CustomMinimumSize = new Vector2(90, 44),
				FocusMode = Control.FocusModeEnum.None,
				MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			};

			deleteButton.AddThemeFontSizeOverride("font_size", 14);
			deleteButton.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.55f, 1f));
			deleteButton.Pressed += onDelete;

			wrapper.AddChild(deleteButton);

			return wrapper;
		}

		private void PopulateWorldRows()
		{
			foreach (var child in ListContainer.GetChildren())
			{
				child.QueueFree();
			}

			ListContainer.AddChild(CreateWorldRow("Mundo Padrão", "Mapa fixo, sem save de terreno", OnDefaultWorldPressed));

			var worlds = Saves?.ListWorlds() ?? new List<WorldSaveData>();

			foreach (var world in worlds)
			{
				var modeLabel = world.CharacterMode == WorldCharacterMode.ServerCharacters
					? $"Servidor (chave: {world.MultiplayerKey})"
					: "Local";

				ListContainer.AddChild(CreateWorldRowWithDelete(
					world.Name,
					$"{modeLabel} · autosave a cada {world.AutosaveIntervalMinutes} min",
					() => OnWorldRowPressed(world),
					() =>
					{
						Saves?.DeleteWorld(world.WorldId);

						PopulateWorldRows();
					}));
			}
		}

		#endregion

		#region Public API

		public void Open()
		{
			Visible = true;

			PopulateWorldRows();
		}

		public void Close()
		{
			Visible = false;
		}

		#endregion

		#region Core - Actions - Mundos

		public void OnDefaultWorldPressed()
		{
			NetworkManager.PendingWorld = null;
			NetworkManager.PendingWorldIsDefault = true;

			Close();

			GetTree().Root.GetNodeOrNull<CharacterSelectUI>("Main/Ui/CharacterSelectUI")?.OpenForOwnWorld();
		}

		public void OnWorldRowPressed(WorldSaveData world)
		{
			NetworkManager.PendingWorld = world;
			NetworkManager.PendingWorldIsDefault = false;

			Close();

			GetTree().Root.GetNodeOrNull<CharacterSelectUI>("Main/Ui/CharacterSelectUI")?.OpenForOwnWorld();
		}

		public void OnCreateWorldPressed()
		{
			Close();

			GetTree().Root.GetNodeOrNull<CreateWorldUI>("Main/Ui/CreateWorldUI")?.Open();
		}

		#endregion

		#region Core - Actions - Navegacao

		public void OnMultiplayerPressed()
		{
			Close();

			GetTree().Root.GetNodeOrNull<MultiplayerUI>("Main/Ui/MultiplayerUI")?.Open();
		}

		public void OnBackPressed()
		{
			Close();

			var startUi = GetTree().Root.GetNodeOrNull<StartUI>("Main/Ui/StartUI");

			if (startUi != null)
			{
				startUi.Visible = true;
			}
		}

		#endregion
	}
}
