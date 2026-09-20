using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Jogo25D.Light
{
    public static class TerrainLightMask
    {
        #region Dinamic properties

        private static readonly ConditionalWeakTable<Texture2D, TexturePixels> PixelCache = new();
        private static readonly ConditionalWeakTable<TileSet, ReachCache> ReachCaches = new();

        #endregion

        #region Core - Construcao

        public static Image Build(Rect2I bounds, Node2D overlayRoot, params TileMapLayer[] layers)
        {
            return BuildCore(bounds, overlayRoot, true, layers);
        }

        internal static Image BuildReference(Rect2I bounds, Node2D overlayRoot, params TileMapLayer[] layers)
        {
            return BuildCore(bounds, overlayRoot, false, layers);
        }

        private static Image BuildCore(Rect2I bounds, Node2D overlayRoot, bool fastCopy, params TileMapLayer[] layers)
        {
            var pixels = new byte[bounds.Size.X * bounds.Size.Y];
            var textures = new Dictionary<Texture2D, (byte[] Data, int Width)>();

            foreach (var layer in layers)
            {
                if (layer == null || !layer.Enabled || !layer.IsVisibleInTree() || layer.TileSet == null)
                {
                    continue;
                }

                BlitLayer(bounds, overlayRoot, fastCopy, layer, pixels, textures);
            }

            return Image.CreateFromData(bounds.Size.X, bounds.Size.Y, false, Image.Format.R8, pixels);
        }

        private static void BlitLayer(Rect2I bounds, Node2D overlayRoot, bool fastCopy, TileMapLayer layer, byte[] pixels, Dictionary<Texture2D, (byte[] Data, int Width)> textures)
        {
            var toOverlay = overlayRoot.GlobalTransform.AffineInverse() * layer.GlobalTransform;
            var localBounds = toOverlay.AffineInverse() * new Rect2(bounds.Position, bounds.Size);

            localBounds = localBounds.Grow(GetTextureReach(layer.TileSet));

            var first = layer.LocalToMap(localBounds.Position) - Vector2I.One;
            var last = layer.LocalToMap(localBounds.End) + Vector2I.One;
            var used = layer.GetUsedRect();

            first = first.Max(used.Position);
            last = last.Min(used.End - Vector2I.One);

            for (var y = first.Y; y <= last.Y; y++)
            {
                for (var x = first.X; x <= last.X; x++)
                {
                    BlitCell(bounds, fastCopy, layer, toOverlay, new Vector2I(x, y), pixels, textures);
                }
            }
        }

        private static void BlitCell(Rect2I bounds, bool fastCopy, TileMapLayer layer, Transform2D toOverlay, Vector2I cell, byte[] pixels, Dictionary<Texture2D, (byte[] Data, int Width)> textures)
        {
            var sourceId = layer.GetCellSourceId(cell);

            if (sourceId == -1 || layer.TileSet.GetSource(sourceId) is not TileSetAtlasSource atlas || atlas.Texture == null)
            {
                return;
            }

            var coords = layer.GetCellAtlasCoords(cell);
            var data = layer.GetCellTileData(cell);

            if (data == null)
            {
                return;
            }

            var source = atlas.GetTileTextureRegion(coords);
            var alternative = layer.GetCellAlternativeTile(cell);
            var tileTransform = Flips(data.Transpose, data.FlipH, data.FlipV);
            var cellTransform = Flips(
                (alternative & TileSetAtlasSource.TransformTranspose) != 0,
                (alternative & TileSetAtlasSource.TransformFlipH) != 0,
                (alternative & TileSetAtlasSource.TransformFlipV) != 0);
            var transform = toOverlay
                * new Transform2D(0, layer.MapToLocal(cell))
                * cellTransform
                * new Transform2D(0, -(Vector2)data.TextureOrigin)
                * tileTransform
                * new Transform2D(0, -(Vector2)source.Size * 0.5f);
            var target = transform * new Rect2(Vector2.Zero, source.Size);
            var start = new Vector2I(Mathf.FloorToInt(target.Position.X), Mathf.FloorToInt(target.Position.Y)).Max(bounds.Position);
            var end = new Vector2I(Mathf.CeilToInt(target.End.X), Mathf.CeilToInt(target.End.Y)).Min(bounds.End);

            if (start.X >= end.X || start.Y >= end.Y)
            {
                return;
            }

            if (!textures.TryGetValue(atlas.Texture, out var texture))
            {
                var cached = Pixels(atlas.Texture);

                texture = (cached.Data, cached.Width);

                textures.Add(atlas.Texture, texture);
            }

            var opacity = data.Modulate.A * layer.Modulate.A * layer.SelfModulate.A;
            var aligned = transform.X == Vector2.Right
                && transform.Y == Vector2.Down
                && transform.Origin.X == Mathf.Floor(transform.Origin.X)
                && transform.Origin.Y == Mathf.Floor(transform.Origin.Y)
                && opacity == 1f;

            if (fastCopy && aligned)
            {
                BlitAligned(bounds, source, transform, start, end, texture, pixels);

                return;
            }

            BlitSampled(bounds, source, transform, start, end, texture, opacity, pixels);
        }

        private static void BlitAligned(Rect2I bounds, Rect2I source, Transform2D transform, Vector2I start, Vector2I end, (byte[] Data, int Width) texture, byte[] pixels)
        {
            var ox = (int)transform.Origin.X;
            var oy = (int)transform.Origin.Y;

            for (var py = start.Y; py < end.Y; py++)
            {
                var src = ((source.Position.Y + py - oy) * texture.Width + source.Position.X + start.X - ox) * 4 + 3;
                var dst = (py - bounds.Position.Y) * bounds.Size.X + start.X - bounds.Position.X;

                for (var px = start.X; px < end.X; px++, src += 4, dst++)
                {
                    int alpha = texture.Data[src];

                    if (alpha == 255)
                    {
                        pixels[dst] = 255;
                    }
                    else if (alpha != 0)
                    {
                        pixels[dst] = (byte)(alpha + (pixels[dst] * (255 - alpha) + 127) / 255);
                    }
                }
            }
        }

        private static void BlitSampled(Rect2I bounds, Rect2I source, Transform2D transform, Vector2I start, Vector2I end, (byte[] Data, int Width) texture, float opacity, byte[] pixels)
        {
            var inverse = transform.AffineInverse();

            for (var py = start.Y; py < end.Y; py++)
            {
                for (var px = start.X; px < end.X; px++)
                {
                    var sample = inverse * new Vector2(px + 0.5f, py + 0.5f);
                    var sx = Mathf.FloorToInt(sample.X);
                    var sy = Mathf.FloorToInt(sample.Y);

                    if (sx < 0 || sy < 0 || sx >= source.Size.X || sy >= source.Size.Y)
                    {
                        continue;
                    }

                    var texel = texture.Data[((source.Position.Y + sy) * texture.Width + source.Position.X + sx) * 4 + 3];
                    var alpha = Mathf.Clamp(Mathf.RoundToInt(texel * opacity), 0, 255);
                    var index = (py - bounds.Position.Y) * bounds.Size.X + px - bounds.Position.X;

                    pixels[index] = (byte)(alpha + (pixels[index] * (255 - alpha) + 127) / 255);
                }
            }
        }

        #endregion

        #region Core - Material

        public static void UpdateMaterial(ShaderMaterial material, ref ImageTexture texture, Image image)
        {
            if (texture == null || texture.GetSize() != image.GetSize())
            {
                texture = ImageTexture.CreateFromImage(image);

                material.SetShaderParameter("terrain_mask", texture);
            }
            else
            {
                texture.Update(image);
            }
        }

        #endregion

        #region Utils

        private static Transform2D Flips(bool transpose, bool horizontal, bool vertical)
        {
            var x = horizontal ? -1f : 1f;
            var y = vertical ? -1f : 1f;

            if (transpose)
            {
                return new Transform2D(new Vector2(0, y), new Vector2(x, 0), Vector2.Zero);
            }

            return new Transform2D(new Vector2(x, 0), new Vector2(0, y), Vector2.Zero);
        }

        private static TexturePixels Pixels(Texture2D texture)
        {
            var cached = PixelCache.GetValue(texture, key =>
            {
                var entry = new TexturePixels();

                key.Changed += () => entry.Data = null;

                return entry;
            });

            if (cached.Data == null)
            {
                using var image = texture.GetImage();

                if (image.IsCompressed())
                {
                    image.Decompress();
                }

                image.Convert(Image.Format.Rgba8);

                cached.Data = image.GetData();
                cached.Width = image.GetWidth();
            }

            return cached;
        }

        private static float GetTextureReach(TileSet tileSet)
        {
            var cached = ReachCaches.GetValue(tileSet, key =>
            {
                var entry = new ReachCache();

                key.Changed += () => entry.Valid = false;

                return entry;
            });

            if (cached.Valid)
            {
                return cached.Value;
            }

            var reach = 0f;

            for (var s = 0; s < tileSet.GetSourceCount(); s++)
            {
                if (tileSet.GetSource(tileSet.GetSourceId(s)) is not TileSetAtlasSource atlas)
                {
                    continue;
                }

                for (var t = 0; t < atlas.GetTilesCount(); t++)
                {
                    var coords = atlas.GetTileId(t);
                    var size = atlas.GetTileTextureRegion(coords).Size;

                    for (var a = 0; a < atlas.GetAlternativeTilesCount(coords); a++)
                    {
                        var data = atlas.GetTileData(coords, atlas.GetAlternativeTileId(coords, a));
                        var origin = data.TextureOrigin.Abs();

                        reach = Mathf.Max(reach, Mathf.Max(size.X, size.Y) * 0.5f + Mathf.Max(origin.X, origin.Y));
                    }
                }
            }

            cached.Value = reach;
            cached.Valid = true;

            return reach;
        }

        #endregion

        #region Types

        private sealed class TexturePixels
        {
            public byte[] Data;
            public int Width;
        }

        private sealed class ReachCache
        {
            public float Value;
            public bool Valid;
        }

        #endregion
    }
}
