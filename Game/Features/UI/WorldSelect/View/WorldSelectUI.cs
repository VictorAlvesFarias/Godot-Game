using Godot;
using System.Collections.Generic;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class WorldSelectUI : CanvasLayer
	{
		#region Properties

		private readonly List<string> _mockWorlds = new()
		{
			"Reino Perdido",
			"Vale Sombrio",
			"Terra dos Ventos",
			"Ilha Esquecida",
		};

		#endregion

		#region Node references

		public LineEdit SearchInput { get; set; }
		public VBoxContainer ListContainer { get; set; }
		public Button CreateWorldButton { get; set; }
		public Button MultiplayerButton { get; set; }
		public Button BackButton { get; set; }
		public WorldManager NetworkManager { get; set; }

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

			CreateWorldButton.Pressed += OnCreateWorldPressed;
			MultiplayerButton.Pressed += OnMultiplayerPressed;
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
		}

		public void Close()
		{
			Visible = false;
		}

		#endregion

		#region Core - Actions

		public void OnCreateWorldPressed()
		{
			NetworkManager?.SpawnLocalWorldAndPlayer();

			Close();
		}

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
