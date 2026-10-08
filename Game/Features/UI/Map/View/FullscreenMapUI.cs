using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class FullscreenMapUI : ScreenUI
    {
        #region Node children references

        public MinimapUI MapView { get; private set; }
        public Panel ZoomHint { get; private set; }
        public Panel PanHint { get; private set; }
        public Label PositionLabel { get; private set; }

        #endregion

        #region Dinamic properties

        public Player LocalPlayer { get; set; }
        public PlayerInput PlayerInput => LocalPlayer?.Input;

        public bool IsPanning { get; set; }

        public override bool IsOverlay => true;

        private static readonly Color COR_ICONE = new Color(0.93f, 0.72f, 0.31f);
        private static readonly Color COR_BORDA = new Color(0.35f, 0.25f, 0.09f);

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            ResolveChildren();
            Initialize();
        }

        public override void _Process(double delta)
        {
            if (Visible)
            {
                UpdatePositionLabel();
            }
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

        #region Core - Setup

        private void ResolveChildren()
        {
            MapView = GetNode<MinimapUI>("Background/MapPanel/MapView");
            ZoomHint = GetNode<Panel>("Background/MapPanel/HintsColumn/ZoomHint");
            PanHint = GetNode<Panel>("Background/MapPanel/HintsColumn/PanHint");
            PositionLabel = GetNode<Label>("Background/MapPanel/PositionWrap/PositionLabel");
        }

        private void Initialize()
        {
            MapView.ViewRadius = 4000f;

            ConfigureHint(ZoomHint, CreateZoomIcon(), CreateMouseIcon(pressed: false));
            ConfigureHint(PanHint, CreatePanIcon(), CreateMouseIcon(pressed: true));
        }

        private static void ConfigureHint(Panel panel, Texture2D icon, Texture2D corner)
        {
            if (panel == null)
            {
                return;
            }

            panel.GetNode<TextureRect>("MarginContainer/CenterContainer/IconRect").Texture = icon;
            panel.GetNode<TextureRect>("CornerIcon").Texture = corner;
        }

        #endregion

        #region Core - Position

        public void UpdatePositionLabel()
        {
            var label = PositionLabel;

            if (label == null)
            {
                return;
            }

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                FindLocalPlayer();
            }

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                label.Text = "X -   Y -";

                return;
            }

            var tileSize = Dimension.TileSize;
            var position = LocalPlayer.GlobalPosition / tileSize;

            label.Text = $"X {Mathf.FloorToInt(position.X)}   Y {Mathf.FloorToInt(position.Y)}";
        }

        #endregion

        #region Core - Icons

        private static Texture2D CreateZoomIcon()
        {
            var image = Image.CreateEmpty(22, 22, false, Image.Format.Rgba8);

            FillCircle(image, new Vector2I(9, 9), 8, COR_ICONE);
            FillCircle(image, new Vector2I(9, 9), 5, new Color(0, 0, 0, 0));

            image.FillRect(new Rect2I(14, 14, 3, 3), COR_ICONE);
            image.FillRect(new Rect2I(16, 16, 3, 3), COR_ICONE);
            image.FillRect(new Rect2I(18, 18, 3, 3), COR_ICONE);

            image.FillRect(new Rect2I(8, 6, 2, 6), COR_ICONE);
            image.FillRect(new Rect2I(6, 8, 6, 2), COR_ICONE);

            return ImageTexture.CreateFromImage(image);
        }

        private static Texture2D CreatePanIcon()
        {
            var image = Image.CreateEmpty(22, 22, false, Image.Format.Rgba8);

            image.FillRect(new Rect2I(10, 3, 2, 16), COR_ICONE);
            image.FillRect(new Rect2I(3, 10, 16, 2), COR_ICONE);

            for (int i = 0; i < 4; i++)
            {
                image.FillRect(new Rect2I(8 + i, 1 + i, 6 - i * 2, 1), COR_ICONE);
                image.FillRect(new Rect2I(8 + i, 20 - i, 6 - i * 2, 1), COR_ICONE);
                image.FillRect(new Rect2I(1 + i, 8 + i, 1, 6 - i * 2), COR_ICONE);
                image.FillRect(new Rect2I(20 - i, 8 + i, 1, 6 - i * 2), COR_ICONE);
            }

            return ImageTexture.CreateFromImage(image);
        }

        private static Texture2D CreateMouseIcon(bool pressed)
        {
            var image = Image.CreateEmpty(26, 24, false, Image.Format.Rgba8);

            image.FillRect(new Rect2I(5, 1, 13, 22), COR_ICONE);
            image.FillRect(new Rect2I(7, 3, 9, 18), new Color(0, 0, 0, 0));

            foreach (var corner in new[] { new Vector2I(5, 1), new Vector2I(17, 1), new Vector2I(5, 22), new Vector2I(17, 22) })
            {
                image.SetPixel(corner.X, corner.Y, new Color(0, 0, 0, 0));
            }

            image.FillRect(new Rect2I(10, 5, 3, pressed ? 4 : 5), COR_ICONE);

            if (pressed)
            {
                image.FillRect(new Rect2I(9, 2, 5, 1), COR_ICONE);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    image.FillRect(new Rect2I(22 - i, 3 + i, 1 + i * 2, 1), COR_ICONE);
                    image.FillRect(new Rect2I(22 - i, 20 - i, 1 + i * 2, 1), COR_ICONE);
                }

                image.FillRect(new Rect2I(22, 6, 1, 12), COR_ICONE);
            }

            return ImageTexture.CreateFromImage(image);
        }

        private static void FillCircle(Image image, Vector2I center, int radius, Color color)
        {
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            {
                for (int x = center.X - radius; x <= center.X + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
                    {
                        continue;
                    }

                    var dx = x - center.X;
                    var dy = y - center.Y;

                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        image.SetPixel(x, y, color);
                    }
                }
            }
        }

        #endregion

        #region ScreenUI implementation

        #endregion

        #region Core - Player lookup

        public void FindLocalPlayer()
        {
            LocalPlayer = Players.GetLocal();

            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                MapView.SetLocalPlayer(LocalPlayer);
            }
        }

        #endregion

        #region Core - Toggle

        public void ToggleMap()
        {
            if (Visible)
            {
                RouterContext.Close(this);
            }
            else
            {
                RouterContext.Open(this);
            }

            if (Visible)
            {
                MapView.PanOffset = Vector2.Zero;

                PlayerInput?.AddBlocker("map");

                var minimap = Ui.Get<HudUI>()?.Minimap;

                if (minimap != null)
                {
                    minimap.SetProcess(false);
                    minimap.Visible = false;
                }
            }
            else
            {
                IsPanning = false;

                PlayerInput?.RemoveBlocker("map");

                var minimap = Ui.Get<HudUI>()?.Minimap;

                if (minimap != null)
                {
                    minimap.SetProcess(true);
                    minimap.Visible = true;
                }
            }
        }

        #endregion

        #region Core - Camera

        public void Zoom(float delta)
        {
            MapView.ViewRadius = Mathf.Clamp(MapView.ViewRadius + delta, 400f, 12000f);
        }

        public void PanDrag(Vector2 screenDelta)
        {
            if (MapView.LastScale <= 0f)
            {
                return;
            }

            MapView.PanOffset -= screenDelta / MapView.LastScale;
        }

        #endregion
    }
}
