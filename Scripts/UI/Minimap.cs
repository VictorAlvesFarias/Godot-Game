using Godot;
using System.Collections.Generic;
using Jogo25D.Characters;

namespace Jogo25D.UI
{
    public partial class Minimap : Control
    {
        [Export] public string PlayerGroupName { get; set; } = "players";
        [Export] public string PlatformGroupName { get; set; } = "platforms";
        [Export] public float ViewRadius { get; set; } = 600f;
        [Export] public Color LocalPlayerColor { get; set; } = new Color(0.2f, 0.8f, 1f, 1f);
        [Export] public Color OtherPlayerColor { get; set; } = new Color(0.6f, 0.6f, 0.6f, 1f);
        [Export] public Color PlatformColor { get; set; } = new Color(0.4f, 0.4f, 0.45f, 0.9f);
        [Export] public Color BackgroundColor { get; set; } = new Color(0.08f, 0.1f, 0.12f, 0.95f);
        [Export] public float PlayerDotRadius { get; set; } = 4f;

        private Node localPlayer;
        private int localPeerId = 1;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(120, 120);

            if (Multiplayer != null && Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
            {
                try
                {
                    localPeerId = Multiplayer.GetUniqueId();
                }
                catch { }
            }
        }

        public void SetLocalPlayer(Node player)
        {
            localPlayer = player;
        }

        public override void _Draw()
        {
            var mapSize = GetSquareSize();
            var margin = 4f;
            var drawRect = new Rect2(margin, margin, mapSize - margin * 2, mapSize - margin * 2);
            DrawRect(drawRect, BackgroundColor);

            if (localPlayer == null || !IsInstanceValid(localPlayer))
                return;

            var playerPos = (localPlayer as Node2D)?.GlobalPosition ?? Vector2.Zero;
            var center = new Vector2(mapSize / 2f, mapSize / 2f);
            var innerSize = mapSize - margin * 2;

            var scale = innerSize / (ViewRadius * 2f);

            if (scale <= 0f)
            {
                return;
            }

            var platforms = GetTree().GetNodesInGroup(PlatformGroupName);

            foreach (Node node in GetTree().GetNodesInGroup("minimap_collidable"))
            {
                if (node is CollisionObject2D body && IsInstanceValid(body))
                {
                    foreach (Node child in body.GetChildren())
                    {
                        if (child is CollisionShape2D shape && shape.Shape != null)
                        {
                            //TODO: 
                            //DrawCollisionShape(body, shape, playerPos, center, scale);
                        }
                    }
                }
            }

            var players = GetTree().GetNodesInGroup(PlayerGroupName);

            foreach (Node node in players)
            {
                if (node is Player player && IsInstanceValid(player))
                {
                    var worldPos = player.GlobalPosition;
                    var mapPos = WorldToMap(worldPos, playerPos, center, scale);
                    var isLocal = localPlayer == player || (Multiplayer != null && player.GetMultiplayerAuthority() == localPeerId);
                    var color = isLocal ? LocalPlayerColor : OtherPlayerColor;
                    DrawCircle(mapPos, PlayerDotRadius, color);
                }
            }
        }

        private float GetSquareSize()
        {
            var s = Size;
            return Mathf.Min(s.X, s.Y);
        }

        private Vector2 WorldToMap(Vector2 worldPos, Vector2 playerPos, Vector2 center, float scale)
        {
            var rel = worldPos - playerPos;
            return center + rel * scale;
        }


        public override void _Process(double delta)
        {
            QueueRedraw();
        }
    }
}
