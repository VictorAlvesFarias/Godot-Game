using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class FullscreenMapUI : CanvasLayer
    {
        public const float DefaultViewRadius = 4000f;
        public const float MinViewRadius = 400f;
        public const float MaxViewRadius = 12000f;
        public const float ZoomStep = 300f;

        public MinimapUI MapView { get; set; }
        public MinimapUI HudMinimap { get; set; }
        public Player LocalPlayer { get; set; }
        public PlayerInput PlayerInput => LocalPlayer?.Input;

        public bool IsPanning { get; set; }

        public override void _Ready()
        {
            Layer = 15;
            Visible = false;

            MapView = GetNode<MinimapUI>("Background/MapPanel/MapView");
            MapView.ViewRadius = DefaultViewRadius;
        }

        public override void _Input(InputEvent @event)
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                FindLocalPlayer();
            }

            if (PlayerInput != null && PlayerInput.IsBlockedByOther("map"))
            {
                return;
            }

            if (@event.IsActionPressed("toggle_map") && !@event.IsEcho())
            {
                ToggleMap();

                GetViewport().SetInputAsHandled();

                return;
            }

            if (@event.IsActionPressed("ui_cancel") && Visible)
            {
                ToggleMap();

                GetViewport().SetInputAsHandled();

                return;
            }

            if (!Visible)
            {
                return;
            }

            if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
            {
                if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
                {
                    Zoom(-ZoomStep);

                    GetViewport().SetInputAsHandled();
                }
                else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
                {
                    Zoom(ZoomStep);

                    GetViewport().SetInputAsHandled();
                }
                else if (mouseEvent.ButtonIndex == MouseButton.Middle)
                {
                    IsPanning = true;

                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseButton releaseEvent && !releaseEvent.Pressed && releaseEvent.ButtonIndex == MouseButton.Middle)
            {
                IsPanning = false;

                GetViewport().SetInputAsHandled();
            }
            else if (IsPanning && @event is InputEventMouseMotion motionEvent)
            {
                PanDrag(motionEvent.Relative);

                GetViewport().SetInputAsHandled();
            }
        }

        public void FindLocalPlayer()
        {
            var worldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

            LocalPlayer = worldManager?.GetLocalPlayer();

            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                MapView.SetLocalPlayer(LocalPlayer);
            }
        }

        public void ToggleMap()
        {
            Visible = !Visible;

            if (HudMinimap == null || !IsInstanceValid(HudMinimap))
            {
                HudMinimap = GetTree().Root.GetNodeOrNull<MinimapUI>("Main/Ui/HudUI/MarginContainer/MinimapPanel/Minimap");
            }

            if (Visible)
            {
                MapView.PanOffset = Vector2.Zero;

                PlayerInput?.AddBlocker("map");

                if (HudMinimap != null)
                {
                    HudMinimap.SetProcess(false);
                    HudMinimap.Visible = false;
                }
            }
            else
            {
                IsPanning = false;

                PlayerInput?.RemoveBlocker("map");

                if (HudMinimap != null)
                {
                    HudMinimap.SetProcess(true);
                    HudMinimap.Visible = true;
                }
            }
        }

        public void Zoom(float delta)
        {
            MapView.ViewRadius = Mathf.Clamp(MapView.ViewRadius + delta, MinViewRadius, MaxViewRadius);
        }

        public void PanDrag(Vector2 screenDelta)
        {
            if (MapView.LastScale <= 0f)
            {
                return;
            }

            MapView.PanOffset -= screenDelta / MapView.LastScale;
        }
    }
}
