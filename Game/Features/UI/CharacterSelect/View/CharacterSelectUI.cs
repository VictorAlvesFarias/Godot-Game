using Godot;
using System.Collections.Generic;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class CharacterSelectUI : CanvasLayer
	{
		private enum Context
		{
			OwnWorld,
			PeerJoinLocal,
			PeerJoinServer,
		}

		private System.Action<CharacterSaveData> _onLocalSelected;
		private Context _context = Context.OwnWorld;

		private string _lastMultiplayerKey = "";
		private Godot.Collections.Array _lastServerSummaries = new();

		#region Node references

		public LineEdit SearchInput { get; set; }
		public VBoxContainer ListContainer { get; set; }
		public Button BackButton { get; set; }
		public Button CreateCharacterButton { get; set; }
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
			BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
			CreateCharacterButton = GetNode<Button>("MarginContainer/Root/ButtonRow/CreateCharacterButton");
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
			Saves = GetTree().Root.GetNodeOrNull<Jogo25D.Systems.SaveManager>(Jogo25D.Systems.SaveManager.DEFAULT_NODE_PATH);

			BackButton.Pressed += OnBackPressed;
			CreateCharacterButton.Pressed += OnCreateCharacterPressed;
		}

		#endregion

		#region Public API

		public void OpenForOwnWorld()
		{
			_context = Context.OwnWorld;
			_onLocalSelected = character =>
			{
				NetworkManager.PendingCharacter = character;

				NetworkManager.EnterPendingWorld();
			};

			ShowLocal();
		}

		public void OpenForPeerJoin()
		{
			_context = Context.PeerJoinLocal;
			_onLocalSelected = character => NetworkManager.SubmitLocalCharacterForJoin(character);

			ShowLocal();
		}

		public void OpenServer(string multiplayerKey, Godot.Collections.Array summaries)
		{
			_context = Context.PeerJoinServer;
			_lastMultiplayerKey = multiplayerKey;
			_lastServerSummaries = summaries;

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

			_onLocalSelected?.Invoke(character);

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
				ListContainer.AddChild(CreateCharacterRow(
					character.Name,
					() =>
					{
						_onLocalSelected?.Invoke(character);

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
					}));
			}
		}

		private void ShowServer()
		{
			Visible = true;

			ClearList();

			foreach (var entry in _lastServerSummaries)
			{
				var dict = entry.AsGodotDictionary();
				var characterId = dict["CharacterId"].AsString();
				var name = dict["Name"].AsString();

				ListContainer.AddChild(CreateCharacterRow(
					name,
					() =>
					{
						NetworkManager.SelectServerCharacterRequest(characterId);

						Close();
					},
					() => NetworkManager.DeleteServerCharacterRequest(characterId)));
			}
		}

		private void ClearList()
		{
			foreach (var child in ListContainer.GetChildren())
			{
				child.QueueFree();
			}
		}

		private Control CreateCharacterRow(string title, System.Action onSelect, System.Action onDelete)
		{
			var row = new HBoxContainer();

			row.AddThemeConstantOverride("separation", 8);

			var selectButton = new Button();

			selectButton.Text = title;
			selectButton.Alignment = HorizontalAlignment.Left;
			selectButton.FocusMode = Control.FocusModeEnum.None;
			selectButton.CustomMinimumSize = new Vector2(0, 44);
			selectButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			selectButton.AddThemeFontSizeOverride("font_size", 16);
			selectButton.AddThemeColorOverride("font_color", Colors.White);

			var normalStyle = new StyleBoxFlat();
			normalStyle.BgColor = new Color(1f, 1f, 1f, 0.06f);
			normalStyle.BorderColor = new Color(1f, 1f, 1f, 0.15f);
			normalStyle.SetBorderWidthAll(1);
			normalStyle.SetCornerRadiusAll(4);
			normalStyle.ContentMarginLeft = 14;
			normalStyle.ContentMarginRight = 14;
			normalStyle.ContentMarginTop = 10;
			normalStyle.ContentMarginBottom = 10;

			selectButton.AddThemeStyleboxOverride("normal", normalStyle);
			selectButton.AddThemeStyleboxOverride("disabled", normalStyle);

			var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
			hoverStyle.BgColor = new Color(0.62f, 0.36f, 0.92f, 0.15f);
			hoverStyle.BorderColor = new Color(0.62f, 0.36f, 0.92f, 0.4f);

			var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
			pressedStyle.BgColor = new Color(0.62f, 0.36f, 0.92f, 0.25f);

			selectButton.AddThemeStyleboxOverride("hover", hoverStyle);
			selectButton.AddThemeStyleboxOverride("pressed", pressedStyle);
			selectButton.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			selectButton.Pressed += onSelect;

			row.AddChild(selectButton);

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

			row.AddChild(deleteButton);

			return row;
		}

		#endregion

		#region Core - Actions

		private void OnCreateCharacterPressed()
		{
			Close();

			var createUi = GetTree().Root.GetNodeOrNull<CreateCharacterUI>("Main/Ui/CreateCharacterUI");

			if (_context == Context.PeerJoinServer)
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

			switch (_context)
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
