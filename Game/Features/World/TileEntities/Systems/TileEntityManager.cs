using Godot;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;

namespace Jogo25D.TileEntities
{
    public partial class TileEntityManager : Node
    {
        [Export] public NodePath TileMapLayerPath { get; set; }

        private TileMapLayer _tileMapLayer;
        private Node2D _world;
        private readonly Dictionary<Vector2I, TileEntity> _entities = new();
        private readonly Dictionary<Player, Vector2I> _lastCell = new();

        public override void _Ready()
        {
            _world = GetParent<Node2D>();
            _tileMapLayer = GetNodeOrNull<TileMapLayer>(TileMapLayerPath);

            if (_tileMapLayer == null)
            {
                GD.PushWarning("[TileEntityManager._Ready] TileMapLayer nao encontrado em " + TileMapLayerPath);

                return;
            }

            ScanTiles();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_tileMapLayer == null)
            {
                return;
            }

            foreach (var player in GetTree().GetNodesInGroup("players").OfType<Player>())
            {
                if (player.GetParent() != _world)
                {
                    _lastCell.Remove(player);

                    continue;
                }

                var cell = _tileMapLayer.LocalToMap(_tileMapLayer.ToLocal(player.GlobalPosition));
                var hadPrevious = _lastCell.TryGetValue(player, out var previousCell);

                if (hadPrevious && previousCell == cell)
                {
                    continue;
                }

                var previousEntity = hadPrevious && _entities.TryGetValue(previousCell, out var pe) ? pe : null;
                var currentEntity = _entities.TryGetValue(cell, out var ce) ? ce : null;

                if (previousEntity != currentEntity)
                {
                    previousEntity?.OnPlayerExit(player);
                    currentEntity?.OnPlayerEnter(player);
                }

                _lastCell[player] = cell;
            }

            foreach (var player in GetTree().GetNodesInGroup("players").OfType<Player>())
            {
                if (player.GetParent() != _world || player.Input == null || !player.Input.Interact)
                {
                    continue;
                }

                if (!_lastCell.TryGetValue(player, out var cell))
                {
                    continue;
                }

                if (_entities.TryGetValue(cell, out var entity))
                {
                    entity.OnPlayerInteract(player);
                }
            }
        }

        public bool TryGetEntityAt(Vector2I cell, out TileEntity entity)
        {
            return _entities.TryGetValue(cell, out entity);
        }

        public void ClearEntities()
        {
            _entities.Clear();
            _lastCell.Clear();
        }

        public void RegisterChunk(TileMapLayer layer, Vector2I chunkCoord, int chunkSize)
        {
            if (_world == null || layer == null)
            {
                return;
            }

            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    ScanCell(layer, new Vector2I(baseCellX + localX, baseCellY + localY));
                }
            }
        }

        public void UnregisterChunk(Vector2I chunkCoord, int chunkSize)
        {
            var baseCellX = chunkCoord.X * chunkSize;
            var baseCellY = chunkCoord.Y * chunkSize;

            for (int localX = 0; localX < chunkSize; localX++)
            {
                for (int localY = 0; localY < chunkSize; localY++)
                {
                    _entities.Remove(new Vector2I(baseCellX + localX, baseCellY + localY));
                }
            }
        }

        public bool TryGetPromptFor(Player player, out string prompt)
        {
            prompt = null;

            if (!_lastCell.TryGetValue(player, out var cell) || !_entities.TryGetValue(cell, out var entity))
            {
                return false;
            }

            prompt = entity.InteractPrompt;

            return !string.IsNullOrEmpty(prompt);
        }

        private void ScanTiles()
        {
            ScanLayer(_tileMapLayer);
        }

        private void ScanLayer(TileMapLayer layer)
        {
            foreach (var cell in layer.GetUsedCells())
            {
                ScanCell(layer, cell);
            }
        }

        private void ScanCell(TileMapLayer layer, Vector2I cell)
        {
            if (!TryReadTag(layer, cell, out var typeId))
            {
                return;
            }

            var cellPosition = _world.ToLocal(layer.ToGlobal(layer.MapToLocal(cell)));
            var entity = TileEntityFactory.CreateInstance(typeId, cell, _world, cellPosition);

            if (entity == null)
            {
                return;
            }

            _entities[cell] = entity;
            entity.OnReady();
        }

        private bool TryReadTag(TileMapLayer layer, Vector2I cell, out string typeId)
        {
            typeId = null;

            var tileData = layer.GetCellTileData(cell);

            if (tileData == null)
            {
                return false;
            }

            var rawType = tileData.GetCustomData("tile_entity_type");
            typeId = rawType.VariantType == Variant.Type.Nil ? null : rawType.AsString();

            return !string.IsNullOrEmpty(typeId);
        }
    }
}
