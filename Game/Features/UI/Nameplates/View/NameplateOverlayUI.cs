using Godot;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class NameplateOverlayUI : CanvasLayer
    {
        #region Properties

        private WorldManager _worldManager;
        private readonly Dictionary<Player, NameplateRow> _rows = new();

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Layer = 5;

            _worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
        }

        public override void _Process(double delta)
        {
            if (_worldManager == null)
            {
                _worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

                return;
            }

            var players = GetTree().GetNodesInGroup("players")
                .OfType<Player>()
                .Where(p => IsInstanceValid(p) && !p.IsOwner())
                .ToList();

            RemoveStaleRows(players);

            foreach (var player in players)
            {
                if (!_rows.TryGetValue(player, out var row))
                {
                    row = NameplateRow.Create(this);
                    _rows[player] = row;
                }

                UpdateRow(player, row);
            }
        }

        #endregion

        #region Core - Rows

        private void RemoveStaleRows(List<Player> currentPlayers)
        {
            var stale = _rows.Keys.Where(p => !IsInstanceValid(p) || !currentPlayers.Contains(p)).ToList();

            foreach (var player in stale)
            {
                _rows[player].Free();
                _rows.Remove(player);
            }
        }

        private void UpdateRow(Player player, NameplateRow row)
        {
            var screenPos = ProjectToScreen(player);

            if (screenPos == null)
            {
                row.SetVisible(false);

                return;
            }

            row.SetVisible(true);
            row.Update(player, screenPos.Value);
        }

        private Vector2? ProjectToScreen(Player player)
        {
            var parent = player.GetParent() as Node2D;
            var container = _worldManager.GetContainerForParent(parent);

            if (container == null || !container.Visible)
            {
                return null;
            }

            if (player.GetViewport() is not SubViewport viewport)
            {
                return null;
            }

            if (viewport.GetCamera2D() == null)
            {
                return null;
            }

            var localPos = viewport.GetCanvasTransform() * player.GlobalPosition;
            var containerRect = container.GetGlobalRect();
            var scale = containerRect.Size / (Vector2)viewport.Size;

            return containerRect.Position + localPos * scale;
        }

        #endregion
    }

    internal class NameplateRow
    {
        public Label NameLabel { get; set; }
        public Label HealthLabel { get; set; }

        public static NameplateRow Create(CanvasLayer parent)
        {
            var nameLabel = new Label();
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.Size = new Vector2(90, 16);
            nameLabel.AddThemeFontSizeOverride("font_size", 13);
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            nameLabel.AddThemeConstantOverride("outline_size", 3);
            parent.AddChild(nameLabel);

            var healthLabel = new Label();
            healthLabel.HorizontalAlignment = HorizontalAlignment.Center;
            healthLabel.Size = new Vector2(90, 16);
            healthLabel.AddThemeFontSizeOverride("font_size", 12);
            healthLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f, 1f));
            healthLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            healthLabel.AddThemeConstantOverride("outline_size", 3);
            parent.AddChild(healthLabel);

            return new NameplateRow { NameLabel = nameLabel, HealthLabel = healthLabel };
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                return;
            }

            NameLabel.Visible = false;
            HealthLabel.Visible = false;
        }

        public void Update(Player player, Vector2 screenPos)
        {
            var hasName = !string.IsNullOrEmpty(player.DisplayName);

            NameLabel.Visible = hasName;
            NameLabel.Text = player.DisplayName;
            NameLabel.Position = screenPos + new Vector2(-45, -58);

            HealthLabel.Visible = true;
            HealthLabel.Text = $"{player.Data.CurrentHealth}/{player.GetMaxHealth()}";
            HealthLabel.Position = screenPos + new Vector2(-45, hasName ? -42 : -50);
        }

        public void Free()
        {
            NameLabel.QueueFree();
            HealthLabel.QueueFree();
        }
    }
}
