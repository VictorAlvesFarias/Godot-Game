using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Utils.Coordinates;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    public partial class LightingManager : Node
    {
        #region Dinamic properties

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
        private readonly HashSet<(string DimensionId, Vector2I ChunkCoord)> _inFlight = new();
        private readonly LightPropagationDispatcher _propagation = new();

        private Dictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> _lightEmittingBlocks;
        private bool _disposed;

        #endregion

        #region Godot implementation

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
                    SubscribeLayer(dimensionId, Game.Managers.DimensionManager.Node.ResolveLayer(dimensionId));
                    SubscribeLayer(dimensionId, Game.Managers.DimensionManager.Node.ResolveBaseLayer(dimensionId));
                }

                Game.Managers.TileStreamingManager.Node.ChunkLoaded += OnChunkLoaded;
                Game.Managers.TileStreamingManager.Node.ChunkUnloaded += OnChunkUnloaded;
            });
        }

        public override void _Process(double delta)
        {
            foreach (var dimensionId in DimensionIds)
            {
                EnsureAuthoredChunks(dimensionId);
                RebuildDirtyChunks(dimensionId);
            }
        }

        public override void _ExitTree()
        {
            _disposed = true;

            _propagation.Dispose();

            foreach (var overlays in _overlaysByDimension.Values)
            {
                foreach (var overlay in overlays.Values)
                {
                    overlay.Dispose();
                }
            }
        }

        #endregion

        #region Core - Chunks

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

            var used = AuthoredRect(dimensionId, layer);

            if (used.Size.X <= 0 || used.Size.Y <= 0)
            {
                return;
            }

            if (_authoredRectByDimension.TryGetValue(dimensionId, out var previous) && previous == used)
            {
                return;
            }

            _authoredRectByDimension[dimensionId] = used;

            var first = CoordinateUtilities.CellToChunk(used.Position);
            var last = CoordinateUtilities.CellToChunk(used.End - Vector2I.One);

            for (var cx = first.X; cx <= last.X; cx++)
            {
                for (var cy = first.Y; cy <= last.Y; cy++)
                {
                    var chunkCoord = new Vector2I(cx, cy);

                    loaded.Add(chunkCoord);

                    MarkDirtyWithNeighbors(dimensionId, chunkCoord);
                }
            }
        }

        private static Rect2I AuthoredRect(string dimensionId, TerrainLayer layer)
        {
            var used = layer.GetUsedRect();
            var baseLayer = Game.Managers.DimensionManager.Node.ResolveBaseLayer(dimensionId);

            if (baseLayer != null)
            {
                var baseUsed = baseLayer.GetUsedRect();

                used = baseUsed.Size == Vector2I.Zero ? used : used.Merge(baseUsed);
            }

            var walls = Game.Managers.DimensionManager.Node.ResolveParent(dimensionId)?.GetNodeOrNull<TileMapLayer>("BackgroundWalls");

            if (walls != null && walls.GetUsedRect().Size != Vector2I.Zero)
            {
                used = used.Size == Vector2I.Zero ? walls.GetUsedRect() : used.Merge(walls.GetUsedRect());
            }

            return used;
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

        #endregion

        #region Core - Invalidacao

        public void OnCellChanged(string dimensionId, Vector2I cell)
        {
            MarkDirtyWithNeighbors(dimensionId, CoordinateUtilities.CellToChunk(cell));
        }

        private void SubscribeLayer(string dimensionId, TerrainLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            layer.CellChanged += cell => OnCellChanged(dimensionId, cell);
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

        #region Core - Propagacao

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

                if (_inFlight.Contains((dimensionId, chunkCoord)) || processed >= LightingConstants.MAX_CHUNK_REBUILDS_PER_FRAME)
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

        private async void RebuildChunk(string dimensionId, Node2D overlayRoot, TerrainLayer layer, TerrainLayer baseLayer, Vector2I chunkCoord)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var padding = LightingConstants.CHUNK_PADDING;
            var chunkOrigin = CoordinateUtilities.ChunkToCell(chunkCoord);
            var region = new Rect2I(
                chunkOrigin - new Vector2I(padding, padding),
                new Vector2I(chunkSize + (padding * 2), chunkSize + (padding * 2)));
            var background = Game.Managers.DimensionManager.Node.ResolveParent(dimensionId)?.GetNodeOrNull<TileMapLayer>("BackgroundWalls");

            bool IsSolid(Vector2I cell)
            {
                return layer.GetCellSourceId(cell) != -1 || (baseLayer != null && baseLayer.GetCellSourceId(cell) != -1);
            }

            var sources = LightSourceScanner.CollectSources(
                layer,
                baseLayer,
                region,
                _lightEmittingBlocks,
                IsSolid,
                includeSkylight: false,
                hasBackground: cell => background != null && background.GetCellSourceId(cell) != -1);

            _inFlight.Add((dimensionId, chunkCoord));

            try
            {
                var output = await _propagation.ComputeTextureAsync(region, IsSolid, sources);

                if (_disposed || !_loadedByDimension[dimensionId].Contains(chunkCoord) || !IsInstanceValid(overlayRoot))
                {
                    output.Dispose();

                    return;
                }

                PublishChunk(dimensionId, overlayRoot, layer, baseLayer, chunkCoord, chunkOrigin, region, output);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception error)
            {
                GD.PushError(error.ToString());
            }
            finally
            {
                _inFlight.Remove((dimensionId, chunkCoord));
            }
        }

        private void PublishChunk(string dimensionId, Node2D overlayRoot, TerrainLayer layer, TerrainLayer baseLayer, Vector2I chunkCoord, Vector2I chunkOrigin, Rect2I region, LightTextureOutput output)
        {
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var tileSize = Game.Managers.DimensionManager.Node.TileSize;
            var overlays = _overlaysByDimension[dimensionId];

            if (!overlays.TryGetValue(chunkCoord, out var overlay))
            {
                overlay = new LightChunkOverlay(overlayRoot, chunkCoord, tileSize);
                overlays[chunkCoord] = overlay;
            }

            var walls = Game.Managers.DimensionManager.Node.ResolveParent(dimensionId)?.GetNodeOrNull<TileMapLayer>("BackgroundWalls");
            var bounds = new Rect2I(chunkOrigin * tileSize, new Vector2I(chunkSize, chunkSize) * tileSize);

            using var mask = TerrainLightMask.Build(bounds, overlayRoot, baseLayer, layer, walls);

            overlay.UpdateOutput(output, mask, LightingConstants.CHUNK_PADDING, region.Size.X);
        }

        #endregion

        #region Core - Apresentacao

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
                root = new Node2D
                {
                    Name = "LightOverlay"
                };

                parent.AddChild(root);
            }

            _overlayRootByDimension[dimensionId] = root;

            return root;
        }

        #endregion
    }
}
