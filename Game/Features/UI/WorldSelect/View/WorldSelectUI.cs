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

			PopulateDefaultWorldRow();
			PopulateMockList();
		}

		#endregion

		#region Core - Setup

		// Mesmo visual (painel com borda) pras duas situacoes - "Mundo
		// Padrao" e os mundos mock so diferem em serem clicaveis ou nao,
		// nao em aparencia (era exatamente essa a inconsistencia
		// reportada: "Mundo Padrao" aparecia como um botao separado, de
		// estilo diferente do resto da lista).
		private Button CreateWorldRow(string worldName, bool interactive)
		{
			var row = new Button();

			row.Text = worldName;
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

			if (interactive)
			{
				var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate();
				hoverStyle.BgColor = new Color(0.62f, 0.36f, 0.92f, 0.15f);
				hoverStyle.BorderColor = new Color(0.62f, 0.36f, 0.92f, 0.4f);

				var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate();
				pressedStyle.BgColor = new Color(0.62f, 0.36f, 0.92f, 0.25f);

				row.AddThemeStyleboxOverride("hover", hoverStyle);
				row.AddThemeStyleboxOverride("pressed", pressedStyle);
				row.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			}
			else
			{
				// Mock/decorativo - nao reage a mouse (sem hover, sem
				// clique), pra nao parecer clicavel sem realmente fazer
				// nada.
				row.AddThemeStyleboxOverride("hover", normalStyle);
				row.AddThemeStyleboxOverride("pressed", normalStyle);
				row.MouseFilter = Control.MouseFilterEnum.Ignore;
			}

			return row;
		}

		private void PopulateDefaultWorldRow()
		{
			var row = CreateWorldRow("Mundo Padrão", interactive: true);

			row.Pressed += OnDefaultWorldPressed;

			ListContainer.AddChild(row);
		}

		private void PopulateMockList()
		{
			foreach (var worldName in _mockWorlds)
			{
				ListContainer.AddChild(CreateWorldRow(worldName, interactive: false));
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

		public void OnDefaultWorldPressed()
		{
			NetworkManager?.SpawnLocalWorldAndPlayer();

			Close();
		}

		public void OnCreateWorldPressed()
		{
			NetworkManager?.CreateProceduralWorldAndPlayer();

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
