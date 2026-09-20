using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    public static class LightSourceScanner
    {
        #region Core - Indice de blocos

        public static Dictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> BuildLightEmittingBlockIndex()
        {
            var map = new Dictionary<(int, Vector2I), BlockDefinition>();

            foreach (var block in BlockDB.All())
            {
                if (block.LightRadius > 0)
                {
                    map[(block.SourceId, block.AtlasCoord)] = block;
                }
            }

            return map;
        }

        public static BlockDefinition FindLightEmittingBlockAt(
            TerrainLayer layer,
            TerrainLayer baseLayer,
            Vector2I cell,
            IReadOnlyDictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> lightEmittingBlocks)
        {
            var sourceId = layer.GetCellSourceId(cell);
            var atlasCoord = layer.GetCellAtlasCoords(cell);

            if (sourceId == -1 && baseLayer != null)
            {
                sourceId = baseLayer.GetCellSourceId(cell);
                atlasCoord = baseLayer.GetCellAtlasCoords(cell);
            }

            if (sourceId == -1)
            {
                return null;
            }

            return lightEmittingBlocks.TryGetValue((sourceId, atlasCoord), out var block) ? block : null;
        }

        #endregion

        #region Core - Coleta

        public static List<LightSource> CollectSources(
            TerrainLayer layer,
            TerrainLayer baseLayer,
            Rect2I region,
            IReadOnlyDictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> lightEmittingBlocks,
            Func<Vector2I, bool> isSolid,
            bool includeSkylight = true,
            Func<Vector2I, bool> hasBackground = null)
        {
            if (hasBackground == null)
            {
                var walls = layer.GetParent()?.GetNodeOrNull<TileMapLayer>("BackgroundWalls");

                hasBackground = cell => walls != null && walls.GetCellSourceId(cell) != -1;
            }

            var sources = new List<LightSource>();
            var right = region.Position.X + region.Size.X;
            var bottom = region.Position.Y + region.Size.Y;

            for (var x = region.Position.X; x < right; x++)
            {
                for (var y = region.Position.Y; y < bottom; y++)
                {
                    var cell = new Vector2I(x, y);

                    if (includeSkylight && !isSolid(cell) && !hasBackground(cell))
                    {
                        sources.Add(new LightSource(cell, Colors.White));
                    }

                    var block = FindLightEmittingBlockAt(layer, baseLayer, cell, lightEmittingBlocks);

                    if (block == null)
                    {
                        continue;
                    }

                    var intensity = Mathf.Clamp(block.LightRadius / LightingConstants.LIGHT_RADIUS_REFERENCE, 0f, 1f);

                    sources.Add(new LightSource(cell, block.LightColor * intensity));
                }
            }

            SceneLightSources.Collect(layer.GetParent(), region, sources);

            return sources;
        }

        #endregion
    }
}
