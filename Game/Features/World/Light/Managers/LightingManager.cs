using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Utils.Coordinates;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Luz e 100% visual/cliente: cada cliente recalcula a partir dos tiles que ja chegaram (BlockDB
    // + celulas das TerrainLayer), sem RPC proprio - mesmo principio do indicador fantasma de
    // colocacao em BlockItemDefinition. Roda em cima do streaming de chunks ja existente
    // (TileStreamingManager.ChunkLoaded/Unloaded) e do evento CellChanged da TerrainLayer.
    public partial class LightingManager : Node
    {
        private static readonly string[] DimensionIds =
        {
            ChunkStreamingConstants.OVERWORLD_ID,
            ChunkStreamingConstants.UPSIDEDOWN_ID,
        };

        private readonly Dictionary<string, HashSet<Vector2I>> _loadedByDimension = new();
        private readonly Dictionary<string, HashSet<Vector2I>> _dirtyByDimension = new();
        private readonly Dictionary<string, Dictionary<Vector2I, LightChunkOverlay>> _overlaysByDimension = new();
        private readonly Dictionary<string, Node2D> _overlayRootByDimension = new();
        private readonly Dictionary<string, Rect2I> _authoredRectByDimension = new();
        private readonly LightPropagationDispatcher _propagacao = new();

        private Dictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> _lightEmittingBlocks;

        public override void _Ready()
        {
            SetProcess(true);

            _lightEmittingBlocks = LightSourceScanner.BuildLightEmittingBlockIndex();

            foreach (var dimensionId in DimensionIds)
            {
                _loadedByDimension[dimensionId] = new HashSet<Vector2I>();
                _dirtyByDimension[dimensionId] = new HashSet<Vector2I>();
                _overlaysByDimension[dimensionId] = new Dictionary<Vector2I, LightChunkOverlay>();
            }

            Game.WhenReady(() =>
            {
                foreach (var dimensionId in DimensionIds)
                {
                    var capturedDimensionId = dimensionId;

                    SubscribeLayer(capturedDimensionId, Game.Managers.DimensionManager.Node.ResolveLayer(capturedDimensionId));
                    SubscribeLayer(capturedDimensionId, Game.Managers.DimensionManager.Node.ResolveBaseLayer(capturedDimensionId));
                }

                Game.Managers.TileStreamingManager.Node.ChunkLoaded += OnChunkLoaded;
                Game.Managers.TileStreamingManager.Node.ChunkUnloaded += OnChunkUnloaded;
            });
        }

        public override void _ExitTree()
        {
            _propagacao.Dispose();
        }

        public override void _Process(double delta)
        {
            foreach (var dimensionId in DimensionIds)
            {
                EnsureAuthoredChunks(dimensionId);
                RebuildDirtyChunks(dimensionId);
            }
        }

        // Mapa desenhado a mao roda com o streaming desligado (WorldManager.SetChunkStreamingEnabled
        // (false)), entao ChunkLoaded nunca dispara e nenhum chunk seria marcado. Aqui os chunks que
        // cobrem a area pintada entram como carregados. Isso e so gatilho: a luz de cada chunk
        // continua sendo calculada pelo mesmo RebuildChunk, com as mesmas regras.
        private void EnsureAuthoredChunks(string dimensionId)
        {
            if (Game.Managers.TileStreamingManager.Node is { Enabled: true })
            {
                return;
            }

            var layer = Game.Managers.DimensionManager.Node?.ResolveLayer(dimensionId);

            if (layer == null || !_loadedByDimension.TryGetValue(dimensionId, out var loaded))
            {
                return;
            }

            var used = layer.GetUsedRect();
            var baseLayer = Game.Managers.DimensionManager.Node.ResolveBaseLayer(dimensionId);

            if (baseLayer != null)
            {
                var baseUsed = baseLayer.GetUsedRect();
                used = baseUsed.Size == Vector2I.Zero ? used : used.Merge(baseUsed);
            }

            var walls = Game.Managers.DimensionManager.Node.ResolveParent(dimensionId)?.GetNodeOrNull<TileMapLayer>("BackgroundWalls");
            if (walls != null && walls.GetUsedRect().Size != Vector2I.Zero)
                used = used.Size == Vector2I.Zero ? walls.GetUsedRect() : used.Merge(walls.GetUsedRect());

            if (used.Size.X <= 0 || used.Size.Y <= 0
                || (_authoredRectByDimension.TryGetValue(dimensionId, out var anterior) && anterior == used))
            {
                return;
            }

            _authoredRectByDimension[dimensionId] = used;

            var primeiro = CoordinateUtilities.CellToChunk(used.Position);
            var ultimo = CoordinateUtilities.CellToChunk(used.End - Vector2I.One);

            for (var cx = primeiro.X; cx <= ultimo.X; cx++)
            {
                for (var cy = primeiro.Y; cy <= ultimo.Y; cy++)
                {
                    var chunkCoord = new Vector2I(cx, cy);

                    loaded.Add(chunkCoord);
                    MarkDirtyWithNeighbors(dimensionId, chunkCoord);
                }
            }
        }

        #region Core - Gatilhos

        private void SubscribeLayer(string dimensionId, TerrainLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            layer.CellChanged += cell => OnCellChanged(dimensionId, cell);
        }

        public void OnCellChanged(string dimensionId, Vector2I cell)
        {
            MarkDirtyWithNeighbors(dimensionId, CoordinateUtilities.CellToChunk(cell));
        }

        private void OnChunkLoaded(string dimensionId, Vector2I chunkCoord)
        {
            if (!_loadedByDimension.TryGetValue(dimensionId, out var loaded))
            {
                return;
            }

            loaded.Add(chunkCoord);
            MarkDirtyWithNeighbors(dimensionId, chunkCoord);
        }

        private void OnChunkUnloaded(string dimensionId, Vector2I chunkCoord)
        {
            if (_loadedByDimension.TryGetValue(dimensionId, out var loaded))
            {
                loaded.Remove(chunkCoord);
            }

            if (_overlaysByDimension.TryGetValue(dimensionId, out var overlays) && overlays.TryGetValue(chunkCoord, out var overlay))
            {
                overlay.Dispose();
                overlays.Remove(chunkCoord);
            }

            if (_dirtyByDimension.TryGetValue(dimensionId, out var dirty))
            {
                dirty.Remove(chunkCoord);
            }
        }

        private void MarkDirtyWithNeighbors(string dimensionId, Vector2I chunkCoord)
        {
            if (!_dirtyByDimension.TryGetValue(dimensionId, out var dirty))
            {
                return;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    dirty.Add(chunkCoord + new Vector2I(dx, dy));
                }
            }
        }

        #endregion

        #region Core - Recalculo

        private void RebuildDirtyChunks(string dimensionId)
        {
            if (!_dirtyByDimension.TryGetValue(dimensionId, out var dirty) || dirty.Count == 0)
            {
                return;
            }

            if (!_loadedByDimension.TryGetValue(dimensionId, out var loaded))
            {
                return;
            }

            var overlayRoot = GetOverlayRoot(dimensionId);

            if (overlayRoot == null)
            {
                return;
            }

            var layer = Game.Managers.DimensionManager.Node.ResolveLayer(dimensionId);
            var baseLayer = Game.Managers.DimensionManager.Node.ResolveBaseLayer(dimensionId);

            if (layer == null)
            {
                return;
            }

            var processed = 0;
            var toProcess = new List<Vector2I>();

            foreach (var chunkCoord in dirty)
            {
                if (!loaded.Contains(chunkCoord))
                {
                    toProcess.Add(chunkCoord);

                    continue;
                }

                if (processed >= LightingConstants.MAX_CHUNK_REBUILDS_PER_FRAME)
                {
                    continue;
                }

                toProcess.Add(chunkCoord);
                processed++;
            }

            foreach (var chunkCoord in toProcess)
            {
                dirty.Remove(chunkCoord);

                if (loaded.Contains(chunkCoord))
                {
                    RebuildChunk(dimensionId, overlayRoot, layer, baseLayer, chunkCoord);
                }
            }
        }

        private void RebuildChunk(string dimensionId, Node2D overlayRoot, TerrainLayer layer, TerrainLayer baseLayer, Vector2I chunkCoord)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var padding = LightingConstants.CHUNK_PADDING;
            var chunkOrigin = CoordinateUtilities.ChunkToCell(chunkCoord);
            var region = new Rect2I(
                chunkOrigin - new Vector2I(padding, padding),
                new Vector2I(chunkSize + (padding * 2), chunkSize + (padding * 2)));

            bool IsSolid(Vector2I cell)
            {
                return layer.GetCellSourceId(cell) != -1 || (baseLayer != null && baseLayer.GetCellSourceId(cell) != -1);
            }

            var sources = LightSourceScanner.CollectSources(layer, baseLayer, region, _lightEmittingBlocks, IsSolid);
            var grid = _propagacao.Compute(region, IsSolid, sources);

            ApplyToOverlay(dimensionId, overlayRoot, chunkCoord, grid, layer, baseLayer);
        }

        private void ApplyToOverlay(string dimensionId, Node2D overlayRoot, Vector2I chunkCoord, Color[,] grid, TerrainLayer layer, TerrainLayer baseLayer)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var padding = LightingConstants.CHUNK_PADDING;
            var image = Image.CreateEmpty(chunkSize, chunkSize, false, Image.Format.Rgba8);

            for (var localX = 0; localX < chunkSize; localX++)
            {
                for (var localY = 0; localY < chunkSize; localY++)
                {
                    var value = grid[localX + padding, localY + padding];

                    image.SetPixel(localX, localY, new Color(
                        Mathf.Max(value.R, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(value.G, LightingConstants.AMBIENT_MIN),
                        Mathf.Max(value.B, LightingConstants.AMBIENT_MIN)));
                }
            }

            var overlays = _overlaysByDimension[dimensionId];

            if (!overlays.TryGetValue(chunkCoord, out var overlay))
            {
                overlay = new LightChunkOverlay(overlayRoot, chunkCoord, Game.Managers.DimensionManager.Node.TileSize);
                overlays[chunkCoord] = overlay;
            }

            var tileSize = Game.Managers.DimensionManager.Node.TileSize;
            var walls = Game.Managers.DimensionManager.Node.ResolveParent(dimensionId)
                ?.GetNodeOrNull<TileMapLayer>("BackgroundWalls");
            using var mask = TerrainLightMask.Build(new Rect2I(
                CoordinateUtilities.ChunkToCell(chunkCoord) * tileSize,
                new Vector2I(chunkSize, chunkSize) * tileSize), overlayRoot, baseLayer, layer, walls);
            overlay.UpdateTexture(image, mask);
        }

        private Node2D GetOverlayRoot(string dimensionId)
        {
            if (_overlayRootByDimension.TryGetValue(dimensionId, out var cachedRoot) && GodotObject.IsInstanceValid(cachedRoot))
            {
                return cachedRoot;
            }

            var parent = Game.Managers.DimensionManager.Node.ResolveParent(dimensionId);

            if (parent == null)
            {
                return null;
            }

            var root = parent.GetNodeOrNull<Node2D>("LightOverlay");

            if (root == null)
            {
                root = new Node2D { Name = "LightOverlay" };
                parent.AddChild(root);
            }

            _overlayRootByDimension[dimensionId] = root;

            return root;
        }

        #endregion
    }
}
