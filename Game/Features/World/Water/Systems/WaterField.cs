using Godot;
using Jogo25D.Constants;
using Jogo25D.Utils.Coordinates;
using System;
using System.Collections.Generic;

namespace Jogo25D.Water
{
    // Agua da dimensao: nivel de 0 (seca) a 255 (cheia) por celula, um byte[1024] por chunk carregado, e a simulacao por
    // celulas ativas. A resolucao fina deixa a agua se nivelar de fato (com poucos niveis sobra uma escada de um nivel por
    // celula); a exibicao usa 8 alturas (Height). C# puro: o servidor simula, os clientes so aplicam o que chega (SetLevel) e desenham.
    public sealed class WaterField
    {
        #region Constructors

        // collides: a celula tem bloco que colide (segura a agua), pela leitura do TerrainCache. simulated: o servidor
        // (ou jogo solo) simula; o cliente so aplica os niveis que chegam, sem conjunto ativo nem registro de mudancas.
        public WaterField(Func<int, int, bool> collides, bool simulated = true)
        {
            _collides = collides;
            Simulated = simulated;
        }

        #endregion

        #region Events

        // Nivel de uma celula mudou (simulacao, edicao ou rede): a camada de exibicao redesenha a celula.
        public event Action<Vector2I, byte> CellChanged;

        #endregion

        #region Dinamic properties

        public const byte Full = 255;
        public const int Heights = 8;
        public const int ChunkCells = ChunkStreamingConstants.CHUNK_SIZE * ChunkStreamingConstants.CHUNK_SIZE;

        // Passos sem mudanca ate a celula sair do conjunto ativo.
        private const int IdleStepsToSleep = 3;

        private readonly Func<int, int, bool> _collides;
        private readonly Dictionary<Vector2I, byte[]> _chunks = new();
        private readonly HashSet<Vector2I> _dirty = new();
        private readonly Dictionary<Vector2I, int> _active = new();
        private readonly Dictionary<Vector2I, Dictionary<int, byte>> _changes = new();
        private readonly HashSet<Vector2I> _changedThisStep = new();

        // Agua que quer entrar num chunk nao carregado: por chunk, as celulas de origem. Elas ficam ativas, sem contar
        // passos parados, ate o chunk carregar ou o servidor adiar a agua (Defer).
        private readonly Dictionary<Vector2I, HashSet<Vector2I>> _wanted = new();
        private readonly HashSet<Vector2I> _waiting = new();

        // Agua movida por um jogador: as celulas acordadas por uma edicao e as que recebem agua delas. So essa agua pede
        // chunks nao carregados; o resto (lago gerado se acomodando ao carregar) trata a borda como parede. A marca fica
        // enquanto a celula tem agua (uma frente que para e volta a andar continua marcada) e sai quando ela dorme seca ou
        // o chunk sai da memoria.
        private readonly HashSet<Vector2I> _driven = new();

        private bool _leftFirst;

        // Rodizio: quando o limite corta o passo, o seguinte comeca de onde este parou.
        private int _cursor;

        public bool Simulated { get; }
        public IEnumerable<Vector2I> LoadedChunks => _chunks.Keys;
        public int ActiveCount => _active.Count;

        #endregion

        #region Core - Chunks

        public bool IsLoaded(Vector2I chunk)
        {
            return _chunks.ContainsKey(chunk);
        }

        // Chunk entra com o estado dado (salvo, gerado ou vindo do servidor); as celulas com agua ficam ativas para a
        // agua se acomodar ao terreno atual e aos vizinhos.
        public void LoadChunk(Vector2I chunk, byte[] levels)
        {
            var data = new byte[ChunkCells];

            if (levels != null)
            {
                Array.Copy(levels, data, Math.Min(levels.Length, ChunkCells));
            }

            _chunks[chunk] = data;
            _dirty.Remove(chunk);

            // Quem esperava este chunk volta a escoar normalmente.
            if (_wanted.Remove(chunk, out var sources))
            {
                _waiting.ExceptWith(sources);
            }

            for (var index = 0; index < ChunkCells; index++)
            {
                if (data[index] == 0)
                {
                    continue;
                }

                var cell = CellOf(chunk, index);

                if (Simulated)
                {
                    ActivateAround(cell);
                }

                CellChanged?.Invoke(cell, data[index]);
            }

            // A agua dos vizinhos que estava parada na borda deste chunk volta a escoar.
            if (Simulated)
            {
                WakeBorder(chunk);
            }
        }

        public void UnloadChunk(Vector2I chunk)
        {
            if (!_chunks.Remove(chunk))
            {
                return;
            }

            _dirty.Remove(chunk);
            _changes.Remove(chunk);

            foreach (var cell in new List<Vector2I>(_active.Keys))
            {
                if (CoordinateUtilities.CellToChunk(cell) == chunk)
                {
                    _active.Remove(cell);
                }
            }

            _driven.RemoveWhere(cell => CoordinateUtilities.CellToChunk(cell) == chunk);
        }

        public byte[] ChunkLevels(Vector2I chunk)
        {
            return _chunks.TryGetValue(chunk, out var data) ? (byte[])data.Clone() : null;
        }

        // Chunks cuja agua mudou desde que foram carregados (ou gravados): vao ao banco.
        public IEnumerable<Vector2I> DirtyChunks()
        {
            return _dirty;
        }

        public bool IsDirty(Vector2I chunk)
        {
            return _dirty.Contains(chunk);
        }

        public void MarkClean(Vector2I chunk)
        {
            _dirty.Remove(chunk);
        }

        public void Clear()
        {
            _chunks.Clear();
            _dirty.Clear();
            _active.Clear();
            _changes.Clear();
            _wanted.Clear();
            _waiting.Clear();
            _driven.Clear();
        }

        // Chunks pedidos desde a ultima chamada, com as celulas de origem; esvazia o registro (as celulas continuam
        // esperando ate o chunk carregar ou Defer).
        public Dictionary<Vector2I, HashSet<Vector2I>> TakeWanted()
        {
            var wanted = new Dictionary<Vector2I, HashSet<Vector2I>>(_wanted);

            _wanted.Clear();

            return wanted;
        }

        // A agua dessas celulas foi adiada: param de esperar e saem do conjunto ativo (voltam com ActivateAround).
        public void Defer(IEnumerable<Vector2I> cells)
        {
            foreach (var cell in cells)
            {
                _waiting.Remove(cell);
                _active.Remove(cell);
                _driven.Remove(cell);
            }
        }

        // Chunks com alguma celula ativa (ou esperando), para decidir o que pode sair da memoria.
        public HashSet<Vector2I> ActiveChunks()
        {
            var chunks = new HashSet<Vector2I>();

            foreach (var cell in _active.Keys)
            {
                chunks.Add(CoordinateUtilities.CellToChunk(cell));
            }

            return chunks;
        }

        // Celulas ativas com agua num chunk.
        public List<Vector2I> ActiveWetCells(Vector2I chunk)
        {
            var cells = new List<Vector2I>();

            foreach (var cell in _active.Keys)
            {
                if (CoordinateUtilities.CellToChunk(cell) == chunk && Level(cell) > 0)
                {
                    cells.Add(cell);
                }
            }

            return cells;
        }

        #endregion

        #region Core - Celulas

        public byte Level(int x, int y)
        {
            return Level(new Vector2I(x, y));
        }

        public byte Level(Vector2I cell)
        {
            return _chunks.TryGetValue(CoordinateUtilities.CellToChunk(cell), out var data) ? data[IndexOf(cell)] : (byte)0;
        }

        // Agua visivel na celula (altura exibida > 0): o que conta para a tocha e para o nado.
        public bool IsWet(Vector2I cell)
        {
            return Height(Level(cell)) > 0;
        }

        // A agua nao entra: bloco que colide, ou chunk nao carregado (a borda funciona como parede ate ele carregar).
        public bool IsBlocked(Vector2I cell)
        {
            return !_chunks.ContainsKey(CoordinateUtilities.CellToChunk(cell)) || _collides(cell.X, cell.Y);
        }

        // Escreve o nivel (edicao no servidor, ou mudanca vinda da rede no cliente): marca o chunk sujo, registra a mudanca
        // para a rede e acorda a celula e os vizinhos.
        public void SetLevel(Vector2I cell, byte level)
        {
            var chunk = CoordinateUtilities.CellToChunk(cell);

            if (!_chunks.TryGetValue(chunk, out var data))
            {
                return;
            }

            var index = IndexOf(cell);

            level = Math.Min(level, Full);

            if (data[index] == level)
            {
                return;
            }

            data[index] = level;
            CellChanged?.Invoke(cell, level);

            if (!Simulated)
            {
                return;
            }

            _dirty.Add(chunk);
            _changedThisStep.Add(cell);

            if (!_changes.TryGetValue(chunk, out var changes))
            {
                _changes[chunk] = changes = new Dictionary<int, byte>();
            }

            changes[index] = level;

            ActivateAround(cell);
        }

        // Como ActivateAround, marcando a agua da celula e dos vizinhos como movida por um jogador (edicao, ou a frente de
        // uma agua adiada que volta).
        public void ActivateAround(Vector2I cell, bool driven)
        {
            ActivateAround(cell);

            if (!driven || !Simulated)
            {
                return;
            }

            foreach (var next in new[] { cell, cell + Vector2I.Left, cell + Vector2I.Right, cell + Vector2I.Up, cell + Vector2I.Down })
            {
                _driven.Add(next);
            }
        }

        public bool IsDriven(Vector2I cell)
        {
            return _driven.Contains(cell);
        }

        // A celula e os 4 vizinhos voltam a ser simulados (terreno mudou ali, ou a agua mexeu).
        public void ActivateAround(Vector2I cell)
        {
            _active[cell] = 0;
            _active[cell + Vector2I.Left] = 0;
            _active[cell + Vector2I.Right] = 0;
            _active[cell + Vector2I.Up] = 0;
            _active[cell + Vector2I.Down] = 0;
        }

        // Altura exibida (0 a 8): um filme de menos de meia altura nao aparece.
        public static int Height(byte level)
        {
            return (level * Heights + Full / 2) / Full;
        }

        public static int IndexOf(Vector2I cell)
        {
            var size = ChunkStreamingConstants.CHUNK_SIZE;
            var chunk = CoordinateUtilities.CellToChunk(cell);

            return (cell.Y - chunk.Y * size) * size + cell.X - chunk.X * size;
        }

        public static Vector2I CellOf(Vector2I chunk, int index)
        {
            var size = ChunkStreamingConstants.CHUNK_SIZE;

            return CoordinateUtilities.ChunkToCell(chunk) + new Vector2I(index % size, index / size);
        }

        private void WakeBorder(Vector2I chunk)
        {
            var size = ChunkStreamingConstants.CHUNK_SIZE;
            var origin = CoordinateUtilities.ChunkToCell(chunk);

            for (var i = 0; i < size; i++)
            {
                WakeIfWet(origin + new Vector2I(-1, i));
                WakeIfWet(origin + new Vector2I(size, i));
                WakeIfWet(origin + new Vector2I(i, -1));
                WakeIfWet(origin + new Vector2I(i, size));
            }
        }

        private void WakeIfWet(Vector2I cell)
        {
            if (Level(cell) > 0)
            {
                _active[cell] = 0;
            }
        }

        #endregion

        #region Core - Simulacao

        // Um passo: as celulas ativas, de baixo para cima e alternando o lado, caem e depois se espalham. Processa ate
        // maxCells (o resto fica para o proximo passo). A agua so muda de celula, entao o total se conserva.
        public void Step(int maxCells)
        {
            var cells = new List<Vector2I>(_active.Keys);
            var leftFirst = _leftFirst;

            _leftFirst = !_leftFirst;

            cells.Sort((a, b) => a.Y != b.Y ? b.Y.CompareTo(a.Y) : leftFirst ? a.X.CompareTo(b.X) : b.X.CompareTo(a.X));

            // Acima do limite, processa uma janela que gira a cada passo, para nenhuma frente ficar parada.
            if (cells.Count > maxCells)
            {
                var start = _cursor % cells.Count;
                var window = new List<Vector2I>(maxCells);

                for (var i = 0; i < maxCells; i++)
                {
                    window.Add(cells[(start + i) % cells.Count]);
                }

                _cursor = start + maxCells;
                cells = window;
            }
            else
            {
                _cursor = 0;
            }

            _changedThisStep.Clear();

            foreach (var cell in cells)
            {
                Flow(cell, leftFirst);
            }

            // Quem nao mudou neste passo conta para dormir; quem mudou ja foi zerado pelo ActivateAround; quem espera um
            // chunk nao dorme.
            foreach (var cell in cells)
            {
                if (_changedThisStep.Contains(cell) || _waiting.Contains(cell) || !_active.TryGetValue(cell, out var idle))
                {
                    continue;
                }

                if (idle + 1 >= IdleStepsToSleep)
                {
                    _active.Remove(cell);

                    if (Level(cell) == 0)
                    {
                        _driven.Remove(cell);
                    }
                }
                else
                {
                    _active[cell] = idle + 1;
                }
            }
        }

        private void Flow(Vector2I cell, bool leftFirst)
        {
            var level = Level(cell);

            if (level == 0 || IsBlocked(cell))
            {
                return;
            }

            // Cair: o quanto couber na celula de baixo.
            var below = cell + Vector2I.Down;
            var belowBlocked = Closed(below, cell);

            if (!belowBlocked)
            {
                var belowLevel = Level(below);
                var move = (byte)Math.Min(level, Full - belowLevel);

                if (move > 0)
                {
                    SetLevel(below, (byte)(belowLevel + move));
                    SetLevel(cell, level -= move);
                    Carry(cell, below);
                }

                if (level == 0 || Level(below) < Full)
                {
                    return;
                }
            }

            // Espalhar: com baixo cheio ou bloqueado, a celula e os lados livres com 2 ou mais a menos ficam com a media
            // (o resto da divisao fica na celula). Media nao inverte diferencas, entao nao oscila.
            var first = leftFirst ? Vector2I.Left : Vector2I.Right;
            var sides = new List<Vector2I>(2);
            var total = (int)level;

            foreach (var side in new[] { cell + first, cell - first })
            {
                if (Level(side) + 2 <= level && !Closed(side, cell))
                {
                    sides.Add(side);
                    total += Level(side);
                }
            }

            if (sides.Count == 0)
            {
                return;
            }

            var share = total / (sides.Count + 1);

            foreach (var side in sides)
            {
                SetLevel(side, (byte)share);
                Carry(cell, side);
            }

            SetLevel(cell, (byte)(total - share * sides.Count));
        }

        // A agua que sai de uma celula movida por um jogador continua marcada.
        private void Carry(Vector2I from, Vector2I to)
        {
            if (_driven.Contains(from))
            {
                _driven.Add(to);
            }
        }

        // A agua nao entra na celula: bloco que colide, ou chunk nao carregado. Num chunk nao carregado, se a celula nao
        // colide (pelo terreno conhecido), a celula de origem pede o chunk e fica esperando.
        private bool Closed(Vector2I target, Vector2I from)
        {
            var chunk = CoordinateUtilities.CellToChunk(target);

            if (_chunks.ContainsKey(chunk))
            {
                return _collides(target.X, target.Y);
            }

            if (Simulated && _driven.Contains(from) && !_collides(target.X, target.Y))
            {
                if (!_wanted.TryGetValue(chunk, out var sources))
                {
                    _wanted[chunk] = sources = new HashSet<Vector2I>();
                }

                sources.Add(from);
                _waiting.Add(from);
            }

            return true;
        }

        // Mudancas por chunk desde a ultima chamada ({indice: nivel}), para a rede; esvazia o registro.
        public Dictionary<Vector2I, Dictionary<int, byte>> DrainChanges()
        {
            var result = new Dictionary<Vector2I, Dictionary<int, byte>>(_changes);

            _changes.Clear();

            return result;
        }

        #endregion
    }
}
