using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Save;
using Jogo25D.Utils.GodotDictionaryParser;
using System.Collections.Generic;
using System.Linq;

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
        public const ulong PickupRetryMsec = 1000;

        // Passos seguidos parado no chao ate o item entrar em repouso.
        private const int StepsToRest = 3;

        // Velocidade maxima de queda (px/s): sem limite, um item preso acumula velocidade e o MoveAndSlide fica caro.
        private const float MaxFallSpeed = 900f;

        [Save, GodotDictionaryField]
        public ItemData Item { get; set; }

        // Personagem que soltou o item: a coleta automatica dele ignora o item. Vazio para drops do mundo.
        [Save, GodotDictionaryField]
        public string DroppedBy { get; set; } = "";

        public float Gravity { get; set; }

        public static float PickupRadius => PickupRadiusTiles * Dimension.TileSize;

        // Ultimo pedido de coleta automatica deste item (o jogador local respeita PickupRetryMsec).
        public ulong LastPickupRequest { get; set; }

        // Parado no chao: sem fisica ate uma celula de apoio mudar (Wake).
        public bool Resting { get; private set; }

        private int _stepsOnFloor;

        #endregion

        #region Node children references

        public Sprite2D Sprite { get; set; }
        public CollisionShape2D Collision { get; set; }

        #endregion

        #region Godot implementation

        public override void _EnterTree()
        {
            WorldItemIndex.Place(this);
        }

        public override void _ExitTree()
        {
            WorldItemIndex.Remove(this);
        }

        public override void _Ready()
        {
            Sprite = GetNodeOrNull<Sprite2D>("Sprite");
            Collision = GetNodeOrNull<CollisionShape2D>("Collision");

            Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

            UpdateVisual();
        }

        public override void _PhysicsProcess(double delta)
        {
            var v = Velocity;

            if (!IsOnFloor())
            {
                v.Y = Mathf.Min(v.Y + Gravity * (float)delta, MaxFallSpeed);
            }
            else
            {
                v.Y = 0f;
            }

            Velocity = v;

            var before = GlobalPosition;

            MoveAndSlide();

            WorldItemIndex.Place(this);

            // Parado encostado em algo (no chao, ou entalado entre paredes num poco estreito, onde nunca ha chao) por
            // StepsToRest passos: repouso.
            var still = GlobalPosition.DistanceSquaredTo(before) < 0.01f && (IsOnFloor() || IsOnWall() || IsOnCeiling());

            _stepsOnFloor = still ? _stepsOnFloor + 1 : 0;

            if (_stepsOnFloor >= StepsToRest)
            {
                Rest();
            }
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
            var visible = texture == null ? new Rect2(0f, 0f, 8f, 8f) : VisibleRect(texture);

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

        // Parte visivel da textura, calculada uma vez por textura (ler a imagem de volta da GPU a cada drop custava caro).
        private static Rect2 VisibleRect(Texture2D texture)
        {
            if (!VisibleRects.TryGetValue(texture, out var rect))
            {
                using var image = texture.GetImage();

                rect = image.GetUsedRect();
                VisibleRects[texture] = rect;
            }

            return rect;
        }

        private static readonly Dictionary<Texture2D, Rect2> VisibleRects = new();

        public bool CanAutoPickup(Player player)
        {
            return player.AutoPickup && (string.IsNullOrEmpty(DroppedBy) || DroppedBy != player.CharacterId);
        }

        #endregion

        #region Core - Repouso

        // Parado no chao: a fisica para e a colisao sai do motor (coleta e pilhas usam o WorldItemIndex), e o item se
        // registra nas celulas que o apoiam. Uma mudanca numa delas acorda o item (Dimension.WakeItemsOn).
        private void Rest()
        {
            Resting = true;
            _stepsOnFloor = 0;
            Velocity = Vector2.Zero;
            SetPhysicsProcess(false);
            Collision?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
            WorldItemIndex.SetSupport(this, SupportCells());

            if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                MergeWithNeighbors();
            }
        }

        // Servidor, ao soltar um item: os itens do chao do mesmo tipo e dono, a ate um tile, recebem a quantidade (os mais
        // antigos primeiro, ate o tamanho maximo da pilha). Devolve o item que absorveu tudo, ou nulo se sobrou algo
        // (o resto fica em `data`, para nascer como item novo).
        public static WorldItem AbsorbNearby(string dimension, Vector2 position, ItemData data, string droppedBy)
        {
            var definition = ItemFactory.Create(data.Id);

            if (definition == null || !definition.Stackable || data.Quantity <= 0)
            {
                return null;
            }

            var targets = WorldItemIndex.Near(dimension, position, Dimension.TileSize)
                .Where(other => !other.IsQueuedForDeletion() && other.Item?.Id == data.Id && other.DroppedBy == droppedBy && other.Item.Quantity < definition.MaxStackSize)
                .OrderBy(EntityRecord.InstanceIdOf);

            foreach (var target in targets)
            {
                var moved = Mathf.Min(definition.MaxStackSize - target.Item.Quantity, data.Quantity);

                target.Item.Quantity += moved;
                data.Quantity -= moved;

                if (data.Quantity <= 0)
                {
                    return target;
                }
            }

            return null;
        }

        // Servidor: os itens parados do mesmo tipo, com o mesmo dono (DroppedBy) e a ate um tile, viram uma pilha. A
        // quantidade vai para os mais antigos (menor id de instancia) ate o tamanho maximo da pilha; quem esvazia some.
        // O cliente so ve o despawn: a quantidade importa no servidor, que faz a coleta e o save.
        private void MergeWithNeighbors()
        {
            var definition = ItemFactory.Create(Item?.Id);

            if (definition == null || !definition.Stackable || Item.Quantity >= definition.MaxStackSize)
            {
                return;
            }

            var dimension = WorldItemIndex.DimensionOf(this);
            var group = WorldItemIndex.Near(dimension, GlobalPosition, Dimension.TileSize)
                .Where(other => other == this || (other.Resting && !other.IsQueuedForDeletion() && other.Item?.Id == Item.Id && other.DroppedBy == DroppedBy))
                .OrderBy(EntityRecord.InstanceIdOf)
                .ToList();

            if (group.Count < 2)
            {
                return;
            }

            for (var i = 1; i < group.Count; i++)
            {
                var from = group[i];

                for (var j = 0; j < i && from.Item.Quantity > 0; j++)
                {
                    var into = group[j];
                    var moved = Mathf.Min(definition.MaxStackSize - into.Item.Quantity, from.Item.Quantity);

                    if (moved <= 0 || into.IsQueuedForDeletion())
                    {
                        continue;
                    }

                    into.Item.Quantity += moved;
                    from.Item.Quantity -= moved;
                }

                if (from.Item.Quantity <= 0)
                {
                    EntitySpawner.DespawnRequest(EntityRecord.InstanceIdOf(from));
                }
            }
        }

        public void Wake()
        {
            if (!Resting)
            {
                return;
            }

            Resting = false;
            _stepsOnFloor = 0;
            WorldItemIndex.ClearSupport(this);
            Collision?.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
            SetPhysicsProcess(true);
        }

        // Celulas que podem estar segurando o item: as que a colisao dele ocupa e as em volta, ate um quarto de tile dos
        // lados e meio tile abaixo. A colisao dos tiles do terreno passa alguns pixels da borda da celula, entao o item pode
        // estar apoiado na quina da celula vizinha, ou entalado entre as paredes de um poco estreito.
        private List<Vector2I> SupportCells()
        {
            var tile = Dimension.TileSize;
            var half = (Collision?.Shape as RectangleShape2D)?.Size / 2f ?? Vector2.One * 4f;
            var center = GlobalPosition + (Collision?.Position ?? Vector2.Zero);
            var top = Mathf.FloorToInt((center.Y - half.Y) / tile);
            var bottom = Mathf.FloorToInt((center.Y + half.Y + tile / 2f) / tile);
            var from = Mathf.FloorToInt((center.X - half.X - tile / 4f) / tile);
            var to = Mathf.FloorToInt((center.X + half.X + tile / 4f) / tile);
            var cells = new List<Vector2I>();

            for (var x = from; x <= to; x++)
            {
                for (var y = top; y <= bottom; y++)
                {
                    cells.Add(new Vector2I(x, y));
                }
            }

            return cells;
        }

        #endregion
    }
}
