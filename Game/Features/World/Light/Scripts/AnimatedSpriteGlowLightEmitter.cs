using Godot;
using System.Collections.Generic;

namespace Jogo25D.Light
{
	public enum LightColorMode 
	{ 
        Manual
	}

    [Tool]
	public partial class AnimatedSpriteGlowLightEmitter : Node2D
	{
		private const int MaxCacheEntries = 256;

		[ExportCategory("ConfiguraÃ§Ã£o Opcional")]
		[Export] public NodePath SourceSpritePath { get; set; } = new NodePath("");
		[Export] public bool ProcessOnlyWhenVisible { get; set; } = true;
		[Export(PropertyHint.Range, "1,10,1")] public int BuildEveryNFrames { get; set; } = 2;

		[ExportCategory("Sprite Light")]
		[Export] public LightColorMode SpriteColorMode { get; set; } = LightColorMode.Manual;
		[Export] public float SpriteIntensity { get; set; } = 1.0f;
		[Export] public Color SpriteColor { get; set; } = Colors.White;

		[ExportCategory("Glow Light")]
		[Export] public LightColorMode GlowColorMode { get; set; } = LightColorMode.Manual;
		[Export] public int GlowRadius { get; set; } = 12;
		[Export(PropertyHint.Range, "0.1,8.0,0.1")] public float GlowIntensity { get; set; } = 2.0f;
		[Export] public Color GlowColor { get; set; } = new Color(0.4f, 0.75f, 1.0f, 1.0f);
     	[Export] public Gradient GlowGradient
		{
			get => _glowGradient;
			set
			{
				_glowGradient = value;
			}
		}

		private Gradient _glowGradient;
		private AnimatedSprite2D _sourceSprite;
		private PointLight2D _spriteLight;
		private PointLight2D _glowLight;
		private static readonly Dictionary<CacheKey, CachedTextures> SharedTextureCache = new();
		private string _lastAnimation = string.Empty;
		private int _lastFrame = -1;
		private bool _lastFlipH;
		private bool _lastFlipV;
		private Vector2 _lastOffset = Vector2.Inf;
		private int _lastGlowRadius;
		private ulong _lastGradientId;
		private CacheBuildRequest _pendingBuild;
		private bool _hasPendingBuild;
		private int _buildFrameCountdown;

		private readonly struct CacheKey
		{
			public readonly ulong TextureId;
			public readonly int Radius;
			public readonly ulong GradientId;

			public CacheKey(ulong textureId, int radius, ulong gradientId)
			{
				TextureId = textureId;
				Radius = radius;
				GradientId = gradientId;
			}

			public override bool Equals(object obj)
			{
				return obj is CacheKey other && TextureId == other.TextureId && Radius == other.Radius && GradientId == other.GradientId;
			}

			public override int GetHashCode()
			{
				return System.HashCode.Combine(TextureId, Radius, GradientId);
			}
		}

		private sealed class CachedTextures
		{
			public Texture2D SpriteTexture { get; init; }
			public Texture2D GlowTexture { get; init; }
		}

		private readonly struct CacheBuildRequest
		{
			public readonly CacheKey Key;
			public readonly Texture2D RawTexture;

			public CacheBuildRequest(CacheKey key, Texture2D rawTexture)
			{
				Key = key;
				RawTexture = rawTexture;
			}
		}

		public override void _Ready()
		{
			SetProcess(true);
          ResolveReferences();
			_lastGlowRadius = GlowRadius;
			_lastGradientId = GetGradientId();
			UpdateConfigurationWarnings();
		}

		public override string[] _GetConfigurationWarnings()
		{
			var warnings = new List<string>();
			var hasInside = GetNodeOrNull<PointLight2D>("InsideLight") != null;
			var hasGlow = GetNodeOrNull<PointLight2D>("GlowLight") != null;

			if (!hasInside || !hasGlow)
			{
				warnings.Add("Aviso: O componente gerarÃ¡/requer exatamente 2 pontos de luz neste NÃ³.\nEles precisam se chamar 'InsideLight' (para o centro) e 'GlowLight' (para o halo)!");
			}

			return warnings.ToArray();
		}

		public override void _Process(double delta)
		{
			if (_sourceSprite == null || _spriteLight == null || _glowLight == null)
			{
				ResolveReferences();

				if (_sourceSprite == null || _spriteLight == null || _glowLight == null)
				{
					return;
				}
			}

			Process();
		}

		private void Process()
		{
			if (ProcessOnlyWhenVisible && (!IsVisibleInTree() || (_sourceSprite != null && !_sourceSprite.IsVisibleInTree())))
			{
				return;
			}

			var gradientId = GetGradientId();

			if (GlowRadius != _lastGlowRadius || gradientId != _lastGradientId)
			{
				SharedTextureCache.Clear();
				_hasPendingBuild = false;
				_lastGlowRadius = GlowRadius;
				_lastGradientId = gradientId;
			}

			if (_hasPendingBuild)
			{
				if (_buildFrameCountdown <= 0)
				{
					BuildPendingCache();
					_buildFrameCountdown = Mathf.Max(1, BuildEveryNFrames) - 1;
				}
				else
				{
					_buildFrameCountdown--;
				}
			}

            var animName = _sourceSprite.Animation;

            if (string.IsNullOrEmpty(animName) || _sourceSprite.SpriteFrames == null || !_sourceSprite.SpriteFrames.HasAnimation(animName))
            {
                return;
            }

            var frame = _sourceSprite.Frame;
            var rawTex = _sourceSprite.SpriteFrames.GetFrameTexture(animName, frame);

            if (rawTex == null)
            {
                return;
            }

		   var fH = _sourceSprite.FlipH;
			var fV = _sourceSprite.FlipV;
			var offset = _sourceSprite.Offset;
			var needsTextureUpdate = _lastAnimation != animName || _lastFrame != frame;
			var needsTransformUpdate = _lastFlipH != fH || _lastFlipV != fV || _lastOffset != offset;

			if (needsTextureUpdate)
			{
				var cacheKey = new CacheKey((ulong)rawTex.GetInstanceId(), GlowRadius, _lastGradientId);

				if (!SharedTextureCache.TryGetValue(cacheKey, out var cachedTextures))
				{
					if (_spriteLight.Texture == null || _glowLight.Texture == null)
					{
						cachedTextures = BuildCacheEntry(cacheKey, rawTex);
						if (cachedTextures == null)
						{
							return;
						}
					}
					else
					{
						SchedulePendingBuild(cacheKey, rawTex);
						cachedTextures = null;
					}
				}

				if (cachedTextures != null)
				{
					_spriteLight.Texture = cachedTextures.SpriteTexture;
					_glowLight.Texture = cachedTextures.GlowTexture;
				}
			}

			if (needsTextureUpdate || needsTransformUpdate)
			{
				var lightScale = new Vector2(fH ? -1.0f : 1.0f, fV ? -1.0f : 1.0f);
				_spriteLight.Scale = lightScale;
				_glowLight.Scale = lightScale;
				_spriteLight.Offset = offset;
				_glowLight.Offset = offset;
				_spriteLight.Position = Vector2.Zero;
				_glowLight.Position = Vector2.Zero;
				_spriteLight.TextureScale = 1.0f;
				_glowLight.TextureScale = 1.0f;
			}

			_spriteLight.Visible = _spriteLight.Texture != null;
			_glowLight.Visible = _glowLight.Texture != null;
			_spriteLight.Color = SpriteColor;
			_spriteLight.Energy = SpriteIntensity;
			_glowLight.Color = GlowColor;
			_glowLight.Energy = GlowIntensity;

			_lastAnimation = animName;
			_lastFrame = frame;
			_lastFlipH = fH;
			_lastFlipV = fV;
			_lastOffset = offset;
        }

		private CachedTextures BuildCacheEntry(CacheKey cacheKey, Texture2D rawTex)
		{
			if (SharedTextureCache.TryGetValue(cacheKey, out var existing))
			{
				return existing;
			}

			var paddedImage = CreatePaddedSpriteTexture(rawTex);
			var glowImage = CreateGlowTexture(rawTex);
			var spriteTex = CreateTextureFromImage(paddedImage);
			var glowTex = CreateTextureFromImage(glowImage);

			if (spriteTex == null || glowTex == null)
			{
				return null;
			}

			var generated = new CachedTextures
			{
				SpriteTexture = spriteTex,
				GlowTexture = glowTex
			};

			if (SharedTextureCache.Count >= MaxCacheEntries)
			{
				SharedTextureCache.Clear();
			}

			SharedTextureCache[cacheKey] = generated;
			return generated;
		}

		private void SchedulePendingBuild(CacheKey cacheKey, Texture2D rawTex)
		{
			if (_hasPendingBuild && _pendingBuild.Key.Equals(cacheKey))
			{
				return;
			}

			_pendingBuild = new CacheBuildRequest(cacheKey, rawTex);
			_hasPendingBuild = true;
		}

		private void BuildPendingCache()
		{
			if (!_hasPendingBuild)
			{
				return;
			}

			if (!IsInstanceValid(_pendingBuild.RawTexture))
			{
				_hasPendingBuild = false;
				return;
			}

			BuildCacheEntry(_pendingBuild.Key, _pendingBuild.RawTexture);
			_hasPendingBuild = false;
		}

		private ulong GetGradientId()
		{
			return GlowGradient == null ? 0UL : (ulong)GlowGradient.GetInstanceId();
		}

		private void ResolveReferences()
		{
			if (!IsInstanceValid(_sourceSprite))
			{
				_sourceSprite = !SourceSpritePath.IsEmpty ? GetNodeOrNull<AnimatedSprite2D>(SourceSpritePath) : null;

				if (_sourceSprite == null && GetParent() is AnimatedSprite2D parent)
				{
					_sourceSprite = parent;
				}
			}

			_spriteLight = GetNodeOrNull<PointLight2D>("InsideLight");

			if (_spriteLight == null)
			{
				_spriteLight = new PointLight2D() 
				{ 
					Name = "InsideLight" 
				};
				
				AddChild(_spriteLight);

				if (Engine.IsEditorHint() && GetTree() != null && GetTree().EditedSceneRoot != null)
				{
					_spriteLight.Owner = GetTree().EditedSceneRoot;
				}
			}

			_glowLight = GetNodeOrNull<PointLight2D>("GlowLight");

			if (_glowLight == null)
			{
				_glowLight = new PointLight2D()
				{
					Name = "GlowLight"
                };
				
				AddChild(_glowLight);

				if (Engine.IsEditorHint() && GetTree() != null && GetTree().EditedSceneRoot != null)
				{
					_glowLight.Owner = GetTree().EditedSceneRoot;
				}
			}

			foreach (var child in GetChildren())
			{
				if (child is PointLight2D pl && pl != _spriteLight && pl != _glowLight)
				{
					pl.QueueFree();
				}
			}
		}

		private ImageTexture CreateTextureFromImage(Image img)
		{
           if (img == null || img.IsEmpty())
			{
				return null;
			}

            var tex = ImageTexture.CreateFromImage(img);

			return tex;
		}

        private Image ExtractImageFromRaw(Texture2D sourceTex)
		{
			var origImg = (Image)null;

			if (sourceTex is AtlasTexture atlas && atlas.Atlas != null)
			{
              var atlasImg = atlas.Atlas.GetImage();

				if (atlasImg != null && !atlasImg.IsEmpty())
				{
					if (atlasImg.IsCompressed())
					{
						atlasImg.Decompress();
					}

					if (atlasImg.GetFormat() != Image.Format.Rgba8)
					{
						atlasImg.Convert(Image.Format.Rgba8);
					}
				}

				if (atlasImg != null && !atlasImg.IsEmpty())
				{
					var region = atlas.Region;

					origImg = Image.CreateEmpty((int)region.Size.X, (int)region.Size.Y, false, Image.Format.Rgba8);
					origImg.BlitRect(atlasImg, new Rect2I((int)region.Position.X, (int)region.Position.Y, (int)region.Size.X, (int)region.Size.Y), Vector2I.Zero);
				}
			}
			else
			{
				origImg = sourceTex.GetImage();

				if (origImg != null && !origImg.IsEmpty())
				{
					if (origImg.IsCompressed())
					{
						origImg.Decompress();
					}

					if (origImg.GetFormat() != Image.Format.Rgba8)
					{
						origImg.Convert(Image.Format.Rgba8);
					}
				}
			}

			return origImg;
		}

		private Image CreatePaddedSpriteTexture(Texture2D sourceTex)
		{
			var origImg = ExtractImageFromRaw(sourceTex);

			if (origImg == null || origImg.IsEmpty())
			{
				return null;
			}

			var w = origImg.GetWidth();
			var h = origImg.GetHeight();
			var padding = GlowRadius + 2;
			var newW = w + (padding * 2);
			var newH = h + (padding * 2);
			var processImg = Image.CreateEmpty(newW, newH, false, Image.Format.Rgba8);

			processImg.BlendRect(origImg, new Rect2I(0, 0, w, h), new Vector2I(padding, padding));

			return processImg;
		}

		private Image CreateGlowTexture(Texture2D sourceTex)
		{
            var origImg = ExtractImageFromRaw(sourceTex);

			if (origImg == null || origImg.IsEmpty())
			{
				return null;
			}

            var w = origImg.GetWidth();
			var h = origImg.GetHeight();
			var padding = GlowRadius + 2;
			var newW = w + (padding * 2);
			var newH = h + (padding * 2);
			var processImg = Image.CreateEmpty(newW, newH, false, Image.Format.Rgba8);
			
			processImg.BlendRect(origImg, new Rect2I(0, 0, w, h), new Vector2I(padding, padding));

			var data = processImg.GetData();
			var nearest = new Vector2I[newW * newH];

			for (var i = 0; i < nearest.Length; i++)
			{
				nearest[i] = new Vector2I(-10000, -10000);
			}

			for (var y = 0; y < newH; y++)
			{
				for (var x = 0; x < newW; x++)
				{
					if (data[(y * newW + x) * 4 + 3] > 20)
					{
						nearest[y * newW + x] = new Vector2I(x, y);
					}
				}
			}

			var maxDim = Mathf.Max(newW, newH);
			var step = 1;

			while (step < maxDim)
			{
				step *= 2;
			}

			step /= 2;

			while (step >= 1)
			{
				for (var y = 0; y < newH; y++)
				{
					for (var x = 0; x < newW; x++)
					{
						var idx = y * newW + x;
						var p = nearest[idx];

						for (var dy = -1; dy <= 1; dy++)
						{
							for (var dx = -1; dx <= 1; dx++)
							{
								var nx = x + (dx * step);
								var ny = y + (dy * step);

								if (nx >= 0 && nx < newW && ny >= 0 && ny < newH)
								{
									var nP = nearest[(ny * newW) + nx];

									if (nP.X != -10000)
									{
										var distSqThis = p.X == -10000 ? float.MaxValue : ((x - p.X) * (x - p.X)) + ((y - p.Y) * (y - p.Y));
										var distSqOther = ((x - nP.X) * (x - nP.X)) + ((y - nP.Y) * (y - nP.Y));

										if (distSqOther < distSqThis)
										{
											p = nP;
											nearest[idx] = p;
										}
									}
								}
							}
						}
					}
				}

				step /= 2;
			}

			var processedData = new byte[data.Length];

			for (var y = 0; y < newH; y++)
			{
				for (var x = 0; x < newW; x++)
				{
                   var idx = (y * newW + x) * 4;
					var origAlpha = data[idx + 3] / 255.0f;
					var nP = nearest[y * newW + x];

					if (nP.X != -10000)
					{
						var dist = Mathf.Sqrt(((x - nP.X) * (x - nP.X)) + ((y - nP.Y) * (y - nP.Y)));

						if (dist <= GlowRadius)
						{
							var offset = GlowRadius > 0 ? dist / GlowRadius : 0f;
							var gradColor = GlowGradient != null ? GlowGradient.Sample(offset) : new Color(1, 1, 1, 1f - offset);
							var intensity = gradColor.A;
							var hollowHaloAlpha = intensity * (1.0f - origAlpha);
							processedData[idx] = 255;
							processedData[idx + 1] = 255;
							processedData[idx + 2] = 255;
							processedData[idx + 3] = (byte)Mathf.Clamp(hollowHaloAlpha * 255, 0, 255);
						}
						else
						{
							processedData[idx] = 0;
							processedData[idx + 1] = 0;
							processedData[idx + 2] = 0;
							processedData[idx + 3] = 0;
						}
					}
					else
					{
						processedData[idx] = 0;
						processedData[idx + 1] = 0;
						processedData[idx + 2] = 0;
						processedData[idx + 3] = 0;
					}
				}
			}

            var glowImg = Image.CreateFromData(newW, newH, false, Image.Format.Rgba8, processedData);

			return glowImg;
		}
	}
}
