using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.SkillTree;
using Jogo25D.Systems;
using System.Collections.Generic;

namespace Jogo25D.UI
{
	public partial class SkillTreeUI : CanvasLayer
	{
		#region Dinamic properties

		public Player LocalPlayer { get; set; }
		public PlayerInput PlayerInput => LocalPlayer?.Input;
		public StyleBoxFlat LockedStyle { get; set; }
		public StyleBoxFlat MaxedStyle { get; set; }
		public Dictionary<string, Button> NodeButtons { get; set; } = new();
		public Dictionary<string, Label> NodeLevelLabels { get; set; } = new();

		#endregion

		#region Node references

		public WorldManager NetworkManager { get; set; }

		#endregion

		#region Node children references

		public GridContainer GridContainer { get; set; }
		public LineEdit SearchInput { get; set; }
		public Button ResetButton { get; set; }
		public Label PointsLabel { get; set; }

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			Visible = false;
			Layer = 20;
			ProcessMode = ProcessModeEnum.Always;
			PointsLabel = GetNode<Label>("Background/MainPanel/MarginContainer/Root/PointsLabel");
			SearchInput = GetNode<LineEdit>("Background/MainPanel/MarginContainer/Root/Toolbar/SearchInput");
			ResetButton = GetNode<Button>("Background/MainPanel/MarginContainer/Root/Toolbar/ResetButton");
			GridContainer = GetNode<GridContainer>("Background/MainPanel/MarginContainer/Root/Scroll/GridContainer");

			SearchInput.TextChanged += OnSearchTextChanged;
			ResetButton.Pressed += OnResetPressed;

			BuildGrid();

			CallDeferred(nameof(FindLocalPlayer));
		}

		public override void _Input(InputEvent @event)
		{
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
			{
				FindLocalPlayer();
			}

			if (PlayerInput != null && PlayerInput.IsBlockedByOther("skill_tree"))
			{
				return;
			}

			if (@event.IsActionPressed("toggle_skill_tree") && !@event.IsEcho())
			{
				ToggleSkillTree();
				GetViewport().SetInputAsHandled();
			}
			else if (@event.IsActionPressed("ui_cancel") && Visible)
			{
				ToggleSkillTree();
				GetViewport().SetInputAsHandled();
			}
		}

		public override void _Process(double delta)
		{
			if (Visible)
			{
				PlayerInput?.AddBlocker("skill_tree");

				RefreshGrid();
			}
		}

		#endregion

		#region Core - Setup

		public void FindLocalPlayer()
		{
			NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);
			LocalPlayer = NetworkManager?.GetLocalPlayer();
		}

		private void BuildGrid()
		{
			var template = (Button)GridContainer.GetChild(0);

			LockedStyle = template.GetThemeStylebox("disabled") as StyleBoxFlat;

			var maxedStyleHolder = GetNodeOrNull<Panel>("Background/MaxedStyleHolder");

			if (maxedStyleHolder == null)
			{
				GD.PushError("SkillTreeUI: MaxedStyleHolder não encontrado em Background.");
			}
			else
			{
				MaxedStyle = maxedStyleHolder.GetThemeStylebox("panel") as StyleBoxFlat;
				maxedStyleHolder.Visible = false;
			}

			foreach (var nodeId in SkillTreeDB.GetAllIds())
			{
				CreateNodeButton(SkillTreeDB.Get(nodeId), template);
			}

			template.QueueFree();
		}

		private void CreateNodeButton(SkillTreeNode node, Button template)
		{
			var button = (Button)template.Duplicate();

			button.TooltipText = BuildTooltip(node);

			var iconRect = button.GetNode<TextureRect>("MarginContainer/Box/CenterBox/Icon");
			var nameLabel = button.GetNode<Label>("MarginContainer/Box/CenterBox/NameLabel");
			var levelLabel = button.GetNode<Label>("MarginContainer/Box/LevelLabel");

			iconRect.Texture = node.Icon;
			iconRect.Visible = node.Icon != null;
			nameLabel.Text = node.Name;

			var nodeId = node.Id;

			button.Pressed += delegate { OnNodePressed(nodeId); };

			GridContainer.AddChild(button);

			NodeButtons[node.Id] = button;
			NodeLevelLabels[node.Id] = levelLabel;
		}

		private string BuildTooltip(SkillTreeNode node)
		{
			var text = node.Description;

			if (node.RequiredTreeLevel > 0)
			{
				text += $"\nRequer nivel total {node.RequiredTreeLevel} na arvore principal.";
			}

			foreach (var dependency in node.Dependencies)
			{
				var dependencyNode = SkillTreeDB.Get(dependency.NodeId);
				var dependencyName = dependencyNode != null ? dependencyNode.Name : dependency.NodeId;

				text += $"\nRequer {dependencyName} nivel {dependency.MinLevel}.";
			}

			return text;
		}

		#endregion

		#region Core - Actions

		public void ToggleSkillTree()
		{
			Visible = !Visible;

			if (Visible)
			{
				PlayerInput?.AddBlocker("skill_tree");

				RefreshGrid();
			}
			else
			{
				PlayerInput?.RemoveBlocker("skill_tree");
			}
		}

		public void OnNodePressed(string nodeId)
		{
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
			{
				return;
			}

			LocalPlayer.LevelUpSkillNodeRequest(nodeId);

			RefreshGrid();
		}

		public void OnResetPressed()
		{
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
			{
				return;
			}

			LocalPlayer.ResetSkillTreeRequest();

			RefreshGrid();
		}

		public void OnSearchTextChanged(string text)
		{
			var query = text.Trim().ToLowerInvariant();

			foreach (var nodeId in NodeButtons.Keys)
			{
				var node = SkillTreeDB.Get(nodeId);
				var matches = string.IsNullOrEmpty(query) || node.Name.ToLowerInvariant().Contains(query);

				NodeButtons[nodeId].Visible = matches;
			}
		}

		#endregion

		#region Core - Refresh

		public void RefreshGrid()
		{
			if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
			{
				FindLocalPlayer();
			}

			var progress = LocalPlayer?.Data?.SkillTree;

			PointsLabel.Text = progress == null ? "Pontos disponiveis: ilimitado (temporario)" : $"Pontos disponiveis: ilimitado (temporario) | Investido na arvore principal: {SkillTreeDB.GetTreeLevel(progress, "main")}";

			foreach (var nodeId in SkillTreeDB.GetAllIds())
			{
				var node = SkillTreeDB.Get(nodeId);
				var button = NodeButtons[nodeId];
				var levelLabel = NodeLevelLabels[nodeId];

				var currentLevel = progress == null ? 0 : SkillTreeDB.GetNodeLevel(progress, nodeId);
				var canLevelUp = progress != null && SkillTreeDB.CanLevelUp(progress, nodeId);
				var maxed = currentLevel >= node.MaxLevel;

				levelLabel.Text = $"{currentLevel}/{node.MaxLevel}";
				button.Disabled = LocalPlayer == null || maxed || !canLevelUp;

				button.AddThemeStyleboxOverride("disabled", maxed ? MaxedStyle : LockedStyle);
			}
		}

		#endregion
	}
}
