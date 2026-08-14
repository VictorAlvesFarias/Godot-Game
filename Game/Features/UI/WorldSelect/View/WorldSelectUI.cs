using Godot;
using Jogo25D.Constants;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;
using System.Collections.Generic;

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
		public SaveManager Saves { get; set; }

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
			NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);
			Saves = GetTree().Root.GetNodeOrNull<SaveManager>(StaticNodePathsConstants.SaveManager);

			CreateWorldButton.Pressed += OnCreateWorldPressed;
			MultiplayerButton.Pressed += OnMultiplayerPressed;
			BackButton.Pressed += OnBackPressed;
		}

		#endregion

		#region Core - Setup

		private Button CreateWorldRow(string title, string subtitle, System.Action onPressed)
		{
			var template = ListContainer.GetNodeOrNull<Button>("WorldRowTemplate");

			if (template == null)
			{
				GD.PushError("WorldSelectUI: WorldRowTemplate não encontrado em ListContainer.");

				return null;
			}

			template.Visible = false;

			var row = (Button)template.Duplicate();
			row.Visible = true;
			row.Text = subtitle == null ? title : $"{title}\n{subtitle}";

			if (onPressed != null)
			{
				row.Pressed += onPressed;
			}

			return row;
		}

		private Control CreateWorldRowWithDelete(string title, string subtitle, System.Action onSelect, System.Action onDelete)
		{
			var template = ListContainer.GetNodeOrNull<HBoxContainer>("WorldRowWithDeleteTemplate");

			if (template == null)
			{
				GD.PushError("WorldSelectUI: WorldRowWithDeleteTemplate não encontrado em ListContainer.");

				return null;
			}

			template.Visible = false;

			var wrapper = (HBoxContainer)template.Duplicate();
			wrapper.Visible = true;

			var selectButton = wrapper.GetNode<Button>("SelectButton");
			selectButton.Text = subtitle == null ? title : $"{title}\n{subtitle}";
			selectButton.Pressed += onSelect;

			var deleteButton = wrapper.GetNode<Button>("DeleteButton");
			deleteButton.Pressed += onDelete;

			return wrapper;
		}

		private void PopulateWorldRows()
		{
			foreach (var child in ListContainer.GetChildren())
			{
				if (child.Name == "WorldRowTemplate" || child.Name == "WorldRowWithDeleteTemplate")
				{
					continue;
				}

				child.QueueFree();
			}

			var defaultRow = CreateWorldRow("Mundo Padrão", "Mapa fixo, sem save de terreno", OnDefaultWorldPressed);

			if (defaultRow != null)
			{
				ListContainer.AddChild(defaultRow);
			}

			var worlds = Saves?.ListWorlds() ?? new List<WorldSaveData>();

			foreach (var world in worlds)
			{
				var modeLabel = world.CharacterMode == WorldCharacterMode.ServerCharacters
					? $"Servidor (chave: {world.MultiplayerKey})"
					: "Local";

				var row = CreateWorldRowWithDelete(
					world.Name,
					$"{modeLabel} · autosave a cada {world.AutosaveIntervalMinutes} min",
					() => OnWorldRowPressed(world),
					() =>
					{
						Saves?.DeleteWorld(world.WorldId);

						PopulateWorldRows();
					});

				if (row != null)
				{
					ListContainer.AddChild(row);
				}
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
