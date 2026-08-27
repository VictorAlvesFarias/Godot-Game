using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Utils.GodotDictionaryParser;
using Jogo25D.Features.World.Items.Resources;

namespace Jogo25D.Items
{
    [Unload(UnloadMode.Global)]
    public partial class WorldItem : CharacterBody2D
    {
        #region Properties


        [GodotDictionaryField]
        public ItemData Item { get; set; }
        public float Gravity { get; set; }
        public float BobTime { get; set; }
        public float SpriteRestY { get; set; }

        #endregion

        #region Node children references

        public Sprite2D Sprite { get; set; }
        public CollisionShape2D Collision { get; set; }
        public Area2D PickupArea { get; set; }
        public CollisionShape2D PickupCollision { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Sprite = GetNodeOrNull<Sprite2D>("Sprite");
            Collision = GetNodeOrNull<CollisionShape2D>("Collision");
            PickupArea = GetNodeOrNull<Area2D>("PickupArea");
            PickupCollision = GetNodeOrNull<CollisionShape2D>("PickupArea/Collision");

            Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
            BobTime = (float)GD.RandRange(0f, Mathf.Tau);

            UpdateVisual();

            if (PickupArea != null)
            {
                PickupArea.BodyEntered += OnBodyEntered;
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
            if (Sprite == null)
            {
                return;
            }

            BobTime += (float)delta * 2.5f;

            var bobLift = Mathf.Abs(Mathf.Sin(BobTime)) * 4f;

            Sprite.Position = new Vector2(0f, SpriteRestY - bobLift);
        }

        #endregion

        #region Core - Item

        public void UpdateVisual()
        {
            if (Sprite == null || Item == null)
            {
                return;
            }

            var texture = ItemFactory.Create(Item?.Id)?.Icon;

            Sprite.Texture = texture;
            Sprite.Scale = new Vector2(0.7f, 0.7f);

            if (texture == null)
            {
                return;
            }

            var size = texture.GetSize() * 0.7f;

            if (Collision != null)
            {
                Collision.Shape = new RectangleShape2D { Size = size };
            }

            if (PickupCollision != null)
            {
                PickupCollision.Shape = new RectangleShape2D { Size = size };
            }
        }

        private void OnBodyEntered(Node body)
        {
            if (body is not Player player || !player.IsOwner())
            {
                return;
            }

            player.PickupItemRequest(DimensionManager.InstanceIdOf(this));
        }

        #endregion
    }
}
