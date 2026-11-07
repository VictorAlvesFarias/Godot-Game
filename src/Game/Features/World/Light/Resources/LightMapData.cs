using Godot;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    [Tool, GlobalClass]
    public partial class LightMapData : Resource
    {
        #region Dinamic properties

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float AmbientLightInfluence
        {
            get => _ambientLightInfluence;
            set => Set(ref _ambientLightInfluence, value);
        }

        [Export(PropertyHint.Range, "0.25,16,0.25")]
        public float TerrainLightDepthTiles
        {
            get => _terrainLightDepthTiles;
            set => Set(ref _terrainLightDepthTiles, value);
        }

        [Export]
        public bool DebugShadow
        {
            get => _debugShadow;
            set => Set(ref _debugShadow, value);
        }

        [Export]
        public Color SunColor
        {
            get => _sunColor;
            set => Set(ref _sunColor, value);
        }

        [Export(PropertyHint.Range, "-89,89,0.5")]
        public float SunAngleDegrees
        {
            get => _sunAngleDegrees;
            set => Set(ref _sunAngleDegrees, value);
        }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float SunPenumbra
        {
            get => _sunPenumbra;
            set => Set(ref _sunPenumbra, value);
        }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float SunPenumbraShadowCurve
        {
            get => _sunPenumbraShadowCurve;
            set => Set(ref _sunPenumbraShadowCurve, value);
        }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float SunPenumbraAmbientCurve
        {
            get => _sunPenumbraAmbientCurve;
            set => Set(ref _sunPenumbraAmbientCurve, value);
        }

        [Export(PropertyHint.Range, "0,1,0.01")]
        public float ShadowStrength
        {
            get => _shadowStrength;
            set => Set(ref _shadowStrength, value);
        }

        private float _terrainLightDepthTiles = 3f;
        private bool _debugShadow;
        private Color _sunColor = Colors.White;
        private float _sunAngleDegrees = -10f;
        private float _sunPenumbra;
        private float _sunPenumbraShadowCurve = 1f;
        private float _sunPenumbraAmbientCurve = 1f;
        private float _ambientLightInfluence = 0.75f;
        private float _shadowStrength = 0.65f;

        #endregion

        #region Utils

        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;

            EmitChanged();
        }

        #endregion
    }
}
