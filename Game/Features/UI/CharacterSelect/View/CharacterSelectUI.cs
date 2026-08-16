using Godot;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
	public partial class CharacterSelectUI : CanvasLayer
	{
		public System.Action<CharacterSaveData> OnLocalSelected { get; set; }
		public CharacterSelectContext CurrentContext { get; set; } = CharacterSelectContext.OwnWorld;

		public string LastMultiplayerKey { get; set; } = "";
		public Godot.Collections.Array LastServerSummaries { get; set; } = new();

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
			Game.Ui.CharacterSelectUI.BackButton.Node.Pressed += OnBackPressed;
			Game.Ui.CharacterSelectUI.CreateCharacterButton.Node.Pressed += OnCreateCharacterPressed;
		}

		#endregion

		#region Public API

		public void OpenForOwnWorld()
		{
			CurrentContext = CharacterSelectContext.OwnWorld;
			OnLocalSelected = character =>
			{
				Game.Managers.WorldManager.Node.PendingCharacter = character;

				Game.Managers.WorldManager.Node.EnterPendingWorld();
			};

			ShowLocal();
		}

		public void OpenForPeerJoin()
		{
			CurrentContext = CharacterSelectContext.PeerJoinLocal;
			OnLocalSelected = character => Game.Managers.WorldManager.Node.SubmitLocalCharacterForJoin(character);

			ShowLocal();
		}

		public void OpenServer(string multiplayerKey, Godot.Collections.Array summaries)
		{
			CurrentContext = CharacterSelectContext.PeerJoinServer;
			LastMultiplayerKey = multiplayerKey;
			LastServerSummaries = summaries;

			ShowServer();
		}

		public void Close()
		{
			Visible = false;
		}

		public void ReopenLocal()
		{
			ShowLocal();
		}

		public void ReopenServer()
		{
			ShowServer();
		}

		public void CompleteLocalCreation(CharacterSaveData character)
		{
			if (character == null)
			{
				return;
			}

			OnLocalSelected?.Invoke(character);

			Close();
		}

		#endregion

		#region Core - Setup

		private void ShowLocal()
		{
			Visible = true;

			ClearList();

			var characters = Game.Managers.SaveManager.Node?.ListLocalCharacters() ?? new List<CharacterSaveData>();

			foreach (var character in characters)
			{
				var row = CreateCharacterRow(
					character.Name,
					() =>
					{
						OnLocalSelected?.Invoke(character);

						Close();
					},
					() =>
					{
						Game.Managers.SaveManager.Node?.DeleteLocalCharacter(character.CharacterId);

						if (Game.Managers.WorldManager.Node.PendingCharacter?.CharacterId == character.CharacterId)
						{
							Game.Managers.WorldManager.Node.PendingCharacter = null;
						}

						ShowLocal();
					});

				if (row != null)
				{
					Game.Ui.CharacterSelectUI.ListContainer.Node.AddChild(row);
				}
			}
		}

		private void ShowServer()
		{
			Visible = true;

			ClearList();

			foreach (var entry in LastServerSummaries)
			{
				var dict = entry.AsGodotDictionary();
				var characterId = dict["CharacterId"].AsString();
				var name = dict["Name"].AsString();

				var row = CreateCharacterRow(
					name,
					() =>
					{
						Game.Managers.WorldManager.Node.SelectServerCharacterRequest(characterId);

						Close();
					},
					() => Game.Managers.WorldManager.Node.DeleteServerCharacterRequest(characterId));

				if (row != null)
				{
					Game.Ui.CharacterSelectUI.ListContainer.Node.AddChild(row);
				}
			}
		}

		private void ClearList()
		{
			foreach (var child in Game.Ui.CharacterSelectUI.ListContainer.Node.GetChildren())
			{
				if (child.Name == "CharacterRowTemplate")
				{
					continue;
				}

				child.QueueFree();
			}
		}

		private Control CreateCharacterRow(string title, System.Action onSelect, System.Action onDelete)
		{
			var template = Game.Ui.CharacterSelectUI.CharacterRowTemplate.Node;

			if (template == null)
			{
				GD.PushError("CharacterSelectUI: CharacterRowTemplate não encontrado em Game.Ui.CharacterSelectUI.ListContainer.Node.");

				return null;
			}

			template.Visible = false;

			var row = (HBoxContainer)template.Duplicate();
			row.Visible = true;

			var selectButton = row.GetNode<Button>("SelectButton");
			selectButton.Text = title;
			selectButton.Pressed += onSelect;

			var deleteButton = row.GetNode<Button>("DeleteButton");
			deleteButton.Pressed += onDelete;

			return row;
		}

		#endregion

		#region Core - Actions

		private void OnCreateCharacterPressed()
		{
			Close();

			var createUi = Game.Ui.CreateCharacterUI.Node;

			if (CurrentContext == CharacterSelectContext.PeerJoinServer)
			{
				createUi?.OpenServer();
			}
			else
			{
				createUi?.OpenLocal();
			}
		}

		private void OnBackPressed()
		{
			Close();

			switch (CurrentContext)
			{
				case CharacterSelectContext.OwnWorld:
					Game.Managers.WorldManager.Node.PendingWorld = null;

					Game.Ui.WorldSelectUI.Node?.Open();

					break;

				case CharacterSelectContext.PeerJoinLocal:
				case CharacterSelectContext.PeerJoinServer:
					Game.Managers.WorldManager.Node.Disconnect();

					Game.Ui.MultiplayerUI.Node?.Open();

					break;
			}
		}

		#endregion
	}
}
