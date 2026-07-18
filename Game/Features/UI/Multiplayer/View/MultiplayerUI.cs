using Godot;
using System.Collections.Generic;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class MultiplayerUI : CanvasLayer
	{
		#region Properties

		// Mock: mesma ideia da tela de seleção de mundo — só decoração por
		// enquanto, não representa servidores reais.
		private readonly List<string> _mockWorlds = new()
		{
			"Servidor da Guilda",
			"Mundo dos Amigos",
			"Arena PvP",
		};

		#endregion

		#region Node references

		public LineEdit SearchInput { get; set; }
		public VBoxContainer ListContainer { get; set; }
		public LineEdit AddressInput { get; set; }
		public Button ConnectButton { get; set; }
		public Button WorldsButton { get; set; }
		public Button BackButton { get; set; }
		public Label StatusLabel { get; set; }
		public WorldManager NetworkManager { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Layer = 20;
			Visible = false;

			SearchInput = GetNode<LineEdit>("MarginContainer/Root/SearchInput");
			ListContainer = GetNode<VBoxContainer>("MarginContainer/Root/ListScroll/ListContainer");
			AddressInput = GetNode<LineEdit>("MarginContainer/Root/ConnectRow/AddressInput");
			ConnectButton = GetNode<Button>("MarginContainer/Root/ConnectRow/ConnectButton");
			WorldsButton = GetNode<Button>("MarginContainer/Root/ButtonRow/WorldsButton");
			BackButton = GetNode<Button>("MarginContainer/Root/ButtonRow/BackButton");
			StatusLabel = GetNode<Label>("MarginContainer/Root/StatusLabel");
			NetworkManager = GetTree().Root.GetNode<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

			ConnectButton.Pressed += OnConnectPressed;
			WorldsButton.Pressed += OnWorldsPressed;
			BackButton.Pressed += OnBackPressed;

			PopulateMockList();
		}

		#endregion

		#region Core - Setup

		private void PopulateMockList()
		{
			foreach (var worldName in _mockWorlds)
			{
				var row = new PanelContainer();

				var rowStyle = new StyleBoxFlat();
				rowStyle.BgColor = new Color(1f, 1f, 1f, 0.06f);
				rowStyle.BorderColor = new Color(1f, 1f, 1f, 0.15f);
				rowStyle.SetBorderWidthAll(1);
				rowStyle.SetCornerRadiusAll(4);
				row.AddThemeStyleboxOverride("panel", rowStyle);

				var margin = new MarginContainer();
				margin.AddThemeConstantOverride("margin_left", 14);
				margin.AddThemeConstantOverride("margin_top", 10);
				margin.AddThemeConstantOverride("margin_right", 14);
				margin.AddThemeConstantOverride("margin_bottom", 10);
				row.AddChild(margin);

				var hbox = new HBoxContainer();
				hbox.AddThemeConstantOverride("separation", 12);
				margin.AddChild(hbox);

				var label = new Label();
				label.Text = worldName;
				label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				label.VerticalAlignment = VerticalAlignment.Center;
				hbox.AddChild(label);

				ListContainer.AddChild(row);
			}
		}

		#endregion

		#region Public API

		public void Open()
		{
			Visible = true;

			if (StatusLabel != null)
			{
				StatusLabel.Text = "";
			}
		}

		public void Close()
		{
			Visible = false;
		}

		#endregion

		#region Core - Actions

		public void OnConnectPressed()
		{
			if (NetworkManager == null)
			{
				return;
			}

			var address = NetworkManager.SpawnWorldAndJoin(AddressInput.Text.Trim());

			if (string.IsNullOrEmpty(address))
			{
				StatusLabel.Text = "Não foi possível conectar. Verifique o IP:Porta.";

				return;
			}

			Close();
		}

		public void OnWorldsPressed()
		{
			Close();

			GetTree().Root.GetNodeOrNull<WorldSelectUI>("Main/Ui/WorldSelectUI")?.Open();
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
