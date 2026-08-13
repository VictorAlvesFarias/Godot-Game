using Godot;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using System;
using System.Collections.Generic;
using System.IO;

namespace Jogo25D.Structures
{

	public class TreeStructureDefinition : StructureDefinition
	{
		public const int WoodTerrainSet = 6;
		public const int LeafTerrainSet = 7;

		private static readonly int[] _terrainSets = { WoodTerrainSet, LeafTerrainSet };

		public override IReadOnlyCollection<int> TerrainSets => _terrainSets;

		#region Formato

		private sealed class TreeShape
		{
			public int TrunkHeight;
			public int CanopyHeight;
			public int TrunkLean;
			public int[] RadiusByRow;
		}

		private sealed class TreeRandom
		{
			private readonly uint[] _state = new uint[624];
			private int _index = 624;

			public TreeRandom(uint seed)
			{
				Initialize(seed);
			}

			private void Initialize(uint seed)
			{
				_state[0] = seed;
				for (var i = 1; i < _state.Length; i++)
				{
					var previous = _state[i - 1];
					_state[i] = unchecked(1812433253u * (previous ^ (previous >> 30)) + (uint)i);
				}

				_index = _state.Length;
			}

			private void Twist()
			{
				for (var i = 0; i < _state.Length; i++)
				{
					var y = (_state[i] & 0x80000000u) | (_state[(i + 1) % _state.Length] & 0x7fffffffu);
					var next = _state[(i + 397) % _state.Length] ^ (y >> 1);

					if ((y & 1u) != 0)
					{
						next ^= 0x9908b0dfu;
					}

					_state[i] = next;
				}

				_index = 0;
			}

			private uint NextUInt32()
			{
				if (_index >= _state.Length)
				{
					Twist();
				}

				var value = _state[_index++];
				value ^= value >> 11;
				value ^= (value << 7) & 0x9d2c5680u;
				value ^= (value << 15) & 0xefc60000u;
				value ^= value >> 18;

				return value;
			}

			public double NextDouble()
			{
				return (((NextUInt32() >> 5) * 67108864.0) + (NextUInt32() >> 6)) / 9007199254740992.0;
			}

			public int NextInt(int minInclusive, int maxInclusive)
			{
				if (maxInclusive < minInclusive)
				{
					return minInclusive;
				}

				var span = maxInclusive - minInclusive + 1;
				return minInclusive + Mathf.Clamp((int)(NextDouble() * span), 0, span - 1);
			}

			public bool NextBool()
			{
				return NextInt(0, 1) == 1;
			}
		}

		private static int RoundToEven(float value)
		{
			return (int)Math.Round(value, MidpointRounding.ToEven);
		}

		private static uint CombineTreeSeed(long worldSeed, string dimensionId, string structureId, int worldX)
		{
			unchecked
			{
				ulong hash = (ulong)worldSeed;
				hash = hash * 397u ^ (ulong)WorldRandom.StableStringHash(dimensionId);
				hash = hash * 397u ^ (ulong)WorldRandom.StableStringHash(structureId);
				hash = hash * 397u ^ (ulong)worldX;

				return (uint)(hash ^ (hash >> 32));
			}
		}

		private TreeShape GenerateShape(TreeRandom rng)
		{
			var trunkHeight = rng.NextInt(7, 12);
			var canopyHeight = rng.NextInt(5, 9);
			var maxRadius = rng.NextInt(3, 6);
			var trunkLean = rng.NextInt(-1, 1);

			var radiusByRow = new int[canopyHeight];

			for (int row = 0; row < canopyHeight; row++)
			{

				var normalized = canopyHeight <= 1 ? 0f : (float)row / (canopyHeight - 1);
				float taper;

				if (normalized < 0.20f)
				{
					taper = normalized / 0.20f;
				}
				else if (normalized > 0.80f)
				{
					taper = (1f - normalized) / 0.20f;
				}
				else
				{
					taper = 1f;
				}

				var variation = rng.NextInt(-1, 1);
				var radius = RoundToEven(maxRadius * taper) + variation;

				radiusByRow[row] = Mathf.Max(0, radius);
			}

			return new TreeShape
			{
				TrunkHeight = trunkHeight,
				CanopyHeight = canopyHeight,
				TrunkLean = trunkLean,
				RadiusByRow = radiusByRow,
			};
		}

		private static Vector2I TrunkPosition(TreeShape shape, int step)
		{
			var progress = shape.TrunkHeight <= 1 ? 0f : (float)step / shape.TrunkHeight;
			var x = RoundToEven(shape.TrunkLean * progress);

			return new Vector2I(x, step);
		}

		#endregion

		#region Geracao das celulas (relativas ao chao, X=0/Y=0)

		private const int PreviewTileSize = 16;

		private void BuildTree(long worldSeed, string dimensionId, int worldX, int worldScale, List<Vector2I> trunkCells, List<Vector2I> canopyCells)
		{
			_ = worldScale;

			var rng = new TreeRandom(CombineTreeSeed(worldSeed, dimensionId, Id, worldX));
			var shape = GenerateShape(rng);
			var trunkCellSet = new HashSet<Vector2I>();
			var canopyCellSet = new HashSet<Vector2I>();

			for (int step = 1; step <= shape.TrunkHeight; step++)
			{
				var trunkCell = TrunkPosition(shape, step);

				if (trunkCellSet.Add(trunkCell))
				{
					trunkCells.Add(trunkCell);
				}
			}

			for (int row = 0; row < shape.CanopyHeight; row++)
			{
				var radius = shape.RadiusByRow[row];
				var normalized = shape.CanopyHeight <= 1 ? 0f : (float)row / (shape.CanopyHeight - 1);
				var centerX = shape.TrunkLean + RoundToEven(shape.TrunkLean * normalized);
				var y = shape.TrunkHeight + row;

				for (int x = -radius; x <= radius; x++)
				{
					var isEdge = Mathf.Abs(x) >= radius - 1;

					if (isEdge)
					{
						var edgeRoll = rng.NextInt(0, 5);

						if (edgeRoll == 0)
						{
							continue;
						}
					}

					var canopyCell = new Vector2I(centerX + x, y);

					if (trunkCellSet.Contains(canopyCell) || !canopyCellSet.Add(canopyCell))
					{
						continue;
					}

					canopyCells.Add(canopyCell);
				}
			}

			AddLeafClusters(canopyCells, trunkCellSet, canopyCellSet, shape, rng, worldSeed, dimensionId, worldX, worldScale);
			AddBranches(trunkCells, trunkCellSet, canopyCells, canopyCellSet, shape, rng, worldSeed, dimensionId, worldX, worldScale);
		}

		private void AddLeafClusters(List<Vector2I> canopyCells, HashSet<Vector2I> trunkCellSet, HashSet<Vector2I> canopyCellSet, TreeShape shape, TreeRandom rng, long worldSeed, string dimensionId, int worldX, int worldScale)
		{
			_ = worldSeed;
			_ = dimensionId;
			_ = worldX;
			_ = worldScale;
			var clusterCount = rng.NextInt(3, 7);

			for (int cluster = 0; cluster < clusterCount; cluster++)
			{
				var salt = 810 + cluster * 10;
				var row = rng.NextInt(0, shape.CanopyHeight - 1);
				var radius = shape.RadiusByRow[row];
				var x = radius > 0 ? rng.NextInt(-radius, radius) : 0;
				var clusterRadius = rng.NextInt(1, 3);

				var centerX = shape.TrunkLean + x;
				var centerY = shape.TrunkHeight + row;

				for (int dx = -clusterRadius; dx <= clusterRadius; dx++)
				{
					for (int dy = -clusterRadius; dy <= clusterRadius; dy++)
					{
						if (dx * dx + dy * dy > clusterRadius * clusterRadius)
						{
							continue;
						}

						var canopyCell = new Vector2I(centerX + dx, centerY + dy);

						if (trunkCellSet.Contains(canopyCell) || !canopyCellSet.Add(canopyCell))
						{
							continue;
						}

						canopyCells.Add(canopyCell);
					}
				}
			}
		}

		private void AddBranches(List<Vector2I> trunkCells, HashSet<Vector2I> trunkCellSet, List<Vector2I> canopyCells, HashSet<Vector2I> canopyCellSet, TreeShape shape, TreeRandom rng, long worldSeed, string dimensionId, int worldX, int worldScale)
		{
			_ = worldSeed;
			_ = dimensionId;
			_ = worldX;
			_ = worldScale;
			var branchCount = rng.NextInt(2, 4);

			for (int branch = 0; branch < branchCount; branch++)
			{
				var branchSalt = 710 + branch * 10;

				var drop = rng.NextInt(0, 2);
				var step = Mathf.Max(1, shape.TrunkHeight - drop);

				var direction = rng.NextBool() ? 1 : -1;
				var length = rng.NextInt(2, 4);

				var start = TrunkPosition(shape, step);

				for (int i = 1; i <= length; i++)
				{

					var verticalOffset = RoundToEven(i * 0.35f);
					var position = start + new Vector2I(direction * i, verticalOffset);

					if (trunkCellSet.Add(position))
					{
						trunkCells.Add(position);
					}

					var leafRadius = rng.NextInt(1, 2);

					for (int lx = -leafRadius; lx <= leafRadius; lx++)
					{
						for (int ly = -leafRadius; ly <= leafRadius; ly++)
						{
							if (Mathf.Abs(lx) + Mathf.Abs(ly) > leafRadius + 1)
							{
								continue;
							}

							var canopyCell = position + new Vector2I(lx, ly);

							if (trunkCellSet.Contains(canopyCell) || !canopyCellSet.Add(canopyCell))
							{
								continue;
							}

							canopyCells.Add(canopyCell);
						}
					}
				}
			}
		}

		#endregion

		#region StructureDefinition

		public override StructureBounds GetBounds(long worldSeed, string dimensionId, int worldX, int worldScale)
		{
			var trunkCells = new List<Vector2I>();
			var canopyCells = new List<Vector2I>();

			BuildTree(worldSeed, dimensionId, worldX, worldScale, trunkCells, canopyCells);

			var left = 0;
			var right = 0;
			var top = 0;

			void Measure(Vector2I cell)
			{
				if (cell.X < 0) left = Mathf.Max(left, -cell.X);
				if (cell.X > 0) right = Mathf.Max(right, cell.X);
				top = Mathf.Max(top, cell.Y);
			}

			foreach (var cell in trunkCells) Measure(cell);
			foreach (var cell in canopyCells) Measure(cell);

			return new StructureBounds(left, right, top);
		}

		public override int GetMaxRightExtent(int worldScale)
		{

			return 16;
		}

		public override List<StructureCellGroup> CollectCells(Vector2I groundCell, long worldSeed, string dimensionId, int worldScale)
		{
			var trunkCells = new List<Vector2I>();
			var canopyCells = new List<Vector2I>();

			BuildTree(worldSeed, dimensionId, groundCell.X, worldScale, trunkCells, canopyCells);
			ExportTreePreview(trunkCells, canopyCells, worldSeed, dimensionId, groundCell.X);

			var absoluteTrunk = new List<Vector2I>(trunkCells.Count);
			var absoluteCanopy = new List<Vector2I>(canopyCells.Count);

			foreach (var cell in trunkCells) absoluteTrunk.Add(groundCell + new Vector2I(cell.X, -cell.Y));
			foreach (var cell in canopyCells) absoluteCanopy.Add(groundCell + new Vector2I(cell.X, -cell.Y));

			return new List<StructureCellGroup>
			{
				new StructureCellGroup(WoodTerrainSet, absoluteTrunk),
				new StructureCellGroup(LeafTerrainSet, absoluteCanopy),
			};
		}

		private static void ExportTreePreview(List<Vector2I> trunkCells, List<Vector2I> canopyCells, long worldSeed, string dimensionId, int worldX)
		{
			if (!TreeDebugConstants.EnableTreePreviewExport || !OS.IsDebugBuild())
			{
				return;
			}

			var exportDirectory = ProjectSettings.GlobalizePath("res://.temp/godot/tree");
			Directory.CreateDirectory(exportDirectory);

			var left = 0;
			var right = 0;
			var top = 0;

			void Measure(Vector2I cell)
			{
				if (cell.X < 0) left = Mathf.Max(left, -cell.X);
				if (cell.X > 0) right = Mathf.Max(right, cell.X);
				top = Mathf.Max(top, cell.Y);
			}

			foreach (var cell in trunkCells) Measure(cell);
			foreach (var cell in canopyCells) Measure(cell);

			var widthTiles = left + right + 1;
			var heightTiles = top + 1;
			var imageWidth = widthTiles * PreviewTileSize;
			var imageHeight = heightTiles * PreviewTileSize;

			using var image = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);
			image.Fill(new Color(0.117f, 0.117f, 0.133f));

			var trunkColor = new Color(121f / 255f, 74f / 255f, 43f / 255f);
			var trunkShade = new Color(98f / 255f, 58f / 255f, 33f / 255f);
			var leafColor = new Color(235f / 255f, 120f / 255f, 170f / 255f);
			var leafShade = new Color(214f / 255f, 92f / 255f, 148f / 255f);

			var shadeRng = new Random((int)(worldSeed * 7919 + worldX));
			foreach (var cell in trunkCells)
			{
				var fill = shadeRng.NextDouble() < 0.75 ? trunkColor : trunkShade;
				var px = (cell.X + left) * PreviewTileSize;
				var py = (top - cell.Y) * PreviewTileSize;
				image.FillRect(new Rect2I(px, py, PreviewTileSize, PreviewTileSize), fill);
			}

			foreach (var cell in canopyCells)
			{
				var fill = shadeRng.NextDouble() < 0.7 ? leafColor : leafShade;
				var px = (cell.X + left) * PreviewTileSize;
				var py = (top - cell.Y) * PreviewTileSize;
				image.FillRect(new Rect2I(px, py, PreviewTileSize, PreviewTileSize), fill);
			}

			var overlayText = worldX.ToString();
			var textPosition = new Vector2I(4, 4);
			DrawOverlayText(image, overlayText, textPosition, Colors.White, Colors.Black with { A = 0.75f }, 2);

			var safeDimensionId = dimensionId.Replace('/', '_').Replace('\\', '_');
			var fileName = $"tree_{safeDimensionId}_{worldSeed}_{worldX}.png";
			var path = Path.Combine(exportDirectory, fileName);
			var error = image.SavePng(path);

			if (error != Error.Ok)
			{
				GD.PrintErr($"[TreeStructureDefinition] Falha ao salvar preview {path}: {error}");
			}
		}

		private static void DrawOverlayText(Image image, string text, Vector2I position, Color color, Color background, int scale)
		{
			var digitPatterns = new Dictionary<char, string[]>
			{
				['0'] = new[] { "111", "101", "101", "101", "111" },
				['1'] = new[] { "010", "110", "010", "010", "111" },
				['2'] = new[] { "111", "001", "111", "100", "111" },
				['3'] = new[] { "111", "001", "111", "001", "111" },
				['4'] = new[] { "101", "101", "111", "001", "001" },
				['5'] = new[] { "111", "100", "111", "001", "111" },
				['6'] = new[] { "111", "100", "111", "101", "111" },
				['7'] = new[] { "111", "001", "010", "010", "010" },
				['8'] = new[] { "111", "101", "111", "101", "111" },
				['9'] = new[] { "111", "101", "111", "001", "111" },
			};

			var digitWidth = 3 * scale;
			var digitHeight = 5 * scale;
			var spacing = scale;
			var textWidth = text.Length * digitWidth + Math.Max(0, text.Length - 1) * spacing;
			var textHeight = digitHeight;

			image.FillRect(new Rect2I(position.X - 2, position.Y - 2, textWidth + 4, textHeight + 4), background);

			var x = position.X;
			foreach (var character in text)
			{
				if (!digitPatterns.TryGetValue(character, out var pattern))
				{
					x += digitWidth + spacing;
					continue;
				}

				for (var row = 0; row < pattern.Length; row++)
				{
					for (var col = 0; col < pattern[row].Length; col++)
					{
						if (pattern[row][col] != '1')
						{
							continue;
						}

						var px = x + col * scale;
						var py = position.Y + row * scale;
						image.FillRect(new Rect2I(px, py, scale, scale), color);
					}
				}

				x += digitWidth + spacing;
			}
		}

		#endregion
	}
}
