using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    public partial class WorldItem : CharacterBody2D
    {
        #region Properties

        public long WorldItemId { get; set; }
        public ItemData Data { get; set; }
        public float Gravity { get; set; }

        private const float VisualScale = 0.7f;
        private const float BobAmplitude = 4f;
        private const float BobSpeed = 2.5f;

        #endregion

        #region Node references

        private Sprite2D _sprite;
        private CollisionShape2D _collision;
        private Area2D _pickupArea;
        private CollisionShape2D _pickupCollision;
        private float _bobTime;
        private float _spriteRestY;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            _sprite = GetNodeOrNull<Sprite2D>("Sprite");
            _collision = GetNodeOrNull<CollisionShape2D>("Collision");
            _pickupArea = GetNodeOrNull<Area2D>("PickupArea");
            _pickupCollision = GetNodeOrNull<CollisionShape2D>("PickupArea/Collision");

            Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
            _bobTime = (float)GD.RandRange(0f, Mathf.Tau);

            UpdateVisual();

            if (_pickupArea != null)
            {
                _pickupArea.BodyEntered += OnBodyEntered;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            var v = Velocity;

            if (!IsOnFloor())
            {
                v.Y += Gravity * (float)delta;
            }
            else
            {
                v.Y = 0f;
            }

            Velocity = v;

            MoveAndSlide();
        }

        public override void _Process(double delta)
        {
            if (_sprite == null)
            {
                return;
            }

            _bobTime += (float)delta * BobSpeed;

            var bobLift = Mathf.Abs(Mathf.Sin(_bobTime)) * BobAmplitude;

            _sprite.Position = new Vector2(0f, _spriteRestY - bobLift);
        }

        #endregion

        #region Core

        public void UpdateVisual()
        {
            if (_sprite == null || Data == null)
            {
                return;
            }

            var texture = ItemFactory.Create(Data.Id)?.Icon;

            _sprite.Texture = texture;
            _sprite.Scale = new Vector2(VisualScale, VisualScale);

            if (texture == null)
            {
                return;
            }

            var size = texture.GetSize() * VisualScale;

            if (_collision != null)
            {
                _collision.Shape = new RectangleShape2D { Size = size };
            }

            if (_pickupCollision != null)
            {
                _pickupCollision.Shape = new RectangleShape2D { Size = size };
            }
        }

        private void OnBodyEntered(Node body)
        {
            if (body is not Player player || !player.IsOwner())
            {
                return;
            }

            player.PickupItemRequest(WorldItemId);
        }

        #endregion
    }
}
