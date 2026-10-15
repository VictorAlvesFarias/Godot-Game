using Godot;
using Jogo25D.Characters;
using Jogo25D.Dimensions;

namespace Jogo25D.UI
{
    // Card "Mapa": o mapa do mundo em volta do jogador, com zoom na roda do mouse, arrastar com o botao do meio e a
    // posicao do jogador em tiles. Com o mapa aberto, o minimapa do HUD fica escondido.
    public partial class MapCard : CardUI
    {
        #region Node children references

        public MinimapUI MapView { get; private set; }
        public Label PositionLabel { get; private set; }

        #endregion

        #region Dinamic properties

        private const float InitialViewRadius = 4000f;
        private const float ZoomStep = 300f;
        private const float MinViewRadius = 400f;
        private const float MaxViewRadius = 12000f;

        public bool IsPanning { get; private set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            MapView = GetNode<MinimapUI>("Box/MapView");
            PositionLabel = GetNode<Label>("Box/PositionWrap/PositionLabel");

            MapView.ViewRadius = InitialViewRadius;
            MapView.SetProcess(false);
        }

        public override void _Process(double delta)
        {
            if (IsOpen)
            {
                UpdatePositionLabel();
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (!IsOpen)
            {
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true } press)
            {
                if (press.ButtonIndex == MouseButton.WheelUp)
                {
                    Zoom(-ZoomStep);
                    GetViewport().SetInputAsHandled();
                }
                else if (press.ButtonIndex == MouseButton.WheelDown)
                {
                    Zoom(ZoomStep);
                    GetViewport().SetInputAsHandled();
                }
                else if (press.ButtonIndex == MouseButton.Middle)
                {
                    IsPanning = true;
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Middle })
            {
                IsPanning = false;
                GetViewport().SetInputAsHandled();
            }
            else if (IsPanning && @event is InputEventMouseMotion motion)
            {
                PanDrag(motion.Relative);
                GetViewport().SetInputAsHandled();
            }
        }

        #endregion

        #region Core - Contrato do card

        public override void Bind(Player player)
        {
            base.Bind(player);

            if (HasPlayer)
            {
                MapView.SetLocalPlayer(player);
            }
        }

        public override void OnOpened()
        {
            base.OnOpened();

            MapView.PanOffset = Vector2.Zero;
            MapView.SetProcess(true);
            SetHudMinimapVisible(false);
        }

        public override void OnClosed()
        {
            base.OnClosed();

            IsPanning = false;
            MapView.SetProcess(false);
            SetHudMinimapVisible(true);
        }

        #endregion

        #region Core - Mapa

        public void Zoom(float delta)
        {
            MapView.ViewRadius = Mathf.Clamp(MapView.ViewRadius + delta, MinViewRadius, MaxViewRadius);
        }

        public void PanDrag(Vector2 screenDelta)
        {
            if (MapView.LastScale > 0f)
            {
                MapView.PanOffset -= screenDelta / MapView.LastScale;
            }
        }

        private void UpdatePositionLabel()
        {
            if (!HasPlayer)
            {
                PositionLabel.Text = "X -   Y -";

                return;
            }

            var position = LocalPlayer.GlobalPosition / Dimension.TileSize;

            PositionLabel.Text = $"X {Mathf.FloorToInt(position.X)}   Y {Mathf.FloorToInt(position.Y)}";
        }

        private static void SetHudMinimapVisible(bool visible)
        {
            var minimap = Ui.Get<HudUI>()?.Minimap;

            if (minimap != null)
            {
                minimap.SetProcess(visible);
                minimap.Visible = visible;
            }
        }

        #endregion
    }
}
