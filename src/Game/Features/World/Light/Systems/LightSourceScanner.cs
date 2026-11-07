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

        // Blocos que emitem luz: os de terreno emitem em qualquer tile do source deles (todas as variacoes do autotile);
        // os sem terreno, so no tile cadastrado.
        public static LightEmitterIndex BuildLightEmittingBlockIndex()
        {
            var index = new LightEmitterIndex();

            foreach (var block in BlockDB.All())
            {
                if (block.LightRadius <= 0)
                {
                    continue;
                }

                index.ByMaterial[block.TerrainSet ?? block.SourceId] = block;

                if (block.TerrainSet != null)
                {
                    index.BySource[block.SourceId] = block;
                }
                else
                {
                    index.ByTile[(block.SourceId, block.AtlasCoord)] = block;
                }
            }

            return index;
        }

        public static BlockDefinition FindLightEmittingBlockAt(
            TerrainLayer layer,
            TerrainLayer baseLayer,
            Vector2I cell,
            LightEmitterIndex lightEmittingBlocks)
        {
            var source = layer;

            if (layer.GetCellSourceId(cell) == -1 && baseLayer != null)
            {
                source = baseLayer;
            }

            var sourceId = source.GetCellSourceId(cell);

            if (sourceId == -1)
            {
                return null;
            }

            if (lightEmittingBlocks.BySource.TryGetValue(sourceId, out var block))
            {
                return block;
            }

            return lightEmittingBlocks.ByTile.Count > 0 && lightEmittingBlocks.ByTile.TryGetValue((sourceId, source.GetCellAtlasCoords(cell)), out block) ? block : null;
        }

        #endregion

        #region Core - Coleta

        public static List<LightSource> CollectSources(
            TerrainLayer layer,
            TerrainLayer baseLayer,
            Rect2I region,
            LightEmitterIndex lightEmittingBlocks,
            Func<Vector2I, bool> isSolid,
            bool includeSkylight = true,
            Func<Vector2I, bool> hasBackground = null,
            Func<Vector2I, BlockDefinition> emitterAt = null)
        {
            if (hasBackground == null)
            {
                var walls = layer.GetParent()?.GetNodeOrNull<TileMapLayer>("Walls");

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

                    var block = emitterAt != null ? emitterAt(cell) : FindLightEmittingBlockAt(layer, baseLayer, cell, lightEmittingBlocks);

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
