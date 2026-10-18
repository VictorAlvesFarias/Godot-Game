using Godot;
using Jogo25D.Utils.Coordinates;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jogo25D.Water
{
    // Previsao de alcance: ate onde pode chegar a agua que quer sair dos chunks residentes, so pelo terreno, sem simular.
    // A agua nao tem pressao: cai e anda para os lados apoiada, nunca sobe. A busca:
    // - desce livre enquanto ha ar;
    // - anda para os lados so onde a celula de baixo e solida ou ja foi alcancada (onde poderia empocar);
    // - processa sempre a celula mais funda primeiro;
    // - cada celula alcancada pelo lado gasta 1 unidade do volume da agua (na regra de espalhar, toda celula molhada pelo
    //   lado guarda ao menos 1 unidade ate poder cair), entao o orcamento nao corta caminho real.
    // E um limite superior: pode dizer que chega onde a agua pararia antes, nunca o contrario.
    //
    // A busca guarda o estado e anda em fatias de tempo (Process), para uma caverna grande nao travar o quadro.
    public sealed class WaterReach
    {
        #region Structures

        // Reaches: encostou numa celula seca de chunk de jogador (ou estourou o teto de visitas). Region: chunks visitados.
        // BudgetLimited: o volume acabou com caminho ainda aberto (mais agua chegando pode mudar a resposta).
        public readonly record struct Result(bool Reaches, HashSet<Vector2I> Region, int Visited, bool Capped, bool BudgetLimited = false);

        #endregion

        #region Constructors

        // targets: celulas para onde a agua quer ir. body: a agua conectada a ela (nao conta como chegada nem e
        // atravessada). volume: unidades dessa agua. collides: a celula segura agua. isPlayerChunk: chunk carregado por
        // um jogador. prepareChunk: chamado na primeira visita a cada chunk (carregar as edicoes salvas dele).
        // stopAtPlayer: falso para so medir a regiao (sem parar na chegada).
        public WaterReach(IEnumerable<Vector2I> targets, IReadOnlySet<Vector2I> body, long volume, Func<Vector2I, bool> collides,
            Func<Vector2I, bool> isPlayerChunk, Action<Vector2I> prepareChunk, int maxVisited, bool stopAtPlayer = true)
        {
            _body = body;
            _budget = volume;
            _collides = collides;
            _isPlayerChunk = isPlayerChunk;
            _prepareChunk = prepareChunk;
            _maxVisited = maxVisited;
            _stopAtPlayer = stopAtPlayer;

            foreach (var target in targets)
            {
                if (!_visited.Contains(target) && Open(target))
                {
                    Push(target);
                }
            }
        }

        #endregion

        #region Dinamic properties

        private readonly IReadOnlySet<Vector2I> _body;
        private readonly Func<Vector2I, bool> _collides;
        private readonly Func<Vector2I, bool> _isPlayerChunk;
        private readonly Action<Vector2I> _prepareChunk;
        private readonly int _maxVisited;
        private readonly bool _stopAtPlayer;

        private readonly PriorityQueue<Vector2I, int> _queue = new();
        private readonly HashSet<Vector2I> _visited = new();
        private readonly HashSet<Vector2I> _region = new();

        private long _budget;
        private bool _reaches;
        private bool _budgetLimited;

        public bool Done { get; private set; }
        public Result Outcome { get; private set; }

        #endregion

        #region Core - Previsao

        // Busca inteira de uma vez (testes e save).
        public static Result Predict(IEnumerable<Vector2I> targets, IReadOnlySet<Vector2I> body, long volume,
            Func<Vector2I, bool> collides, Func<Vector2I, bool> isPlayerChunk, Action<Vector2I> prepareChunk, int maxVisited,
            bool stopAtPlayer = true)
        {
            var search = new WaterReach(targets, body, volume, collides, isPlayerChunk, prepareChunk, maxVisited, stopAtPlayer);

            search.Process(double.PositiveInfinity);

            return search.Outcome;
        }

        // Anda a busca por ate `milliseconds`; Done quando termina (Outcome com a resposta).
        public void Process(double milliseconds)
        {
            if (Done)
            {
                return;
            }

            var watch = Stopwatch.StartNew();

            while (_queue.TryDequeue(out var cell, out _))
            {
                if (_visited.Count > _maxVisited)
                {
                    Finish(true, true);

                    return;
                }

                if (_isPlayerChunk(CoordinateUtilities.CellToChunk(cell)))
                {
                    _reaches = true;

                    if (_stopAtPlayer)
                    {
                        break;
                    }
                }

                Expand(cell);

                if (watch.Elapsed.TotalMilliseconds >= milliseconds)
                {
                    return;
                }
            }

            Finish(_reaches, false);
        }

        private void Expand(Vector2I cell)
        {
            var below = cell + Vector2I.Down;

            if (!_visited.Contains(below) && Open(below))
            {
                Push(below);
            }

            var supported = _visited.Contains(below) || _body.Contains(below) || _collides(below);

            if (!supported)
            {
                return;
            }

            foreach (var side in new[] { cell + Vector2I.Left, cell + Vector2I.Right })
            {
                if (_visited.Contains(side) || !Open(side))
                {
                    continue;
                }

                if (_budget <= 0)
                {
                    _budgetLimited = true;

                    continue;
                }

                _budget--;
                Push(side);
            }
        }

        private bool Open(Vector2I cell)
        {
            var chunk = CoordinateUtilities.CellToChunk(cell);

            if (_region.Add(chunk))
            {
                _prepareChunk?.Invoke(chunk);
            }

            return !_body.Contains(cell) && !_collides(cell);
        }

        private void Push(Vector2I cell)
        {
            _visited.Add(cell);
            _queue.Enqueue(cell, -cell.Y);
        }

        private void Finish(bool reaches, bool capped)
        {
            Done = true;
            Outcome = new Result(reaches, _region, _visited.Count, capped, !reaches && _budgetLimited);
        }

        // A agua conectada as celulas (4 vizinhos com nivel > 0, nos chunks carregados) e o volume dela, ate maxCells.
        public static (HashSet<Vector2I> Body, long Volume) Body(WaterField water, IEnumerable<Vector2I> sources, int maxCells)
        {
            var body = new HashSet<Vector2I>();
            var stack = new Stack<Vector2I>();
            long volume = 0;

            foreach (var source in sources)
            {
                if (water.Level(source) > 0 && body.Add(source))
                {
                    stack.Push(source);
                }
            }

            while (stack.Count > 0 && body.Count < maxCells)
            {
                var cell = stack.Pop();

                volume += water.Level(cell);

                foreach (var next in new[] { cell + Vector2I.Left, cell + Vector2I.Right, cell + Vector2I.Up, cell + Vector2I.Down })
                {
                    if (water.Level(next) > 0 && body.Add(next))
                    {
                        stack.Push(next);
                    }
                }
            }

            return (body, volume);
        }

        #endregion
    }
}
