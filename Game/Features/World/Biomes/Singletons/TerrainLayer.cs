using Godot;
using System.Collections.Generic;

namespace Jogo25D.Biomes
{
    // Script anexado DIRETO numa TileMapLayer (Base/Bordercap/Texture sao cada uma uma instancia
    // desse script, sem node pai centralizador nenhum). Mapeia sozinho, lendo o proprio TileSet,
    // qual atlas coord representa o interior de cada terrain_set - nao depende de BiomeDB nem de
    // nenhum outro node pra saber disso. As regras de conexao (Connections, editavel no
    // Inspector) tambem sao proprias dessa camada - nao existe conceito de regra compartilhada
    // entre camadas diferentes. No editor, qualquer alteracao de tile (_UpdateCells) recalcula
    // sozinha usando essas regras, sem precisar de comando manual.
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

        // Array SEM tipo generico de proposito, e leitura via Get() (API dinamica do Godot) em
        // vez de cast C# pra TerrainConnectionRule: dentro do PROPRIO EDITOR (nao no jogo
        // rodando), um Resource [GlobalClass] customizado as vezes nao resolve pro tipo C# certo
        // no momento em que a cena carrega (limitacao conhecida do Godot 4 C#) - o wrapper fica
        // como Resource generico, e QUALQUER cast C# (direto ou "as") falha ou retorna null. Get()
        // passa por cima disso porque le a propriedade exportada pelo proprio sistema de
        // propriedades do Godot, que sempre sabe o valor certo independente de como o C# resolveu
        // o tipo do objeto.
        [Export] public Godot.Collections.Array Connections { get; set; } = new();

        public bool AreConnected(int terrainSetA, int terrainSetB)
        {
            if (terrainSetA == terrainSetB)
            {
                return true;
            }

            foreach (var entry in Connections)
            {
                if (entry.Obj is not Resource resource)
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
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Mapeamento terrain_set -> tile interior (lido direto do TileSet)

        private Dictionary<int, (int SourceId, Vector2I AtlasCoord)> _interiorTileByTerrainSet;
        private bool _isRecalculating;

        public override void _Ready()
        {
            BuildTerrainMap();
            RedrawDebugOverlay();
        }

        // Hook oficial do proprio TileMapLayer - chamado pelo Godot toda vez que celulas
        // precisam de atualizacao interna, com as coordenadas exatas que mudaram. So liga em
        // editor - em jogo a geracao procedural ja chama Connect/Reconnect explicitamente por
        // chunk, reagir tambem aqui duplicaria trabalho.
        //
        // IMPORTANTE: NAO chamar NotifyRuntimeTileDataUpdate()/UpdateInternals() dentro de
        // TriggerRecalculate() - os dois forcam uma atualizacao interna IMEDIATA, o que reentra
        // nesse mesmo _UpdateCells ainda durante a chamada atual e vira um loop sem fim (call
        // stack overflow). O guard de _isRecalculating sozinho ja evita reconectar em cima de
        // reconexao; o proprio Godot cuida de redesenhar no fim do frame (batched update), sem
        // precisar forcar nada daqui.
        //
        // Nota: qualquer SetCell chamado por script (nao importa o gatilho) faz o dock de pintura
        // de Terrains do editor recarregar e perder a selecao do terreno escolhido na paleta -
        // efeito colateral do proprio editor reagindo a mudanca de dados da camada, nao tem como
        // evitar daqui.
        public override void _UpdateCells(Godot.Collections.Array<Vector2I> coords, bool forcedCleanup)
        {
            if (!Engine.IsEditorHint() || forcedCleanup || _isRecalculating)
            {
                return;
            }

            TriggerRecalculate();
        }

        // Toggle no Inspector (liga/desliga, o valor em si nao importa) - forca um recalculo na
        // hora, sem depender do hook. Atalho manual de reserva.
        [Export]
        public bool Recalculate
        {
            get => false;
            set
            {
                if (!value || !Engine.IsEditorHint())
                {
                    return;
                }

                TriggerRecalculate();
            }
        }

        // Recalcula TODAS as celulas pintadas usando as regras de conexao atuais. Guarda de
        // reentrancia porque ReconnectExistingCells tambem chama SetCell/SetCellsTerrainConnect,
        // o que dispara _UpdateCells de novo.
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
                ReconnectExistingCells(GetUsedCells());
            }
            finally
            {
                _isRecalculating = false;
            }

            RedrawDebugOverlay();
        }

        // Varre o TileSet inteiro uma vez e guarda, pra cada terrain_set encontrado, UM atlas
        // coord de referencia (nao importa qual - so serve pra "disfarçar" uma celula vizinha
        // como se fosse desse terrain_set na hora de calcular conexao). Isso elimina a
        // necessidade de qualquer configuracao manual tipo BiomeDefinition.InteriorAtlasCoord -
        // o proprio .tres ja diz tudo.
        private void BuildTerrainMap()
        {
            _interiorTileByTerrainSet = new Dictionary<int, (int, Vector2I)>();

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

                        if (!_interiorTileByTerrainSet.ContainsKey(tileData.TerrainSet))
                        {
                            _interiorTileByTerrainSet[tileData.TerrainSet] = (sourceId, atlasCoord);
                        }
                    }
                }
            }
        }

        #endregion

        #region Debug - desenho do mapeamento de terreno

        // Toggle no Inspector - desenha por cima de cada celula pintada o mesmo mapeamento de
        // peering bits que a aba Terrains do TileSet mostra pra cada tile. Serve pra ver de cara
        // se duas celulas vizinhas de camadas/biomas diferentes estao realmente com o terrain_set
        // e a conectividade que voce espera, sem precisar abrir o TileSet e procurar o atlas
        // coord manualmente.
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
        private DebugOverlay _debugOverlay;

        // As 9 sub-regioes de uma celula (grade 3x3), na mesma disposicao que a aba Terrains do
        // TileSet usa pra mostrar quais peering bits um tile tem marcados - o meio (Vector2I(1,1))
        // nao e um peering bit de verdade, e so preenchido sempre que a celula tem terreno, pra
        // fechar o desenho visualmente igual ao editor nativo.
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

        // O overlay de debug NAO desenha direto nessa TileMapLayer (via _Draw() dela) porque
        // Base/Bordercap sao desenhadas ANTES das camadas irmas seguintes na arvore (Bordercap e
        // Texture) - o proprio _Draw() delas ficaria escondido atras dos tiles das camadas
        // desenhadas depois. Em vez disso usa um child Node2D dedicado com ZIndex bem alto e
        // ZAsRelative=false, garantindo que fica por cima de QUALQUER outra camada da cena,
        // independente da ordem na arvore.
        private void RedrawDebugOverlay()
        {
            if (!_showTerrainSetDebug)
            {
                _debugOverlay?.QueueRedraw();

                return;
            }

            if (_debugOverlay == null || !IsInstanceValid(_debugOverlay))
            {
                _debugOverlay = new DebugOverlay
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

        // Desenha, em cima de cada celula pintada, o mesmo mapeamento de peering bits que a aba
        // Terrains do TileSet mostra pra cada tile (qual canto/lado esta marcado como parte do
        // terreno) - mas direto no mapa, sobre a celula de verdade, em vez de precisar abrir o
        // TileSet e procurar o atlas coord manualmente. Chamado pelo DebugOverlay filho (ver
        // RedrawDebugOverlay) - "target" recebe os DrawXxx pra ficar no ZIndex alto do overlay,
        // nao no ZIndex normal dessa camada.
        private void DrawTerrainSetDebug(CanvasItem target)
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

                // Meio - representa o terreno em si (sempre preenchido, nao e peering bit).
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

                // Grade 3x3 fina por cima, igual ao preview do editor, so pra separar visualmente
                // as sub-regioes.
                for (int i = 1; i < 3; i++)
                {
                    target.DrawLine(topLeft + new Vector2(subSize.X * i, 0), topLeft + new Vector2(subSize.X * i, tileSize.Y), Colors.Black with { A = 0.4f }, 1f);
                    target.DrawLine(topLeft + new Vector2(0, subSize.Y * i), topLeft + new Vector2(tileSize.X, subSize.Y * i), Colors.Black with { A = 0.4f }, 1f);
                }

                target.DrawRect(cellRect, color, filled: false, width: 1.5f);
                target.DrawString(font, topLeft + new Vector2(2, 11), terrainSet.ToString(), HorizontalAlignment.Left, -1, 11, Colors.White);
            }
        }

        // Cor deterministica (sempre a mesma pro mesmo terrain_set) espalhada pela roda de matiz
        // usando a razao aurea, pra terrain_set vizinhos (0/1, 4/5...) nao ficarem com cores
        // parecidas por acaso.
        private static Color ColorForTerrainSet(int terrainSet)
        {
            var hue = (terrainSet * 0.6180339887f) % 1f;

            return Color.FromHsv(hue, 0.75f, 1f);
        }

        // Node2D auxiliar minimo - so existe pra ter seu proprio ZIndex absoluto acima de tudo.
        // Sem posicao/escala propria (fica exatamente sobreposto ao pai), entao as coordenadas
        // calculadas via MapToLocal() do pai (TerrainLayer) valem direto aqui tambem.
        [Tool]
        private partial class DebugOverlay : Node2D
        {
            public TerrainLayer TerrainLayerOwner { get; set; }

            public override void _Draw()
            {
                if (TerrainLayerOwner != null && TerrainLayerOwner._showTerrainSetDebug)
                {
                    TerrainLayerOwner.DrawTerrainSetDebug(this);
                }
            }
        }

        #endregion

        #region Conexao

        // Conecta "cells" (todas do mesmo terrain_set) com o autotile - vizinhos solidos de OUTRO
        // terrain_set so sao temporariamente disfarcados de interior deste terrain_set SE os dois
        // estiverem marcados como conectados em Connections - senao a fronteira fica crua de
        // proposito. Depois do calculo, os vizinhos disfarcados voltam ao tile real deles.
        public void Connect(IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            if (cells.Count == 0 || _interiorTileByTerrainSet == null)
            {
                return;
            }

            var cellSet = new HashSet<Vector2I>(cells);
            var foreignNeighbors = CollectForeignNeighbors(cellSet, terrainSet);
            var disguisedNeighbors = new List<ForeignCellInfo>();

            if (!_interiorTileByTerrainSet.TryGetValue(terrainSet, out var interiorTile))
            {
                return;
            }

            foreach (var entry in foreignNeighbors)
            {
                if (!AreConnected(terrainSet, entry.TerrainSet))
                {
                    continue;
                }

                disguisedNeighbors.Add(entry);
                SetCell(entry.Cell, interiorTile.SourceId, interiorTile.AtlasCoord);
            }

            var cellsArray = new Godot.Collections.Array<Vector2I>();

            foreach (var cell in cells)
            {
                cellsArray.Add(cell);
            }

            SetCellsTerrainConnect(cellsArray, terrainSet, 0, false);
            RedrawDebugOverlay();

            foreach (var entry in disguisedNeighbors)
            {
                SetCell(entry.Cell, entry.SourceId, entry.AtlasCoord, entry.AlternativeId);
            }
        }

        // Mesma logica do Connect(), mas so mantem as celulas que ainda estao solidas em
        // "dependency" (usado pela camada Base, que so existe onde ha chao) - qualquer celula
        // sem chao correspondente vira vazia aqui tambem.
        public void ConnectDependent(TileMapLayer dependency, IReadOnlyCollection<Vector2I> cells, int terrainSet)
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

            Connect(activeCells, terrainSet);
        }

        // Espelha o Connect() do outro lado da divisa: reconecta as celulas estrangeiras que
        // encostam em "cells", cada uma com o proprio terrain_set dela, tratando "cells" como
        // solido temporario. Assim os dois lados da fronteira ficam com a borda correta.
        public void ReconnectForeignBorder(IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            foreach (var group in GroupForeignNeighborsByTerrainSet(cells, terrainSet))
            {
                Connect(group.Value, group.Key);
            }
        }

        public void ReconnectForeignBorderDependent(TileMapLayer dependency, IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            foreach (var group in GroupForeignNeighborsByTerrainSet(cells, terrainSet))
            {
                ConnectDependent(dependency, group.Value, group.Key);
            }
        }

        // Reconecta cada celula com o terrain_set que ela JA tem (le o TerrainSet do tile atual),
        // agrupando antes de conectar - assim celulas de terrenos diferentes nunca sao repintadas
        // com o terrain de outra. Passada final util depois de Connect+ReconnectForeignBorder:
        // recalcula CADA celula de novo (nao so o subconjunto de fronteira) usando o estado ja
        // estavel dos vizinhos - corrige artefatos de canto que sobram quando so o subconjunto
        // estrangeiro e recalculado.
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

        // Devolve as celulas vizinhas de OUTRO terrain_set que encostam em "cells" - usado pra
        // tambem atualizar a celula do OUTRO lado da fronteira (o vizinho ja pintado).
        public List<Vector2I> GetForeignNeighborCells(IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            var cellSet = new HashSet<Vector2I>(cells);
            var foreign = CollectForeignNeighbors(cellSet, terrainSet);
            var result = new List<Vector2I>();

            foreach (var entry in foreign)
            {
                result.Add(entry.Cell);
            }

            return result;
        }

        private Dictionary<int, List<Vector2I>> GroupForeignNeighborsByTerrainSet(IReadOnlyCollection<Vector2I> cells, int terrainSet)
        {
            var cellSet = new HashSet<Vector2I>(cells);
            var foreignNeighbors = CollectForeignNeighbors(cellSet, terrainSet);
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

        private struct ForeignCellInfo
        {
            public Vector2I Cell;
            public int SourceId;
            public Vector2I AtlasCoord;
            public int AlternativeId;
            public int TerrainSet;
        }

        private List<ForeignCellInfo> CollectForeignNeighbors(HashSet<Vector2I> cellSet, int terrainSet)
        {
            var result = new List<ForeignCellInfo>();
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

                    result.Add(new ForeignCellInfo
                    {
                        Cell = neighbor,
                        SourceId = GetCellSourceId(neighbor),
                        AtlasCoord = GetCellAtlasCoords(neighbor),
                        AlternativeId = GetCellAlternativeTile(neighbor),
                        TerrainSet = tileData.TerrainSet,
                    });
                }
            }

            return result;
        }

        #endregion
    }
}
