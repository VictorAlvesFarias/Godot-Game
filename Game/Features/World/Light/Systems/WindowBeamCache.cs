using Godot;
using System;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    internal sealed class WindowBeamCache
    {
        #region Dinamic properties

        private const int ViewportScale = 4;
        private const float ApertureDegrees = 8;
        private const float Unset = -999;

        public long Revision { get; private set; }

        private LogicalLightWorld _world;
        private ImageTexture _sourceData;
        private ImageTexture _cells;
        private Vector2I _origin;
        private Vector2I _size;
        private long _sourceRevision = -1;
        private long _revision = -1;
        private long _background = -1;
        private float _angle = Unset;
        private float _penumbra = Unset;
        private float _shadowCurve = Unset;
        private float _ambientCurve = Unset;
        private float _depth = Unset;

        #endregion

        #region Node children references

        private SubViewport _localViewport;
        private ShaderMaterial _localMaterial;
        private SubViewport _viewport;
        private ShaderMaterial _material;

        #endregion

        #region Core - Invalidacao

        public void Update(Node parent, LogicalLightWorld world, Vector2I origin, Vector2I size, float angle, float penumbra, float shadowCurve = 1, float ambientCurve = 1, float terrainDepth = 3)
        {
            EnsureViewports(parent);

            var sourcesRevision = SceneLightSources.Revision(parent.GetParent());

            if (_sourceRevision == sourcesRevision
                && _world == world
                && _revision == world.Revision
                && _background == world.BackgroundRevision
                && _origin == origin
                && _size == size
                && _angle == angle
                && _penumbra == penumbra
                && _shadowCurve == shadowCurve
                && _ambientCurve == ambientCurve
                && _depth == terrainDepth)
            {
                return;
            }

            _sourceRevision = sourcesRevision;
            _world = world;
            _revision = world.Revision;
            _background = world.BackgroundRevision;
            _origin = origin;
            _size = size;
            _angle = angle;
            _penumbra = penumbra;
            _shadowCurve = shadowCurve;
            _ambientCurve = ambientCurve;
            _depth = terrainDepth;

            Revision++;

            UpdateSkyCells(world, origin, size, terrainDepth);
            UpdateSolarBeam(size, angle, penumbra, shadowCurve, ambientCurve);
            UpdateLocalBeam(parent.GetParent(), world, origin, size);

            _localViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

            _viewport.GetNode<ColorRect>("Field").QueueRedraw();
        }

        #endregion

        #region Core - Material

        public void Bind(ShaderMaterial material, TileMapLayer grid, bool enabled, Color sunColor = default)
        {
            if (material == null)
            {
                return;
            }

            material.SetShaderParameter("window_sun_color", sunColor == default ? Colors.White : sunColor);
            material.SetShaderParameter("window_beam_ready", enabled && _viewport != null);

            if (_viewport == null)
            {
                return;
            }

            var inverse = grid.GlobalTransform.AffineInverse();
            Vector2 tile = grid.TileSet.TileSize;

            material.SetShaderParameter("window_beam", _viewport.GetTexture());
            material.SetShaderParameter("local_window_beam", _localViewport.GetTexture());
            material.SetShaderParameter("window_cells", _cells);
            material.SetShaderParameter("sky_access", _cells);
            material.SetShaderParameter("beam_origin", grid.ToGlobal(grid.MapToLocal(_origin) - tile / 2));
            material.SetShaderParameter("beam_axis_x", new Vector2(inverse.X.X, inverse.Y.X) / tile.X);
            material.SetShaderParameter("beam_axis_y", new Vector2(inverse.X.Y, inverse.Y.Y) / tile.Y);
            material.SetShaderParameter("beam_size", (Vector2)_size);
        }

        #endregion

        #region Core - Viewports

        private void EnsureViewports(Node parent)
        {
            if (_viewport != null)
            {
                return;
            }

            _material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://Assets/Shaders/window_beam_cache.gdshader")
            };

            _viewport = new SubViewport
            {
                Name = "WindowBeamCache",
                Disable3D = true,
                TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
            };

            _viewport.AddChild(new ColorRect { Name = "Field", Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore });

            parent.AddChild(_viewport);

            _localMaterial = new ShaderMaterial
            {
                Shader = _material.Shader
            };

            _localMaterial.SetShaderParameter("local_mode", true);

            _localViewport = new SubViewport
            {
                Name = "LocalWindowBeamCache",
                Disable3D = true,
                TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
            };

            _localViewport.AddChild(new ColorRect { Name = "Field", Material = _localMaterial });

            parent.AddChild(_localViewport);
        }

        #endregion

        #region Core - Campos

        private void UpdateSkyCells(LogicalLightWorld world, Vector2I origin, Vector2I size, float terrainDepth)
        {
            var bytes = SkyAccessField.Build(world, origin, size, terrainDepth);

            using var image = Image.CreateFromData(size.X, size.Y, false, Image.Format.Rgba8, bytes);

            if (_cells == null)
            {
                _cells = ImageTexture.CreateFromImage(image);
            }
            else
            {
                _cells.SetImage(image);
            }
        }

        private void UpdateSolarBeam(Vector2I size, float angle, float penumbra, float shadowCurve, float ambientCurve)
        {
            _viewport.Size = size * ViewportScale;
            _viewport.GetNode<ColorRect>("Field").Size = size * ViewportScale;

            _material.SetShaderParameter("cells", _cells);
            _material.SetShaderParameter("grid_size", (Vector2)size);
            _material.SetShaderParameter("angle", Mathf.DegToRad(angle));
            _material.SetShaderParameter("aperture", Mathf.DegToRad(ApertureDegrees) * (1 - penumbra) * (1 - penumbra));
            _material.SetShaderParameter("shadow_curve", shadowCurve);
            _material.SetShaderParameter("ambient_curve", ambientCurve);
        }

        private void UpdateLocalBeam(Node level, LogicalLightWorld world, Vector2I origin, Vector2I size)
        {
            var sources = new List<LightSource>();

            SceneLightSources.Collect(level, new Rect2I(origin, size), sources);
            sources.RemoveAll(source => world.HasBackground(source.Cell.X, source.Cell.Y) || world.Opacity(source.Cell.X, source.Cell.Y) > 0);

            using var sourceImage = Image.CreateEmpty(2, Math.Max(1, sources.Count), false, Image.Format.Rgbaf);

            for (var i = 0; i < sources.Count; i++)
            {
                var position = (Vector2)(sources[i].Cell - origin) + new Vector2(.5f, .5f);

                sourceImage.SetPixel(0, i, new Color(position.X, position.Y, 0, 1));
                sourceImage.SetPixel(1, i, sources[i].Color);
            }

            if (_sourceData == null)
            {
                _sourceData = ImageTexture.CreateFromImage(sourceImage);
            }
            else
            {
                _sourceData.SetImage(sourceImage);
            }

            _localViewport.Size = size * ViewportScale;
            _localViewport.GetNode<ColorRect>("Field").Size = size * ViewportScale;

            _localMaterial.SetShaderParameter("cells", _cells);
            _localMaterial.SetShaderParameter("grid_size", (Vector2)size);
            _localMaterial.SetShaderParameter("local_sources", _sourceData);
            _localMaterial.SetShaderParameter("local_count", sources.Count);
        }

        #endregion
    }
}
