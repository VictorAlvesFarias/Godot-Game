using Godot;
using Jogo25D.Constants;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jogo25D.Biomes
{
    [Tool]
    public partial class TerrainLayer : TileMapLayer
    {
        private static readonly Vector2I[] NeighborOffsets = new Vector2I[]
        {
            new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(1, -1),
            new Vector2I(-1, 0), new Vector2I(1, 0),
            new Vector2I(-1, 1), new Vector2I(0, 1), new Vector2I(1, 1),
        };

        #region Regras de conexao

        private const string ConnectionsHint = "24/17:" + nameof(TerrainConnectionRule);

        private Godot.Collections.Array<Resource> _connections = new();

        [Export(PropertyHint.TypeString, ConnectionsHint)]
        public Godot.Collections.Array<Resource> Connections
        {
            get => _connections;
            set
            {
                _connections = value;
                NotifyConfigChanged();
            }
        }

        private bool _useGodotNativeAutotile;

        [Export]
        public bool UseGodotNativeAutotile
        {
            get => _useGodotNativeAutotile;
            set
            {
                _useGodotNativeAutotile = value;
                NotifyConfigChanged();
            }
        }

        private bool _useConnections;

        [Export]
        public bool UseConnections
        {
            get => _useConnections;
            set
            {
                _useConnections = value;
                NotifyConfigChanged();
            }
        }

        private ConnectionsMode _mode = ConnectionsMode.Blocklist;

        [Export]
        public ConnectionsMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                NotifyConfigChanged();
            }
        }

        private const string RelationsHint = "24/17:" + nameof(TileDirectionRelation);

        private Godot.Collections.Array<Resource> _relations = new();

        [Export(PropertyHint.TypeString, RelationsHint)]
        public Godot.Collections.Array<Resource> Relations
        {
            get => _relations;
            set
            {
                _relations= value;
                BuildRelationSet();
                NotifyConfigChanged();
            }
        }

        private HashSet<(int TerrainSet, Vector2I AtlasCoord, TileSet.CellNeighbor Direction)> _relationSet;

        private void NotifyConfigChanged()
        {
            if (!Engine.IsEditorHint() || !IsInsideTree())
            {
                return;
            }

            TriggerRecalculate();
        }

        public bool AreConnected(int terrainSetA, int terrainSetB)
        {
            if (terrainSetA == terrainSetB)
            {
                return true;
            }

            if (!UseConnections)
            {
                return false;
            }

            var hasRule = false;

            foreach (var resource in Connections)
            {
                if (resource == null)
                {
                    continue;
                }

                var ruleAVariant = resource.Get(TerrainConnectionRule.PropertyName.TerrainSetA);
                var ruleBVariant = resource.Get(TerrainConnectionRule.PropertyName.TerrainSetB);

                if (ruleAVariant.VariantType != Variant.Type.Int || ruleBVariant.VariantType != Variant.Type.Int)
                {
                    continue;
                }

                var ruleA = ruleAVariant.AsInt32();
                var ruleB = ruleBVariant.AsInt32();

                if ((ruleA == terrainSetA && ruleB == terrainSetB) || (ruleA == terrainSetB && ruleB == terrainSetA))
                {
                    hasRule = true;
                    break;
                }
            }

            return Mode == ConnectionsMode.Blocklist
                ? !hasRule
                : hasRule;
        }

        private void BuildRelationSet()
        {
            _relationSet = new HashSet<(int, Vector2I, TileSet.CellNeighbor)>();

            foreach (var resource in Relations)
            {
                if (resource == null)
                {
                    continue;
                }

                var terrainSetVariant = resource.Get(TileDirectionRelation.PropertyName.TerrainSet);
                var atlasCoordVariant = resource.Get(TileDirectionRelation.PropertyName.AtlasCoord);
                var directionsVariant = resource.Get(TileDirectionRelation.PropertyName.Directions);

                if (terrainSetVariant.VariantType != Variant.Type.Int
                    || atlasCoordVariant.VariantType != Variant.Type.Vector2I
                    || directionsVariant.VariantType != Variant.Type.Array)
                {
                    continue;
                }

                var terrainSet = terrainSetVariant.AsInt32();
                var atlasCoord = atlasCoordVariant.AsVector2I();
                var directions = directionsVariant.AsGodotArray<TileSet.CellNeighbor>();

                foreach (var direction in directions)
                {
                    _relationSet.Add((terrainSet, atlasCoord, direction));
                }
            }
        }

        #endregion

        #region Mapeamento terrain_set -> tabela de tiles por assinatura de bits

        private static readonly TileSet.CellNeighbor[] SignatureBits = new[]
        {
            TileSet.CellNeighbor.TopLeftCorner, TileSet.CellNeighbor.TopSide, TileSet.CellNeighbor.TopRightCorner,
            TileSet.CellNeighbor.LeftSide, TileSet.CellNeighbor.RightSide,
            TileSet.CellNeighbor.BottomLeftCorner, TileSet.CellNeighbor.BottomSide, TileSet.CellNeighbor.BottomRightCorner,
        };

        private Dictionary<int, Dictionary<int, TerrainTileMatch>> _tilesByTerrainSetAndSignature;
        private bool _isRecalculating;

        private const int ReferenceQuadrantSize = 16;

        public override void _Ready()
        {
            var tileSize = TileSet?.TileSize.X ?? ChunkStreamingConstants.REFERENCE_TILE_SIZE;

            RenderingQuadrantSize = Mathf.Max(1, Mathf.RoundToInt(ReferenceQuadrantSize * ChunkStreamingConstants.REFERENCE_TILE_SIZE / (float)tileSize));

            BuildTerrainMap();
            BuildRelationSet();
            RedrawDebugOverlay();
        }

        public override void _UpdateCells(Godot.Collections.Array<Vector2I> coords, bool forcedCleanup)
        {
            if (!Engine.IsEditorHint() || forcedCleanup || _isRecalculating)
            {
                return;
            }

            TriggerRecalculate();
        }

        private void TriggerRecalculate()
        {
            if (_isRecalculating)
            {
                return;
            }

            _isRecalculating = true;

            try
            {
                BuildTerrainMap();
                BuildRelationSet();
                ReconnectExistingCells(GetUsedCells());
            }
            finally
            {
                _isRecalculating = false;
            }

            RedrawDebugOverlay();
        }

        private void BuildTerrainMap()
        {
            _tilesByTerrainSetAndSignature = new Dictionary<int, Dictionary<int, TerrainTileMatch>>();

            if (TileSet == null)
            {
                return;
            }

            for (int i = 0; i < TileSet.GetSourceCount(); i++)
            {
                var sourceId = TileSet.GetSourceId(i);

                if (TileSet.GetSource(sourceId) is not TileSetAtlasSource atlasSource)
                {
                    continue;
                }

                for (int t = 0; t < atlasSource.GetTilesCount(); t++)
                {
                    var atlasCoord = atlasSource.GetTileId(t);
                    var alternativesCount = atlasSource.GetAlternativeTilesCount(atlasCoord);

                    for (int a = 0; a < alternativesCount; a++)
                    {
                        var alternativeId = atlasSource.GetAlternativeTileId(atlasCoord, a);
                        var tileData = atlasSource.GetTileData(atlasCoord, alternativeId);

                        if (tileData == null || tileData.TerrainSet < 0)
                        {
                            continue;
                        }

                        if (!_tilesByTerrainSetAndSignature.TryGetValue(tileData.TerrainSet, out var bySignature))
                        {
                            bySignature = new Dictionary<int, TerrainTileMatch>();
                            _tilesByTerrainSetAndSignature[tileData.TerrainSet] = bySignature;
                        }

                        var signature = ComputeTileSignature(tileData);

                        if (!bySignature.ContainsKey(signature))
                        {
                            bySignature[signature] = new TerrainTileMatch(sourceId, atlasCoord, alternativeId);
                        }
                    }
                }
            }
        }

        private static int ComputeTileSignature(TileData tileData)
        {
            int signature = 0;

            for (int i = 0; i < SignatureBits.Length; i++)
            {
                if (tileData.GetTerrainPeeringBit(SignatureBits[i]) >= 0)
                {
                    signature |= 1 << i;
                }
            }

            return signature;
        }

        #endregion

        #region Debug - desenho do mapeamento de terreno

        [Export]
        public bool ShowTerrainSetDebug
        {
            get => _showTerrainSetDebug;
            set
            {
                _showTerrainSetDebug = value;
                RedrawDebugOverlay();
            }
        }

        private bool _showTerrainSetDebug;
        private TerrainDebugOverlay _debugOverlay;

        private static readonly (TileSet.CellNeighbor Bit, Vector2I GridPos)[] PeeringBitGrid = new[]
        {
            (TileSet.CellNeighbor.TopLeftCorner, new Vector2I(0, 0)),
            (TileSet.CellNeighbor.TopSide, new Vector2I(1, 0)),
            (TileSet.CellNeighbor.TopRightCorner, new Vector2I(2, 0)),
            (TileSet.CellNeighbor.LeftSide, new Vector2I(0, 1)),
            (TileSet.CellNeighbor.RightSide, new Vector2I(2, 1)),
            (TileSet.CellNeighbor.BottomLeftCorner, new Vector2I(0, 2)),
            (TileSet.CellNeighbor.BottomSide, new Vector2I(1, 2)),
            (TileSet.CellNeighbor.BottomRightCorner, new Vector2I(2, 2)),
        };

        public void RedrawDebugOverlay()
        {
            if (!_showTerrainSetDebug)
            {
                _debugOverlay?.QueueRedraw();

                return;
            }

            if (_debugOverlay == null || !IsInstanceValid(_debugOverlay))
            {
                _debugOverlay = new TerrainDebugOverlay
                {
                    Name = "__TerrainSetDebugOverlay",
                    TerrainLayerOwner = this,
                    ZIndex = 4096,
                    ZAsRelative = false,
                };

                AddChild(_debugOverlay);
            }

            _debugOverlay.QueueRedraw();
        }

        internal void DrawTerrainSetDebug(CanvasItem target)
        {
            var tileSize = TileSet?.TileSize ?? new Vector2I(32, 32);
            var subSize = new Vector2(tileSize.X / 3f, tileSize.Y / 3f);
            var font = ThemeDB.FallbackFont;

            foreach (var cell in GetUsedCells())
            {
                var tileData = GetCellTileData(cell);

                if (tileData == null)
                {
                    continue;
                }

                var terrainSet = tileData.TerrainSet;
                var color = ColorForTerrainSet(terrainSet);
                var topLeft = MapToLocal(cell) - new Vector2(tileSize.X / 2f, tileSize.Y / 2f);
                var cellRect = new Rect2(topLeft, tileSize);

                target.DrawRect(new Rect2(topLeft + subSize, subSize), color with { A = 0.6f }, filled: true);

                foreach (var (bit, gridPos) in PeeringBitGrid)
                {
                    if (tileData.GetTerrainPeeringBit(bit) < 0)
                    {
                        continue;
                    }

                    var subTopLeft = topLeft + new Vector2(gridPos.X * subSize.X, gridPos.Y * subSize.Y);

                    target.DrawRect(new Rect2(subTopLeft, subSize), color with { A = 0.6f }, filled: true);
                }

                for (int i = 1; i < 3; i++)
                {
                    target.DrawLine(topLeft + new Vector2(subSize.X * i, 0), topLeft + new Vector2(subSize.X * i, tileSize.Y), Colors.Black with { A = 0.4f }, 1f);
                    target.DrawLine(topLeft + new Vector2(0, subSize.Y * i), topLeft + new Vector2(tileSize.X, subSize.Y * i), Colors.Black with { A = 0.4f }, 1f);
                }

                target.DrawRect(cellRect, color, filled: false, width: 1.5f);

                var sourceId = GetCellSourceId(cell);
                var atlasCoord = GetCellAtlasCoords(cell);

                target.DrawString(font, topLeft + new Vector2(2, 11), terrainSet.ToString(), HorizontalAlignment.Left, -1, 11, Colors.White);
                target.DrawString(font, topLeft + new Vector2(2, tileSize.Y - 3), $"{sourceId}:{atlasCoord.X},{atlasCoord.Y}", HorizontalAlignment.Left, -1, 9, Colors.White);
            }
        }

        private static Color ColorForTerrainSet(int terrainSet)
        {
            var hue = (terrainSet * 0.6180339887f) % 1f;

            return Color.FromHsv(hue, 0.75f, 1f);
        }

        #endregion

        #region Conexao

        public void Connect(IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            ConnectAsync(cells, terrainSet, cellsPerFrame: int.MaxValue).GetAwaiter().GetResult();
        }

        public async Task ConnectAsync(IReadOnlyCollection<Vector2I> cells, int terrainSet, int cellsPerFrame = 200)
        {
            if (cells.Count == 0)
            {
                return;
            }

            if (UseGodotNativeAutotile)
            {
                var nativeCellsArray = new Godot.Collections.Array<Vector2I>();

                foreach (var cell in cells)
                {
                    SetCell(cell, -1);
                    nativeCellsArray.Add(cell);
                }

                SetCellsTerrainConnect(nativeCellsArray, terrainSet, 0, false);
                RedrawDebugOverlay();

                return;
            }

            if (_tilesByTerrainSetAndSignature == null)
            {
                return;
            }

            var cellSet = new HashSet<Vector2I>(cells);
            var processedSinceYield = 0;

            foreach (var cell in cells)
            {
                if (TryMatchTile(cell, terrainSet, cellSet, out var match))
                {
                    SetCell(cell, match.SourceId, match.AtlasCoord, match.AlternativeId);
                }

                processedSinceYield++;

                if (processedSinceYield >= cellsPerFrame)
                {
                    processedSinceYield = 0;

                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }

            RedrawDebugOverlay();
        }

        private static readonly int[] CornerIndices = new[] { 0, 2, 5, 7 };

        private bool TryMatchTile(Vector2I cell, int terrainSet, HashSet<Vector2I> cellSet, out TerrainTileMatch match)
        {
            match = default;

            if (!_tilesByTerrainSetAndSignature.TryGetValue(terrainSet, out var bySignature))
            {
                return false;
            }

            var cornerConnected = new bool[SignatureBits.Length];
            var expected = new bool[SignatureBits.Length];

            for (int i = 0; i < SignatureBits.Length; i++)
            {
                if (!TryGetCornerFlanks(SignatureBits[i], out _, out _))
                {
                    expected[i] = IsConnectedNeighbor(cell + NeighborOffsets[i], terrainSet, cellSet, SignatureBits[i]);
                }
            }

            for (int i = 0; i < SignatureBits.Length; i++)
            {
                if (!TryGetCornerFlanks(SignatureBits[i], out var flankA, out var flankB))
                {
                    continue;
                }

                cornerConnected[i] = IsConnectedNeighbor(cell + NeighborOffsets[i], terrainSet, cellSet, SignatureBits[i]);

                var flankAIndex = System.Array.IndexOf(SignatureBits, flankA);
                var flankBIndex = System.Array.IndexOf(SignatureBits, flankB);

                expected[i] = expected[flankAIndex] && expected[flankBIndex] && cornerConnected[i];
            }

            if (!TryFindMatch(bySignature, expected, out match))
            {
                return false;
            }

            if (_relationSet == null)
            {
                return true;
            }

            for (int iteration = 0; iteration < SignatureBits.Length; iteration++)
            {
                if (!ApplySelfRelations(cell, terrainSet, match.AtlasCoord, expected, cornerConnected))
                {
                    return true;
                }

                if (!TryFindMatch(bySignature, expected, out match))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ApplySelfRelations(Vector2I cell, int terrainSet, Vector2I selfAtlasCoord, bool[] expected, bool[] cornerConnected)
        {
            var changed = false;

            for (int i = 0; i < SignatureBits.Length; i++)
            {
                if (TryGetCornerFlanks(SignatureBits[i], out _, out _))
                {
                    continue;
                }

                if (!expected[i])
                {
                    continue;
                }

                var neighborCell = cell + NeighborOffsets[i];
                var neighborTileData = GetCellTileData(neighborCell);

                if (neighborTileData == null || neighborTileData.TerrainSet == terrainSet)
                {
                    continue;
                }

                if (_relationSet.Contains((terrainSet, selfAtlasCoord, SignatureBits[i])))
                {
                    expected[i] = false;
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            for (int i = 0; i < SignatureBits.Length; i++)
            {
                if (!TryGetCornerFlanks(SignatureBits[i], out var flankA, out var flankB))
                {
                    continue;
                }

                var flankAIndex = System.Array.IndexOf(SignatureBits, flankA);
                var flankBIndex = System.Array.IndexOf(SignatureBits, flankB);

                expected[i] = expected[flankAIndex] && expected[flankBIndex] && cornerConnected[i];
            }

            return true;
        }

        private static bool TryFindMatch(Dictionary<int, TerrainTileMatch> bySignature, bool[] expected, out TerrainTileMatch match)
        {
            var signature = 0;

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i])
                {
                    signature |= 1 << i;
                }
            }

            for (int dropCount = 0; dropCount <= CornerIndices.Length; dropCount++)
            {
                if (TryFindWithDroppedCorners(bySignature, signature, CornerIndices, dropCount, out match))
                {
                    return true;
                }
            }

            match = default;

            return false;
        }

        private static bool TryFindWithDroppedCorners(Dictionary<int, TerrainTileMatch> bySignature, int signature, int[] cornerIndices, int dropCount, out TerrainTileMatch match)
        {
            if (dropCount == 0)
            {
                return bySignature.TryGetValue(signature, out match);
            }

            match = default;

            var setCorners = new List<int>();

            foreach (var idx in cornerIndices)
            {
                if ((signature & (1 << idx)) != 0)
                {
                    setCorners.Add(idx);
                }
            }

            if (dropCount > setCorners.Count)
            {
                return false;
            }

            foreach (var combo in Combinations(setCorners, dropCount))
            {
                var candidate = signature;

                foreach (var idx in combo)
                {
                    candidate &= ~(1 << idx);
                }

                if (bySignature.TryGetValue(candidate, out match))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<List<int>> Combinations(List<int> items, int count)
        {
            if (count == 0)
            {
                yield return new List<int>();

                yield break;
            }

            for (int i = 0; i <= items.Count - count; i++)
            {
                foreach (var rest in Combinations(items.GetRange(i + 1, items.Count - i - 1), count - 1))
                {
                    rest.Insert(0, items[i]);

                    yield return rest;
                }
            }
        }

        private static bool TryGetCornerFlanks(TileSet.CellNeighbor bit, out TileSet.CellNeighbor flankA, out TileSet.CellNeighbor flankB)
        {
            switch (bit)
            {
                case TileSet.CellNeighbor.TopLeftCorner:
                    flankA = TileSet.CellNeighbor.TopSide; flankB = TileSet.CellNeighbor.LeftSide; return true;
                case TileSet.CellNeighbor.TopRightCorner:
                    flankA = TileSet.CellNeighbor.TopSide; flankB = TileSet.CellNeighbor.RightSide; return true;
                case TileSet.CellNeighbor.BottomLeftCorner:
                    flankA = TileSet.CellNeighbor.BottomSide; flankB = TileSet.CellNeighbor.LeftSide; return true;
                case TileSet.CellNeighbor.BottomRightCorner:
                    flankA = TileSet.CellNeighbor.BottomSide; flankB = TileSet.CellNeighbor.RightSide; return true;
                default:
                    flankA = default; flankB = default; return false;
            }
        }

        private static TileSet.CellNeighbor OppositeDirection(TileSet.CellNeighbor bit)
        {
            switch (bit)
            {
                case TileSet.CellNeighbor.TopLeftCorner: return TileSet.CellNeighbor.BottomRightCorner;
                case TileSet.CellNeighbor.TopSide: return TileSet.CellNeighbor.BottomSide;
                case TileSet.CellNeighbor.TopRightCorner: return TileSet.CellNeighbor.BottomLeftCorner;
                case TileSet.CellNeighbor.LeftSide: return TileSet.CellNeighbor.RightSide;
                case TileSet.CellNeighbor.RightSide: return TileSet.CellNeighbor.LeftSide;
                case TileSet.CellNeighbor.BottomLeftCorner: return TileSet.CellNeighbor.TopRightCorner;
                case TileSet.CellNeighbor.BottomSide: return TileSet.CellNeighbor.TopSide;
                case TileSet.CellNeighbor.BottomRightCorner: return TileSet.CellNeighbor.TopLeftCorner;
                default: return bit;
            }
        }

        private bool IsConnectedNeighbor(Vector2I neighborCell, int terrainSet, HashSet<Vector2I> cellSet, TileSet.CellNeighbor direction)
        {
            if (cellSet.Contains(neighborCell))
            {
                return true;
            }

            if (GetCellSourceId(neighborCell) == -1)
            {
                return false;
            }

            var tileData = GetCellTileData(neighborCell);

            if (tileData == null)
            {
                return false;
            }

            if (tileData.TerrainSet == terrainSet)
            {
                return true;
            }

            var baseConnected = AreConnected(terrainSet, tileData.TerrainSet);

            if (_relationSet == null)
            {
                return baseConnected;
            }

            var neighborAtlasCoord = GetCellAtlasCoords(neighborCell);
            var hasRelation = _relationSet.Contains((tileData.TerrainSet, neighborAtlasCoord, OppositeDirection(direction)));

            return baseConnected && !hasRelation;
        }

        public void ConnectDependent(TileMapLayer dependency, IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            ConnectDependentAsync(dependency, cells, terrainSet, cellsPerFrame: int.MaxValue).GetAwaiter().GetResult();
        }

        public async Task ConnectDependentAsync(TileMapLayer dependency, IReadOnlyCollection<Vector2I> cells, int terrainSet, int cellsPerFrame = 200)
        {
            var activeCells = new List<Vector2I>();

            foreach (var cell in cells)
            {
                if (dependency.GetCellSourceId(cell) == -1)
                {
                    SetCell(cell, -1);

                    continue;
                }

                activeCells.Add(cell);
            }

            await ConnectAsync(activeCells, terrainSet, cellsPerFrame);
        }

        public void ReconnectForeignBorder(IReadOnlyCollection<Vector2I> cells, int terrainSet, HashSet<int> excludedForeignTerrainSets = null)
        {
            ReconnectForeignBorderAsync(cells, terrainSet, cellsPerFrame: int.MaxValue, excludedForeignTerrainSets: excludedForeignTerrainSets).GetAwaiter().GetResult();
        }

        public async Task ReconnectForeignBorderAsync(IReadOnlyCollection<Vector2I> cells, int terrainSet, int cellsPerFrame = 200, HashSet<int> excludedForeignTerrainSets = null)
        {
            foreach (var group in GroupForeignNeighborsByTerrainSet(cells, terrainSet, excludedForeignTerrainSets))
            {
                await ConnectAsync(group.Value, group.Key, cellsPerFrame);
            }
        }

        public void ReconnectForeignBorderDependent(TileMapLayer dependency, IReadOnlyCollection<Vector2I> cells, int terrainSet, HashSet<int> excludedForeignTerrainSets = null)
        {
            ReconnectForeignBorderDependentAsync(dependency, cells, terrainSet, cellsPerFrame: int.MaxValue, excludedForeignTerrainSets: excludedForeignTerrainSets).GetAwaiter().GetResult();
        }

        public async Task ReconnectForeignBorderDependentAsync(TileMapLayer dependency, IReadOnlyCollection<Vector2I> cells, int terrainSet, int cellsPerFrame = 200, HashSet<int> excludedForeignTerrainSets = null)
        {
            foreach (var group in GroupForeignNeighborsByTerrainSet(cells, terrainSet, excludedForeignTerrainSets))
            {
                await ConnectDependentAsync(dependency, group.Value, group.Key, cellsPerFrame);
            }
        }

        public void ReconnectExistingCells(IEnumerable<Vector2I> cells)
        {
            var groups = new Dictionary<int, List<Vector2I>>();

            foreach (var cell in cells)
            {
                if (GetCellSourceId(cell) == -1)
                {
                    continue;
                }

                var tileData = GetCellTileData(cell);

                if (tileData == null)
                {
                    continue;
                }

                if (!groups.TryGetValue(tileData.TerrainSet, out var group))
                {
                    group = new List<Vector2I>();
                    groups[tileData.TerrainSet] = group;
                }

                group.Add(cell);
            }

            foreach (var group in groups)
            {
                Connect(group.Value, group.Key);
            }
        }

        #endregion

        #region Vizinhanca

        public List<Vector2I> GetForeignNeighborCells(IReadOnlyCollection<Vector2I> cells, int terrainSet, HashSet<int> excludedForeignTerrainSets = null)
        {
            var cellSet = new HashSet<Vector2I>(cells);
            var foreign = CollectForeignNeighbors(cellSet, terrainSet, excludedForeignTerrainSets);
            var result = new List<Vector2I>();

            foreach (var entry in foreign)
            {
                result.Add(entry.Cell);
            }

            return result;
        }

        private Dictionary<int, List<Vector2I>> GroupForeignNeighborsByTerrainSet(IReadOnlyCollection<Vector2I> cells, int terrainSet, HashSet<int> excludedForeignTerrainSets = null)
        {
            var cellSet = new HashSet<Vector2I>(cells);
            var foreignNeighbors = CollectForeignNeighbors(cellSet, terrainSet, excludedForeignTerrainSets);
            var groups = new Dictionary<int, List<Vector2I>>();

            foreach (var entry in foreignNeighbors)
            {
                if (!groups.TryGetValue(entry.TerrainSet, out var group))
                {
                    group = new List<Vector2I>();
                    groups[entry.TerrainSet] = group;
                }

                group.Add(entry.Cell);
            }

            return groups;
        }

        private List<TerrainForeignCellInfo> CollectForeignNeighbors(HashSet<Vector2I> cellSet, int terrainSet, HashSet<int> excludedForeignTerrainSets = null)
        {
            var result = new List<TerrainForeignCellInfo>();
            var seen = new HashSet<Vector2I>();

            foreach (var cell in cellSet)
            {
                foreach (var offset in NeighborOffsets)
                {
                    var neighbor = cell + offset;

                    if (cellSet.Contains(neighbor) || !seen.Add(neighbor))
                    {
                        continue;
                    }

                    if (GetCellSourceId(neighbor) == -1)
                    {
                        continue;
                    }

                    var tileData = GetCellTileData(neighbor);

                    if (tileData == null || tileData.TerrainSet == terrainSet)
                    {
                        continue;
                    }

                    if (excludedForeignTerrainSets != null && excludedForeignTerrainSets.Contains(tileData.TerrainSet))
                    {
                        continue;
                    }

                    result.Add(new TerrainForeignCellInfo
                    {
                        Cell = neighbor,
                        TerrainSet = tileData.TerrainSet,
                    });
                }
            }

            return result;
        }

        #endregion
    }
}
