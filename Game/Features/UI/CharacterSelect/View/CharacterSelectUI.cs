using Godot;
using Jogo25D.Constants;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
	public partial class CharacterSelectUI : CanvasLayer
	{
		public enum Context
		{
			OwnWorld,
			PeerJoinLocal,
			PeerJoinServer,
		}

		public System.Action<CharacterSaveData> OnLocalSelected { get; set; }
		public Context CurrentContext { get; set; } = Context.OwnWorld;

		public string LastMultiplayerKey { get; set; } = "";
		public Godot.Collections.Array LastServerSummaries { get; set; } = new();

		#region Node references

		public LineEdit SearchInput { get; set; }
		public VBoxContainer ListContainer { get; set; }
		public Button BackButton { get; set; }
		public Button CreateCharacterButton { get; set; }
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
			BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
			CreateCharacterButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateCharacterButton");
			NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);
			Saves = GetTree().Root.GetNodeOrNull<SaveManager>(StaticNodePathsConstants.SaveManager);

			BackButton.Pressed += OnBackPressed;
			CreateCharacterButton.Pressed += OnCreateCharacterPressed;
		}

		#endregion

		#region Public API

		public void OpenForOwnWorld()
		{
			CurrentContext = Context.OwnWorld;
			OnLocalSelected = character =>
			{
				NetworkManager.PendingCharacter = character;

				NetworkManager.EnterPendingWorld();
			};

			ShowLocal();
		}

		public void OpenForPeerJoin()
		{
			CurrentContext = Context.PeerJoinLocal;
			OnLocalSelected = character => NetworkManager.SubmitLocalCharacterForJoin(character);

			ShowLocal();
		}

		public void OpenServer(string multiplayerKey, Godot.Collections.Array summaries)
		{
			CurrentContext = Context.PeerJoinServer;
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

			var characters = Saves?.ListLocalCharacters() ?? new List<CharacterSaveData>();

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
						Saves?.DeleteLocalCharacter(character.CharacterId);

						if (NetworkManager.PendingCharacter?.CharacterId == character.CharacterId)
						{
							NetworkManager.PendingCharacter = null;
						}

						ShowLocal();
					});

				if (row != null)
				{
					ListContainer.AddChild(row);
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
						NetworkManager.SelectServerCharacterRequest(characterId);

						Close();
					},
					() => NetworkManager.DeleteServerCharacterRequest(characterId));

				if (row != null)
				{
					ListContainer.AddChild(row);
				}
			}
		}

		private void ClearList()
		{
			foreach (var child in ListContainer.GetChildren())
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
			var template = ListContainer.GetNodeOrNull<HBoxContainer>("CharacterRowTemplate");

			if (template == null)
			{
				GD.PushError("CharacterSelectUI: CharacterRowTemplate não encontrado em ListContainer.");

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

			var createUi = GetTree().Root.GetNodeOrNull<CreateCharacterUI>("Main/Ui/CreateCharacterUI");

			if (CurrentContext == Context.PeerJoinServer)
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
				case Context.OwnWorld:
					NetworkManager.PendingWorld = null;

					GetTree().Root.GetNodeOrNull<WorldSelectUI>("Main/Ui/WorldSelectUI")?.Open();

					break;

				case Context.PeerJoinLocal:
				case Context.PeerJoinServer:
					NetworkManager.Disconnect();

					GetTree().Root.GetNodeOrNull<MultiplayerUI>("Main/Ui/MultiplayerUI")?.Open();

					break;
			}
		}

		#endregion
	}
}
