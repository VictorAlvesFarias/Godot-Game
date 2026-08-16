using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
	public partial class WorldSelectUI : CanvasLayer
	{
		#region Node references


		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;
			Visible = false;


			Game.WhenReady(Initialize);
		}

		#endregion

		#region Core - Setup

		private void Initialize()
		{
			Game.Ui.WorldSelectUI.CreateWorldButton.Node.Pressed += OnCreateWorldPressed;
			Game.Ui.WorldSelectUI.MultiplayerButton.Node.Pressed += OnMultiplayerPressed;
			Game.Ui.WorldSelectUI.BackButton.Node.Pressed += OnBackPressed;
		}

		#endregion

		#region Core - Setup

		private Button CreateWorldRow(string title, string subtitle, System.Action onPressed)
		{
			var template = Game.Ui.WorldSelectUI.WorldRowTemplate.Node;

			if (template == null)
			{
				GD.PushError("WorldSelectUI: WorldRowTemplate não encontrado em Game.Ui.WorldSelectUI.ListContainer.Node.");

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
			var template = Game.Ui.WorldSelectUI.WorldRowWithDeleteTemplate.Node;

			if (template == null)
			{
				GD.PushError("WorldSelectUI: WorldRowWithDeleteTemplate não encontrado em Game.Ui.WorldSelectUI.ListContainer.Node.");

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
			foreach (var child in Game.Ui.WorldSelectUI.ListContainer.Node.GetChildren())
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
				Game.Ui.WorldSelectUI.ListContainer.Node.AddChild(defaultRow);
			}

			var worlds = Game.Managers.SaveManager.Node?.ListWorlds() ?? new List<WorldSaveData>();

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
						Game.Managers.SaveManager.Node?.DeleteWorld(world.WorldId);

						PopulateWorldRows();
					});

				if (row != null)
				{
					Game.Ui.WorldSelectUI.ListContainer.Node.AddChild(row);
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
			Game.Managers.WorldManager.Node.PendingWorld = null;
			Game.Managers.WorldManager.Node.PendingWorldIsDefault = true;

			Close();

			Game.Ui.CharacterSelectUI.Node?.OpenForOwnWorld();
		}

		public void OnWorldRowPressed(WorldSaveData world)
		{
			Game.Managers.WorldManager.Node.PendingWorld = world;
			Game.Managers.WorldManager.Node.PendingWorldIsDefault = false;

			Close();

			Game.Ui.CharacterSelectUI.Node?.OpenForOwnWorld();
		}

		public void OnCreateWorldPressed()
		{
			Close();

			Game.Ui.CreateWorldUI.Node?.Open();
		}

		#endregion

		#region Core - Actions - Navegacao

		public void OnMultiplayerPressed()
		{
			Close();

			Game.Ui.MultiplayerUI.Node?.Open();
		}

		public void OnBackPressed()
		{
			Close();

			var startUi = Game.Ui.StartUI.Node;

			if (startUi != null)
			{
				startUi.Visible = true;
			}
		}

		#endregion
	}
}
