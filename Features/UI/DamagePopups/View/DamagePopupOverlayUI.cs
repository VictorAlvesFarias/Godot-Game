using Godot;
using System.Collections.Generic;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class DamagePopupOverlayUI : CanvasLayer
    {
        #region Properties

        public static DamagePopupOverlayUI Instance { get; private set; }

        private WorldManager _worldManager;
        private readonly List<PopupEntry> _popups = new();

        private const float Duration = 0.8f;
        private const float RiseDistance = 26f;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Layer = 6;

            Instance = this;

            _worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void _Process(double delta)
        {
            if (_worldManager == null)
            {
                _worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

                return;
            }

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var popup = _popups[i];

                if (!IsInstanceValid(popup.Player) || !IsInstanceValid(popup.Label))
                {
                    popup.Label?.QueueFree();
                    _popups.RemoveAt(i);

                    continue;
                }

                popup.Elapsed += (float)delta;

                var t = Mathf.Clamp(popup.Elapsed / Duration, 0f, 1f);
                var screenPos = ProjectToScreen(popup.Player);

                if (screenPos != null)
                {
                    popup.Label.Position = screenPos.Value + popup.Offset + Vector2.Up * RiseDistance * t;
                    popup.Label.Modulate = new Color(1f, 1f, 1f, 1f - t);
                }

                if (t >= 1f)
                {
                    popup.Label.QueueFree();
                    _popups.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Public API

        public void ShowDamagePopup(Player player, int amount)
        {
            if (player == null || !IsInstanceValid(player) || amount <= 0)
            {
                return;
            }

            var label = new Label();

            label.Text = $"-{amount}";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Size = new Vector2(60, 20);
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", new Color(1f, 0.25f, 0.25f, 1f));
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 3);

            AddChild(label);

            var offset = new Vector2(-30f + (float)GD.RandRange(-8, 8), -70f);

            _popups.Add(new PopupEntry { Player = player, Label = label, Offset = offset, Elapsed = 0f });
        }

        #endregion

        #region Core - Projection

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

    internal class PopupEntry
    {
        public Player Player { get; set; }
        public Label Label { get; set; }
        public Vector2 Offset { get; set; }
        public float Elapsed { get; set; }
    }
}
