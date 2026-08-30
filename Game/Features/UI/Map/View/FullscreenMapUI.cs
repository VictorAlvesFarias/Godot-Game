using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class FullscreenMapUI : ScreenUI
    {
        #region Dinamic properties

        public Player LocalPlayer { get; set; }
        public PlayerInput PlayerInput => LocalPlayer?.Input;

        public bool IsPanning { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Game.WhenReady(() => Game.Ui.FullscreenMapUI.MapView.Node.ViewRadius = 4000f);
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
                    Zoom(-300f);

                    GetViewport().SetInputAsHandled();
                }
                else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
                {
                    Zoom(300f);

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

        #endregion

        #region ScreenUI implementation

        public override bool IsOverlay => true;

        #endregion

        #region Core - Player lookup

        public void FindLocalPlayer()
        {
            var worldManager = Game.Managers.WorldManager.Node;

            LocalPlayer = worldManager?.GetLocalPlayer();

            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                Game.Ui.FullscreenMapUI.MapView.Node.SetLocalPlayer(LocalPlayer);
            }
        }

        #endregion

        #region Core - Toggle

        public void ToggleMap()
        {
            if (Visible)
            {
                Game.Managers.RouterManager.Node.Close(this);
            }
            else
            {
                Game.Managers.RouterManager.Node.Open(this);
            }

            if (Visible)
            {
                Game.Ui.FullscreenMapUI.MapView.Node.PanOffset = Vector2.Zero;

                PlayerInput?.AddBlocker("map");

                if (Game.Ui.HudUI.Minimap.Node != null)
                {
                    Game.Ui.HudUI.Minimap.Node.SetProcess(false);
                    Game.Ui.HudUI.Minimap.Node.Visible = false;
                }
            }
            else
            {
                IsPanning = false;

                PlayerInput?.RemoveBlocker("map");

                if (Game.Ui.HudUI.Minimap.Node != null)
                {
                    Game.Ui.HudUI.Minimap.Node.SetProcess(true);
                    Game.Ui.HudUI.Minimap.Node.Visible = true;
                }
            }
        }

        #endregion

        #region Core - Camera

        public void Zoom(float delta)
        {
            Game.Ui.FullscreenMapUI.MapView.Node.ViewRadius = Mathf.Clamp(Game.Ui.FullscreenMapUI.MapView.Node.ViewRadius + delta, 400f, 12000f);
        }

        public void PanDrag(Vector2 screenDelta)
        {
            if (Game.Ui.FullscreenMapUI.MapView.Node.LastScale <= 0f)
            {
                return;
            }

            Game.Ui.FullscreenMapUI.MapView.Node.PanOffset -= screenDelta / Game.Ui.FullscreenMapUI.MapView.Node.LastScale;
        }

        #endregion
    }
}
