using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Save;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Items
{
    [Unload(UnloadMode.Global)]
    [SaveScene("worldItem", "res://Scenes/World/Items/WorldItem.tscn")]
    public partial class WorldItem : CharacterBody2D
    {
        #region Properties

        // Raio de coleta em volta do jogador, em tiles.
        public const float PickupRadiusTiles = 1.5f;

        // Intervalo entre pedidos de coleta automatica do mesmo item (o servidor pode recusar com inventario cheio).
        private const ulong PickupRetryMsec = 1000;

        [Save, GodotDictionaryField]
        public ItemData Item { get; set; }

        // Personagem que soltou o item: a coleta automatica dele ignora o item. Vazio para drops do mundo.
        [Save, GodotDictionaryField]
        public string DroppedBy { get; set; } = "";

        public float Gravity { get; set; }
        public float BobTime { get; set; }
        public float SpriteRestY { get; set; }

        public static float PickupRadius => PickupRadiusTiles * Dimension.TileSize;

        private ulong _lastPickupRequest;

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

            if (PickupCollision != null)
            {
                PickupCollision.Shape = new CircleShape2D { Radius = PickupRadius };
            }

            UpdateVisual();
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

            TryAutoPickup();
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

            // A texture.png ja tem o tamanho do item no mundo; a escala do tipo poe ela na grade do jogador (ou do tileset).
            var definition = ItemFactory.Create(Item?.Id);
            var texture = definition?.Texture;
            var scale = definition?.WorldPixelScale ?? 1f;

            // A colisao cobre so a parte visivel da arte (o tile de bloco tem sobra transparente em volta).
            var visible = texture == null ? new Rect2(0f, 0f, 8f, 8f) : (Rect2)texture.GetImage().GetUsedRect();

            Sprite.Texture = texture;
            Sprite.Scale = Vector2.One * scale;
            Sprite.Offset = texture == null ? Vector2.Zero : texture.GetSize() / 2f - visible.GetCenter();

            // A colisao com o terreno nao depende da imagem: sem texture.png o item continua no chao.
            var size = visible.Size * scale;

            if (Collision != null)
            {
                Collision.Shape = new RectangleShape2D { Size = size };
            }
        }

        public bool CanAutoPickup(Player player)
        {
            return player.AutoPickup && (string.IsNullOrEmpty(DroppedBy) || DroppedBy != player.CharacterId);
        }

        // Coleta automatica: o jogador local no raio de coleta, com a opcao ligada e que nao soltou este item.
        private void TryAutoPickup()
        {
            if (PickupArea == null || Time.GetTicksMsec() - _lastPickupRequest < PickupRetryMsec)
            {
                return;
            }

            foreach (var body in PickupArea.GetOverlappingBodies())
            {
                if (body is Player player && player.IsOwner() && CanAutoPickup(player))
                {
                    _lastPickupRequest = Time.GetTicksMsec();

                    player.PickupItemRequest(EntityRecord.InstanceIdOf(this));

                    return;
                }
            }
        }

        #endregion
    }
}
