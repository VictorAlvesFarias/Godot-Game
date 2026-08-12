using Godot;
using Jogo25D.Chunks;
using System;
using System.Collections.Generic;
using System.IO;

namespace Jogo25D.Structures
{
	// Arvore "hibrida" (copa base + tufos extras + galhos com tufo proprio) - formato calibrado
	// batendo o olho num "treinamento" visual: o usuario gerou dezenas de arvores com varios
	// algoritmos diferentes (.dev/generate_tree_variations.py), escolheu as que mais gostou
	// (maioria "branching": copa densa dominando o visual, tronco curto mas visivel por baixo,
	// riscos de galho marrom aparecendo POR DENTRO da copa), e os parametros abaixo foram
	// tunados em .dev/tune_hybrid_tree.py ate bater com essa selecao antes de vir pra ca -
	// reproduz a MESMA logica desse script Python, so traduzida pra C#.
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
			public int[] RadiusByRow; // indice 0 = base da copa (encostada no tronco)
		}

		// worldScale e ignorado aqui de proposito: os ranges abaixo foram calibrados 1:1 com
		// .dev/tune_hybrid_tree.py (1 celula = 1 tile). Multiplicar pelo worldScale do
		// ChunkGenerator (tile_size=16 -> 2) dobrava tronco/copa/galhos em relacao ao preview
		// Python e quebrava a silhueta que foi escolhida visualmente.
		private TreeShape GenerateShape(long worldSeed, string dimensionId, int worldX, int worldScale)
		{
			_ = worldScale;

			var trunkHeight = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 0, (7, 12));
			var canopyHeight = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 1, (5, 9));
			var maxRadius = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 2, (3, 6));
			var trunkLean = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 3, (-1, 1));

			var radiusByRow = new int[canopyHeight];

			for (int row = 0; row < canopyHeight; row++)
			{
				// Afunila nos dois extremos (20% de cima e de baixo) - meio da copa fica no
				// raio maximo.
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

				var variation = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX + row, 100, (-1, 1));
				var radius = Mathf.RoundToInt(maxRadius * taper) + variation;

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

		// (x, altura acima do chao - positivo pra cima) do tronco no degrau "step".
		private static Vector2I TrunkPosition(TreeShape shape, int step)
		{
			var progress = shape.TrunkHeight <= 1 ? 0f : (float)step / shape.TrunkHeight;
			var x = Mathf.RoundToInt(shape.TrunkLean * progress);

			return new Vector2I(x, step);
		}

		#endregion

		#region Geracao das celulas (relativas ao chao, X=0/Y=0)

		private const int PreviewTileSize = 16;

		// Gera a arvore inteira (tronco + copa base + tufos + galhos), em coordenadas RELATIVAS
		// ao groundCell (Y positivo = pra cima) - usado tanto pelo calculo de alcance quanto
		// pela pintura de verdade, pra garantir que os dois NUNCA divirjam (a causa do bug
		// reportado: alcance calculado por formula aproximada, sem contar o quanto os galhos/
		// tufos realmente se espalhavam, deixava a copa e os galhos brotarem em posicoes que a
		// checagem de espaco/borda do ChunkGenerator nao esperava).
		private void BuildTree(long worldSeed, string dimensionId, int worldX, int worldScale, List<Vector2I> trunkCells, List<Vector2I> canopyCells)
		{
			var shape = GenerateShape(worldSeed, dimensionId, worldX, worldScale);

			for (int step = 1; step <= shape.TrunkHeight; step++)
			{
				trunkCells.Add(TrunkPosition(shape, step));
			}

			for (int row = 0; row < shape.CanopyHeight; row++)
			{
				var radius = shape.RadiusByRow[row];
				var normalized = shape.CanopyHeight <= 1 ? 0f : (float)row / (shape.CanopyHeight - 1);
				var centerX = shape.TrunkLean + Mathf.RoundToInt(shape.TrunkLean * normalized);
				var y = shape.TrunkHeight + row;

				for (int x = -radius; x <= radius; x++)
				{
					var isEdge = Mathf.Abs(x) >= radius - 1;

					if (isEdge)
					{
						var edgeRoll = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX + centerX + x, 500 + row, (0, 5));

						if (edgeRoll == 0)
						{
							continue;
						}
					}

					canopyCells.Add(new Vector2I(centerX + x, y));
				}
			}

			AddLeafClusters(canopyCells, shape, worldSeed, dimensionId, worldX, worldScale);
			AddBranches(trunkCells, canopyCells, shape, worldSeed, dimensionId, worldX, worldScale);
		}

		// Tufos redondos extras espalhados pela copa - dao a silhueta organica/irregular (menos
		// "bola perfeita") que dominou a selecao.
		private void AddLeafClusters(List<Vector2I> canopyCells, TreeShape shape, long worldSeed, string dimensionId, int worldX, int worldScale)
		{
			_ = worldScale;
			var clusterCount = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 800, (3, 7));

			for (int cluster = 0; cluster < clusterCount; cluster++)
			{
				var salt = 810 + cluster * 10;
				var row = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, salt, (0, shape.CanopyHeight - 1));
				var radius = shape.RadiusByRow[row];
				var x = radius > 0 ? WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, salt + 1, (-radius, radius)) : 0;
				var clusterRadius = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, salt + 2, (1, 3));

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

						canopyCells.Add(new Vector2I(centerX + dx, centerY + dy));
					}
				}
			}
		}

		// Galhos diagonais saindo do tronco, cada um com seu proprio tufo na ponta - da o efeito
		// de "graveto marrom aparecendo por dentro da folhagem rosa". Nascem colados no TOPO do
		// tronco (onde a copa ja comeca), nunca no meio dele - foi exatamente esse o bug visual
		// reportado: nascendo no meio do tronco (a "fracao da altura toda" usada antes), o galho
		// ficava longe demais da copa e sobrava um vao vazio entre os dois.
		private void AddBranches(List<Vector2I> trunkCells, List<Vector2I> canopyCells, TreeShape shape, long worldSeed, string dimensionId, int worldX, int worldScale)
		{
			_ = worldScale;
			var branchCount = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, 700, (2, 4));

			for (int branch = 0; branch < branchCount; branch++)
			{
				var branchSalt = 710 + branch * 10;

				var drop = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, branchSalt, (0, 2));
				var step = Mathf.Max(1, shape.TrunkHeight - drop);

				var direction = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, branchSalt + 1, (0, 1)) == 0 ? -1 : 1;
				var length = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX, branchSalt + 2, (2, 4));

				var start = TrunkPosition(shape, step);

				for (int i = 1; i <= length; i++)
				{
					// O galho sobe conforme se afasta do tronco, mirando de volta pra dentro
					// da copa em vez de reto pro lado.
					var verticalOffset = Mathf.RoundToInt(i * 0.35f);
					var position = start + new Vector2I(direction * i, verticalOffset);

					trunkCells.Add(position);

					// Sorteado A CADA PASSO (igual ao Python) - nao uma vez so pro galho
					// inteiro, senao os tufos ficam uniformes demais ao longo do galho. Varia
					// por "worldX + i" (posicao), nao por salt, senao colidiria com o salt do
					// proximo galho (branchSalt anda de 10 em 10).
					var leafRadius = WorldRandom.StructureRandomInt(worldSeed, dimensionId, Id, worldX + i, branchSalt + 3, (1, 2));

					for (int lx = -leafRadius; lx <= leafRadius; lx++)
					{
						for (int ly = -leafRadius; ly <= leafRadius; ly++)
						{
							if (Mathf.Abs(lx) + Mathf.Abs(ly) > leafRadius + 1)
							{
								continue;
							}

							canopyCells.Add(position + new Vector2I(lx, ly));
						}
					}
				}
			}
		}

		#endregion

		#region StructureDefinition

		// Caixa REAL - gera a arvore inteira (tronco + copa + tufos + galhos) e mede o bloco
		// mais a esquerda, mais a direita e mais ao topo entre TODAS as celulas de verdade,
		// exatamente como CollectCells vai desenhar (mesma chamada, mesmos parametros -
		// BuildTree e deterministico por worldX, entao os dois SEMPRE concordam). Nao e mais
		// uma aproximacao pelo raio da copa base (que ignorava o quanto galho/tufo podiam se
		// esticar pra fora dela).
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
			// Valor conservador para garantir lookback suficiente no chunk anterior.
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
#if DEBUG
			if (!OS.IsDebugBuild())
			{
				return;
			}

			var exportDirectory = ProjectSettings.GlobalizePath("res://.dev/godot/tree");
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
#endif
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
