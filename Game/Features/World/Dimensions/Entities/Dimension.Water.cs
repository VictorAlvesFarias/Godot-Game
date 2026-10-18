using Godot;
using Jogo25D.Systems;
using Jogo25D.Water;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Dimensions
{
    // Agua da dimensao (so no procedural): o WaterField, a simulacao no servidor a passo fixo, a carga e descarga junto
    // com os chunks e o envio das mudancas aos peers de cada chunk.
    //
    // A agua posta em movimento segue alem dos chunks dos jogadores: quando ela quer entrar num chunk nao carregado, a
    // previsao (WaterReach) diz se ela pode chegar a um chunk de jogador. Se pode, o chunk entra "so de dados" (edicoes e
    // agua, sem tiles e sem envio a ninguem) e a simulacao segue. Se nao, a frente e a regiao alcancavel vao para o banco
    // (agua adiada) e a agua para ali, ate um jogador carregar um chunk da regiao.
    public partial class Dimension
    {
        #region Dinamic properties

        public const double WaterStepSeconds = 0.1;
        public const int MaxWaterCellsPerStep = 4096;

        // Previsoes novas por passo, tempo por quadro para andar as previsoes em curso, e limites da previsao.
        public const int MaxWaterChunkLoadsPerStep = 2;
        private const double WaterSearchMillisecondsPerFrame = 2;
        private const int MaxReachVisited = 200_000;
        private const int MaxBodyCells = 200_000;

        // Teto da busca que so mede a regiao de uma frente ao salvar (feita de uma vez, no save).
        private const int MaxSnapshotVisited = 50_000;

        // Passos sem celula ativa ate um chunk so de dados ser gravado e sair da memoria.
        private const int WaterOnlyIdleSteps = 30;

        // Tipo da agua adiada no banco: pela previsao, ou frente ativa gravada no save.
        private const int PendingDeferred = 0;
        private const int PendingSaved = 1;

        // Chunks residentes so para a agua (sem tiles, longe dos jogadores) e quantos passos estao parados.
        private readonly HashSet<Vector2I> _waterOnly = new();
        private readonly Dictionary<Vector2I, int> _waterOnlyIdle = new();

        // Chunks pedidos pela agua ainda nao tratados, com as celulas de origem.
        private readonly Dictionary<Vector2I, HashSet<Vector2I>> _wantQueue = new();

        // Previsoes em curso por chunk pedido, com as celulas que esperam a resposta.
        private readonly Dictionary<Vector2I, (WaterReach Search, HashSet<Vector2I> Sources)> _searches = new();

        // Chunks cuja previsao deu "nao chega" pela geometria: novos pedidos so param a agua, sem refazer a previsao, ate o
        // terreno mudar ou um jogador carregar um chunk (a resposta pode mudar). Um "nao chega" por falta de volume nao
        // entra aqui: mais agua chegando refaz a previsao.
        private readonly HashSet<Vector2I> _deferredChunks = new();

        // Chunks com agua adiada ja gravada no banco nesta rodada (uma linha por chunk, nao uma por pedido).
        private readonly HashSet<Vector2I> _pendingWritten = new();

        public IReadOnlyCollection<Vector2I> WaterOnlyChunks => _waterOnly;

        private WaterField _water;
        private WaterGenerator _waterGenerator;
        private double _waterClock;

        public WaterField Water => _water;

        #endregion

        #region Godot implementation

        public override void _PhysicsProcess(double delta)
        {
            StepWater(delta);
        }

        #endregion

        #region Core - Agua

        // O campo de agua da dimensao, criado no primeiro uso: simulado no servidor (ou jogo solo), so aplicado no cliente.
        public WaterField EnsureWater()
        {
            if (_water == null && Procedural)
            {
                _water = new WaterField((x, y) => EnsureTerrain().Collides(x, y), IsServerAuthoritative());
                _water.CellChanged += OnWaterCellChanged;
            }

            return _water;
        }

        // Servidor: as edicoes do terreno (nas duas camadas) acordam a agua em volta.
        private void ConnectWaterToTerrain()
        {
            if (Layer != null)
            {
                Layer.CellChanged += OnTerrainCellChanged;
            }

            if (BaseLayer != null)
            {
                BaseLayer.CellChanged += OnTerrainCellChanged;
            }
        }

        // Bloco que colide numa celula com agua tira a agua dela (tapar um furo); qualquer edicao acorda a celula e os
        // vizinhos.
        private void OnTerrainCellChanged(Vector2I cell)
        {
            if (_water == null || !_water.Simulated)
            {
                return;
            }

            // Edicao dentro da regiao de uma agua adiada: a frente volta e a previsao e refeita (um tunel novo pode ligar
            // a agua a um jogador).
            _deferredChunks.Clear();
            _pendingWritten.Clear();
            ResumePendingWater(Jogo25D.Utils.Coordinates.CoordinateUtilities.CellToChunk(cell));

            if (!_water.IsLoaded(Jogo25D.Utils.Coordinates.CoordinateUtilities.CellToChunk(cell)))
            {
                return;
            }

            if (_water.Level(cell) > 0 && EnsureTerrain().Collides(cell.X, cell.Y))
            {
                _water.SetLevel(cell, 0);

                return;
            }

            _water.ActivateAround(cell, driven: true);
        }

        // Minimapa: a agua aparece onde o terreno nao bloqueia luz (agua dentro de madeira fica com a cor da madeira), so
        // nos chunks que algum jogador carregou com tiles. A agua que atravessa chunks so de dados, longe de todos, nao
        // e mapa descoberto (e esticaria a imagem do minimapa ate onde ela escorre).
        private void OnWaterCellChanged(Vector2I cell, byte level)
        {
            if (IsChunkPainted(Jogo25D.Utils.Coordinates.CoordinateUtilities.CellToChunk(cell)) && EnsureTerrain().Opacity(cell.X, cell.Y) == 0)
            {
                _minimap.RecordWater(DimensionId, cell, WaterField.Height(level) > 0);
            }
        }

        public void DropWater()
        {
            _water = null;
            _waterOnly.Clear();
            _waterOnlyIdle.Clear();
            _wantQueue.Clear();
            _searches.Clear();
            _deferredChunks.Clear();
            _pendingWritten.Clear();
            _waterGenerator?.Dispose();
            _waterGenerator = null;
            _waterClock = 0;
        }

        // Servidor: o chunk entra com a agua salva ou, sem linha no banco, com a gerada pela seed. O cliente (que tambem
        // carrega chunks ao pre-carregar um teleporte) nao gera: a agua vem do servidor no LoadChunkReceive.
        private void LoadWater(Vector2I chunk)
        {
            var water = EnsureWater();

            if (water == null || !water.Simulated || water.IsLoaded(chunk))
            {
                return;
            }

            water.LoadChunk(chunk, (WorldStore.IsOpen ? WorldStore.ReadWater(DimensionId, chunk) : null) ?? GenerateWater(chunk));
        }

        // Servidor: grava a agua do chunk que sai, se mudou, e solta da memoria. Com agua em movimento, o chunk fica so de
        // dados: a agua continua ate assentar.
        private void UnloadWater(Vector2I chunk)
        {
            if (_water == null)
            {
                return;
            }

            if (_water.Simulated && _water.ActiveChunks().Contains(chunk))
            {
                _waterOnly.Add(chunk);
                _waterOnlyIdle[chunk] = 0;

                return;
            }

            if (_water.Simulated && _water.IsDirty(chunk) && WorldStore.IsOpen)
            {
                WorldStore.SaveWater(DimensionId, _water, chunk);
            }

            _water.UnloadChunk(chunk);
        }

        // Agua gerada pela seed para um chunk sem estado salvo, ou nula (seco).
        private byte[] GenerateWater(Vector2I chunk)
        {
            _waterGenerator ??= new WaterGenerator(EnsureTerrain());

            return _waterGenerator.Generate(chunk);
        }

        // Bytes de agua do chunk para o payload de carga do cliente (vazio = seco).
        private byte[] WaterPayload(Vector2I chunk)
        {
            return _water?.ChunkLevels(chunk) ?? System.Array.Empty<byte>();
        }

        private void StepWater(double delta)
        {
            if (_water == null || !_water.Simulated)
            {
                return;
            }

            ProcessWaterSearches();

            _waterClock += delta;

            if (_waterClock < WaterStepSeconds)
            {
                return;
            }

            // Sem acumular atraso: um quadro longo vale um passo so.
            _waterClock = Mathf.Min(_waterClock - WaterStepSeconds, WaterStepSeconds);

            _water.Step(MaxWaterCellsPerStep);

            var changes = _water.DrainChanges();

            SendWaterChanges(changes);
            BreakFloodedBlocks(changes);
            HandleWantedChunks();
            ReleaseIdleWaterOnly();
        }

        // Um RPC por chunk com as mudancas do passo, so para os peers que tem o chunk: 3 bytes por celula (indice, nivel).
        private void SendWaterChanges(Dictionary<Vector2I, Dictionary<int, byte>> changes)
        {
            var ownPeerId = OwnPeerId();

            foreach (var (chunk, cells) in changes)
            {
                if (!_loadedPeers.TryGetValue(chunk, out var peers))
                {
                    continue;
                }

                byte[] packed = null;

                foreach (var peerId in peers)
                {
                    if (peerId == ownPeerId || !IsPeerConnected(peerId))
                    {
                        continue;
                    }

                    packed ??= PackWater(cells);

                    RpcId(peerId, nameof(WaterChangesReceive), chunk, packed);
                }
            }
        }

        // A agua que chegou numa celula com bloco que nao vai na agua (tocha) o quebra, com drop.
        private void BreakFloodedBlocks(Dictionary<Vector2I, Dictionary<int, byte>> changes)
        {
            var terrain = EnsureTerrain();

            foreach (var (chunk, cells) in changes)
            {
                foreach (var (index, level) in cells)
                {
                    var cell = WaterField.CellOf(chunk, index);

                    if (WaterField.Height(level) == 0 || terrain.EditedMaterial(cell.X, cell.Y) is var material && material < 0)
                    {
                        continue;
                    }

                    if (Blocks.BlockDB.FindByTerrain(material) is not { BreaksInWater: true } block)
                    {
                        continue;
                    }

                    // Chunk com tiles: a quebra normal (tile, rede e drop). Chunk so de dados: sem tile para quebrar, a
                    // edicao sai do cache (e do banco no save) e o item cai como entidade, salva com o chunk.
                    if (_loaded.Contains(chunk))
                    {
                        Layer.BreakBlockClientRequest(cell);
                    }
                    else
                    {
                        ApplyEdit(cell, Save.Types.TerrainLayerType.Terrain, null);

                        if (!string.IsNullOrEmpty(block.DropItemId))
                        {
                            Jogo25D.Entities.EntitySpawner.SpawnWorldItemRequest(Jogo25D.Items.ItemFactory.CreateInstance(block.DropItemId), Layer.ToGlobal(Layer.MapToLocal(cell)), DimensionId);
                        }
                    }
                }
            }
        }

        // Chunks pedidos pela agua: ate MaxWaterChunkLoadsPerStep por passo, cada um com a previsao. Chega a um jogador:
        // o chunk entra so de dados. Nao chega: a agua e adiada no banco.
        private void HandleWantedChunks()
        {
            foreach (var (chunk, sources) in _water.TakeWanted())
            {
                if (!_wantQueue.TryGetValue(chunk, out var queued))
                {
                    _wantQueue[chunk] = queued = new HashSet<Vector2I>();
                }

                queued.UnionWith(sources);
            }

            var started = 0;

            foreach (var chunk in new List<Vector2I>(_wantQueue.Keys))
            {
                if (started >= MaxWaterChunkLoadsPerStep)
                {
                    break;
                }

                var sources = _wantQueue[chunk];

                _wantQueue.Remove(chunk);

                if (_water.IsLoaded(chunk))
                {
                    continue;
                }

                if (_deferredChunks.Contains(chunk))
                {
                    _water.Defer(sources);

                    continue;
                }

                // Previsao ja em curso para o chunk: as celulas novas esperam a mesma resposta.
                if (_searches.TryGetValue(chunk, out var running))
                {
                    running.Sources.UnionWith(sources);

                    continue;
                }

                started++;
                _searches[chunk] = (StartPrediction(chunk, sources, stopAtPlayer: true), new HashSet<Vector2I>(sources));
            }
        }

        // A cada quadro, anda as previsoes em curso ate WaterSearchMillisecondsPerFrame no total e aplica as que terminam:
        // chega a um jogador, o chunk entra so de dados; nao chega, a agua e adiada no banco.
        private void ProcessWaterSearches()
        {
            if (_searches.Count == 0)
            {
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var chunk in new List<Vector2I>(_searches.Keys))
            {
                var remaining = WaterSearchMillisecondsPerFrame - watch.Elapsed.TotalMilliseconds;

                if (remaining <= 0)
                {
                    break;
                }

                var (search, sources) = _searches[chunk];

                search.Process(remaining);

                if (!search.Done)
                {
                    continue;
                }

                _searches.Remove(chunk);

                if (_water.IsLoaded(chunk))
                {
                    continue;
                }

                var result = search.Outcome;

                if (result.Reaches)
                {
                    LoadWaterOnly(chunk);

                    continue;
                }

                _water.Defer(sources);

                if (!result.BudgetLimited)
                {
                    _deferredChunks.Add(chunk);
                }

                if (WorldStore.IsOpen && _pendingWritten.Add(chunk))
                {
                    WorldStore.WritePendingWater(DimensionId, PendingDeferred, sources, result.Region);
                }
            }
        }

        // Previsao a partir das celulas de origem, para as celulas vizinhas delas no chunk pedido (ou para todas as
        // vizinhas, sem chunk). Volta a busca sem andar; quem chama anda com Process.
        private WaterReach StartPrediction(Vector2I? chunk, IReadOnlyCollection<Vector2I> sources, bool stopAtPlayer, int maxVisited = MaxReachVisited)
        {
            var terrain = EnsureTerrain();
            var targets = new List<Vector2I>();

            foreach (var source in sources)
            {
                foreach (var target in new[] { source + Vector2I.Down, source + Vector2I.Left, source + Vector2I.Right })
                {
                    if (chunk == null || Jogo25D.Utils.Coordinates.CoordinateUtilities.CellToChunk(target) == chunk)
                    {
                        targets.Add(target);
                    }
                }
            }

            var (body, volume) = WaterReach.Body(_water, sources, MaxBodyCells);

            return new WaterReach(targets, body, volume, cell => terrain.Collides(cell.X, cell.Y), _loaded.Contains, EnsureChunkData,
                maxVisited, stopAtPlayer);
        }

        // Carrega um chunk so de dados: edicoes dele e da margem (as celulas da borda colidem certo) e a agua salva ou gerada.
        private void LoadWaterOnly(Vector2I chunk)
        {
            EnsureChunkDataAround(chunk);

            _water.LoadChunk(chunk, (WorldStore.IsOpen ? WorldStore.ReadWater(DimensionId, chunk) : null) ?? GenerateWater(chunk));
            _waterOnly.Add(chunk);
            _waterOnlyIdle[chunk] = 0;
        }

        // Chunks so de dados sem celula ativa por alguns passos: a agua e gravada e o chunk sai da memoria (as edicoes saem
        // depois, pelo ReleaseFarChunkData).
        private void ReleaseIdleWaterOnly()
        {
            if (_waterOnly.Count == 0)
            {
                return;
            }

            var active = _water.ActiveChunks();

            foreach (var chunk in new List<Vector2I>(_waterOnly))
            {
                if (_loaded.Contains(chunk))
                {
                    _waterOnly.Remove(chunk);
                    _waterOnlyIdle.Remove(chunk);

                    continue;
                }

                if (active.Contains(chunk) || _wantQueue.ContainsKey(chunk) || _searches.ContainsKey(chunk))
                {
                    _waterOnlyIdle[chunk] = 0;

                    continue;
                }

                if ((_waterOnlyIdle[chunk] = _waterOnlyIdle.GetValueOrDefault(chunk) + 1) < WaterOnlyIdleSteps)
                {
                    continue;
                }

                if (_water.IsDirty(chunk) && WorldStore.IsOpen)
                {
                    WorldStore.SaveWater(DimensionId, _water, chunk);
                }

                _water.UnloadChunk(chunk);
                _waterOnly.Remove(chunk);
                _waterOnlyIdle.Remove(chunk);
            }
        }

        // Retoma a agua adiada cuja regiao contem o chunk: as celulas da frente voltam a ser simuladas de onde pararam (os
        // chunks delas entram so de dados, se precisar).
        private void ResumePendingWater(Vector2I chunk)
        {
            if (_water == null || !_water.Simulated || !WorldStore.IsOpen)
            {
                return;
            }

            // Um chunk de jogador a mais (ou uma edicao) muda o que a agua alcanca.
            _deferredChunks.Clear();
            _pendingWritten.Clear();

            foreach (var frontier in WorldStore.TakePendingWaterAt(DimensionId, chunk))
            {
                foreach (var cell in frontier)
                {
                    var cellChunk = Jogo25D.Utils.Coordinates.CoordinateUtilities.CellToChunk(cell);

                    if (!_water.IsLoaded(cellChunk))
                    {
                        LoadWaterOnly(cellChunk);
                    }

                    _water.ActivateAround(cell, driven: true);
                }
            }
        }

        // Save: a agua em movimento nos chunks so de dados vira agua adiada (com a regiao que pode alcancar), para
        // continuar de onde parou depois de sair e voltar ao mundo. As frentes do save anterior desta sessao saem antes.
        public void SnapshotWaterFronts()
        {
            if (_water == null || !_water.Simulated || !WorldStore.IsOpen)
            {
                return;
            }

            WorldStore.DeleteSavedPendingWater(DimensionId);

            foreach (var chunk in _waterOnly)
            {
                var cells = _water.ActiveWetCells(chunk);

                if (cells.Count == 0)
                {
                    continue;
                }

                var search = StartPrediction(null, cells, stopAtPlayer: false, MaxSnapshotVisited);

                search.Process(double.PositiveInfinity);

                var region = search.Outcome.Region;

                region.Add(chunk);

                WorldStore.WritePendingWater(DimensionId, PendingSaved, cells, region);
            }
        }

        private static byte[] PackWater(Dictionary<int, byte> cells)
        {
            var packed = new byte[cells.Count * 3];
            var offset = 0;

            foreach (var (index, level) in cells)
            {
                packed[offset++] = (byte)(index & 0xFF);
                packed[offset++] = (byte)(index >> 8);
                packed[offset++] = level;
            }

            return packed;
        }

        #endregion

        #region Core - Rpc - Agua

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void WaterChangesReceive(Vector2I chunk, byte[] packed)
        {
            var water = EnsureWater();

            if (water == null || !water.IsLoaded(chunk))
            {
                return;
            }

            for (var offset = 0; offset + 2 < packed.Length; offset += 3)
            {
                var index = packed[offset] | packed[offset + 1] << 8;

                water.SetLevel(WaterField.CellOf(chunk, index), packed[offset + 2]);
            }
        }

        #endregion
    }
}
