using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
	public partial class WorldSelectUI : ScreenUI
	{
		#region Node references


		#endregion

		#region Godot implementation

		public override void _Ready()
		{

			Game.WhenReady(Initialize);
		}

        #endregion

        #region ScreenUI implementation

        public override void OnOpened()
        {

            PopulateWorldRows();
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

		private Control CreateWorldRow(string title, string subtitle, System.Action onSelect, System.Action onDelete)
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

			var worlds = Game.Managers.SaveManager.Node?.ListWorlds() ?? new List<WorldSaveData>();

			foreach (var world in worlds)
			{
				var modeLabel = world.CharacterMode == WorldCharacterMode.ServerCharacters
					? $"Servidor (chave: {world.MultiplayerKey})"
					: "Local";

				var row = CreateWorldRow(
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

		#region Core - Actions - Mundos

		public void OnWorldRowPressed(WorldSaveData world)
		{
			Game.Managers.SessionManager.Node.PendingWorld = world;

			Game.Ui.CharacterSelectUI.Node.CurrentContext = CharacterSelectContext.OwnWorld;

			Game.Managers.RouterManager.Node.Open(Game.Ui.CharacterSelectUI.Node);
		}

		public void OnCreateWorldPressed()
		{
			Game.Managers.RouterManager.Node.Open(Game.Ui.CreateWorldUI.Node);
		}

		#endregion

		#region Core - Actions - Navegacao

		public void OnMultiplayerPressed()
		{
			Game.Managers.RouterManager.Node.Open(Game.Ui.MultiplayerUI.Node);
		}

		public void OnBackPressed()
		{
			Game.Managers.RouterManager.Node.Back();
		}

		#endregion
	}
}
