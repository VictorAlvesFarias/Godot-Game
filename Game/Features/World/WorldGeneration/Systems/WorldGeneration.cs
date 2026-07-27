using Godot;
using System;
using System.Collections.Generic;

public partial class WorldGeneration : TileMapLayer
{
	[Export] public int Width = 400;
	[Export] public int Height = 200;
	[Export] public int SeaLevel = 100;
	[Export] public int SkyLimit = 70;
	[Export] public int PropsSourceId = 1;
	
	[Export] public int TerrainSetId = 0; 
	[Export] public int TerrainId = 0;    

	private Vector2I _chestTile = new Vector2I(1, 18);
	private Vector2I _statueTop = new Vector2I(22, 3);
	private Vector2I _statueMid = new Vector2I(22, 4);
	private Vector2I _statueBase = new Vector2I(22, 5);
	private Vector2I _bushLeft = new Vector2I(11, 18);
	private Vector2I _bushRight = new Vector2I(12, 18);
	
	private Vector2I[] _treeLayer1 = { new Vector2I(27, 17), new Vector2I(28, 17), new Vector2I(29, 17) };
	private Vector2I[] _treeLayer2 = { new Vector2I(26, 16), new Vector2I(27, 16), new Vector2I(28, 16), new Vector2I(29, 16), new Vector2I(30, 16) };
	private Vector2I[] _treeLayer3 = { new Vector2I(26, 15), new Vector2I(27, 15), new Vector2I(28, 15), new Vector2I(29, 15), new Vector2I(30, 15) };
	private Vector2I[] _treeLayer4 = { new Vector2I(26, 14), new Vector2I(27, 14), new Vector2I(28, 14), new Vector2I(29, 14), new Vector2I(30, 14) };

	private FastNoiseLite _heightNoise = new FastNoiseLite();
	private FastNoiseLite _islandNoise = new FastNoiseLite();
	private FastNoiseLite _caveNoise = new FastNoiseLite();

    private Vector2I _chestCommon = new Vector2I(1, 16);
    private Vector2I _chestRare = new Vector2I(1, 18);
    private Vector2I _chestLegendary = new Vector2I(3, 18);

    private bool[,] _occupiedTiles;

    [Export] public PackedScene ChestScene;

    public override void _Ready()
	{
		SetupNoise();
		GenerateWorld();
	}

	private void SetupNoise()
	{
		Random random = new Random();
		int seed = random.Next();

		_heightNoise.Seed = seed;
		_heightNoise.Frequency = 0.008f;
		_heightNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;

		_islandNoise.Seed = seed + 1;
		_islandNoise.Frequency = 0.04f;
		_islandNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_islandNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_islandNoise.FractalOctaves = 4;
		_islandNoise.FractalGain = 0.2f;

		_caveNoise.Seed = seed + 2;
		_caveNoise.Frequency = 0.03f; 
		_caveNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular; 
		_caveNoise.CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean;
		_caveNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue;
	}

	public void GenerateWorld()
	{
        Clear();
		bool[,] map = new bool[Width, Height];
        _occupiedTiles = new bool[Width, Height];

        for (int x = 0; x < Width; x++)
		{
			int groundLevel = SeaLevel + (int)(_heightNoise.GetNoise2D(x, 0) * 20);
			for (int y = 0; y < Height; y++)
			{
                if (y > groundLevel + 10 && !map[x, y - 1] && map[x, y])
                {
                    if (GD.Randf() < 0.05f) SpawnChestNode(x, y - 1);
                }
				
                bool isSolid = false;
				if (y >= groundLevel) isSolid = true;

				int safetyMargin = 30;
				if (y < groundLevel - safetyMargin && _islandNoise.GetNoise2D(x, y) > 0.3f)
				{
					isSolid = true;
				}

				if (isSolid && _caveNoise.GetNoise2D(x, y) < -0.3f)
				{
					isSolid = false;
				}
				map[x, y] = isSolid;
			}
        }

		int smoothingPasses = 2;
		for (int i = 0; i < smoothingPasses; i++)
		{
			map = SmoothMap(map);
		}

        List<Vector2I> surfaceCells = new List<Vector2I>();
        Godot.Collections.Array<Vector2I> terrainCells = new Godot.Collections.Array<Vector2I>();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (map[x, y])
                {
                    terrainCells.Add(new Vector2I(x, y));
                    if (y > 0 && !map[x, y - 1])
                    {
                        surfaceCells.Add(new Vector2I(x, y - 1));
                    }
                }
            }
        }

        SetCellsTerrainConnect(terrainCells, TerrainSetId, TerrainId);

        foreach (Vector2I surfacePos in surfaceCells)
		{
			TrySpawnProp(surfacePos.X, surfacePos.Y, map);
		}
	}

    private bool[,] SmoothMap(bool[,] oldMap)
	{
		bool[,] newMap = new bool[Width, Height];
		for (int x = 0; x < Width; x++)
		{
			for (int y = 0; y < Height; y++)
			{
				int neighbors = GetSurroundingWallCount(x, y, oldMap);
				if (oldMap[x, y])
					newMap[x, y] = neighbors >= 4;
				else
					newMap[x, y] = neighbors >= 5;
			}
		}
		return newMap;
	}

	private int GetSurroundingWallCount(int gridX, int gridY, bool[,] map)
	{
		int wallCount = 0;
		for (int nX = gridX - 1; nX <= gridX + 1; nX++)
		{
			for (int nY = gridY - 1; nY <= gridY + 1; nY++)
			{
				if (nX >= 0 && nX < Width && nY >= 0 && nY < Height)
				{
					if ((nX != gridX || nY != gridY) && map[nX, nY]) wallCount++;
				}
				else wallCount++;
			}
		}
		return wallCount;
	}

    private void SpawnChestNode(int x, int y)
    {
        if (ChestScene == null) return;

        Node2D chest = ChestScene.Instantiate<Node2D>();
        AddChild(chest);

        chest.Position = MapToLocal(new Vector2I(x, y));

        float chance = (float)GD.RandRange(0, 100);
        if (chest.HasMethod("SetRarity"))
        {
            if (chance > 95) chest.Call("SetRarity", "Legendary");
            else if (chance > 75) chest.Call("SetRarity", "Rare");
            else chest.Call("SetRarity", "Common");
        }
    }

    private void SpawnRandomChest(int x, int y)
    {
        float chance = (float)GD.RandRange(0, 100);
        Vector2I selectedChestAtlasPos;

        if (chance > 95)
        {
            selectedChestAtlasPos = _chestLegendary;
        }
        else if (chance > 75)
        {
            selectedChestAtlasPos = _chestRare;
        }
        else
        {
            selectedChestAtlasPos = _chestCommon;
        }

        SetCell(new Vector2I(x, y), PropsSourceId, selectedChestAtlasPos);
    }

    private bool IsAreaClear(int x, int y, int width, int height, bool[,] map)
    {
        int halfWidth = width / 2;
        for (int i = x - halfWidth; i <= x + halfWidth; i++)
        {
            for (int j = y; j > y - height; j--)
            {
                if (i < 0 || i >= Width || j < 0 || j >= Height) return false;
                if (map[i, j] || _occupiedTiles[i, j]) return false;
            }
        }
        return true;
    }

    private void TrySpawnProp(int x, int y, bool[,] map)
    {
        if (_occupiedTiles[x, y]) return;

        if (GD.Randf() > 0.15f) return;
        float itemRand = GD.Randf();

        if (itemRand < 0.10f)
        {
            SpawnRandomChest(x, y);
            _occupiedTiles[x, y] = true;
        }
        else if (itemRand < 0.20f && IsAreaClear(x, y, 1, 3, map))
        {
            SpawnStatue(x, y);
            MarkAreaOccupied(x, y, 1, 3);
        }
        else if (itemRand < 0.50f && IsAreaClear(x, y, 5, 5, map))
        {
            SpawnTree1(x, y);
            MarkAreaOccupied(x, y, 5, 5);
        }
        else if (IsAreaClear(x, y, 2, 1, map))
        {
            SpawnBush(x, y);
            MarkAreaOccupied(x, y, 2, 1);
        }
    }

    private void MarkAreaOccupied(int x, int y, int width, int height)
    {
        int halfWidth = width / 2;
        for (int i = x - halfWidth; i <= x + halfWidth; i++)
        {
            for (int j = y; j > y - height; j--)
            {
                if (i >= 0 && i < Width && j >= 0 && j < Height)
                    _occupiedTiles[i, j] = true;
            }
        }
    }

    private void SpawnStatue(int x, int y)
	{
		SetCell(new Vector2I(x, y), PropsSourceId, _statueBase);
		SetCell(new Vector2I(x, y - 1), PropsSourceId, _statueMid);
		SetCell(new Vector2I(x, y - 2), PropsSourceId, _statueTop);
	}

	private void SpawnTree1(int x, int y)
	{
		SetCell(new Vector2I(x, y), PropsSourceId, new Vector2I(28, 18));
		SetCell(new Vector2I(x - 1, y - 1), PropsSourceId, _treeLayer1[0]);
		SetCell(new Vector2I(x, y - 1), PropsSourceId, _treeLayer1[1]);
		SetCell(new Vector2I(x + 1, y - 1), PropsSourceId, _treeLayer1[2]);

		for (int i = 0; i < 5; i++)
		{
			int offset = i - 2; 
			SetCell(new Vector2I(x + offset, y - 2), PropsSourceId, _treeLayer2[i]);
			SetCell(new Vector2I(x + offset, y - 3), PropsSourceId, _treeLayer3[i]);
			SetCell(new Vector2I(x + offset, y - 4), PropsSourceId, _treeLayer4[i]);
		}
	}

	private void SpawnBush(int x, int y)
	{
		SetCell(new Vector2I(x, y), PropsSourceId, _bushLeft);
		SetCell(new Vector2I(x + 1, y), PropsSourceId, _bushRight);
	}

}
