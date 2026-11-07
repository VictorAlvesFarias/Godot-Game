using Godot;
using System;
using System.Collections.Generic;

namespace Jogo25D.Items
{
    // Objetos em baldes por celula, com uma segunda chave opcional (as celulas que apoiam o objeto). Consultas por celula,
    // por raio e pelo mais proximo tocam so os baldes do raio, sem varrer todos os objetos.
    public class CellIndex<T> where T : class
    {
        #region Properties

        private readonly Dictionary<Vector2I, List<T>> _byCell = new();
        private readonly Dictionary<Vector2I, List<T>> _bySupport = new();
        private readonly Dictionary<T, Vector2I> _cellOf = new();
        private readonly Dictionary<T, Vector2I[]> _supportOf = new();

        public int Count => _cellOf.Count;

        #endregion

        #region Core - Escrita

        // Coloca (ou move) o objeto na celula.
        public void Place(T item, Vector2I cell)
        {
            if (_cellOf.TryGetValue(item, out var current))
            {
                if (current == cell)
                {
                    return;
                }

                Take(_byCell, current, item);
            }

            _cellOf[item] = cell;
            Put(_byCell, cell, item);
        }

        public void Remove(T item)
        {
            if (_cellOf.Remove(item, out var cell))
            {
                Take(_byCell, cell, item);
            }

            ClearSupport(item);
        }

        // As celulas que apoiam o objeto (troca as anteriores).
        public void SetSupport(T item, IReadOnlyList<Vector2I> cells)
        {
            ClearSupport(item);

            var copy = new Vector2I[cells.Count];

            for (var i = 0; i < cells.Count; i++)
            {
                copy[i] = cells[i];
                Put(_bySupport, cells[i], item);
            }

            _supportOf[item] = copy;
        }

        public void ClearSupport(T item)
        {
            if (!_supportOf.Remove(item, out var cells))
            {
                return;
            }

            foreach (var cell in cells)
            {
                Take(_bySupport, cell, item);
            }
        }

        #endregion

        #region Core - Consulta

        public bool TryGetCell(T item, out Vector2I cell)
        {
            return _cellOf.TryGetValue(item, out cell);
        }

        // Objetos apoiados na celula (copia: quem chama pode mudar o indice enquanto percorre).
        public List<T> SupportedBy(Vector2I cell)
        {
            return _bySupport.TryGetValue(cell, out var list) ? new List<T>(list) : new List<T>();
        }

        // Objetos nas celulas a ate `radiusCells` celulas (em x e em y) da celula central.
        public List<T> Around(Vector2I center, int radiusCells)
        {
            var result = new List<T>();

            for (var dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (var dy = -radiusCells; dy <= radiusCells; dy++)
                {
                    if (_byCell.TryGetValue(center + new Vector2I(dx, dy), out var list))
                    {
                        result.AddRange(list);
                    }
                }
            }

            return result;
        }

        // O mais proximo de `position` entre os aceitos por `accept`, a ate `radius` (distancia medida por `positionOf`).
        public T Nearest(Vector2 position, int tileSize, float radius, Func<T, Vector2> positionOf, Func<T, bool> accept = null)
        {
            var center = new Vector2I(Mathf.FloorToInt(position.X / tileSize), Mathf.FloorToInt(position.Y / tileSize));
            var cells = Mathf.CeilToInt(radius / tileSize);
            T best = null;
            var bestDistance = radius;

            foreach (var item in Around(center, cells))
            {
                var distance = positionOf(item).DistanceTo(position);

                if (distance <= bestDistance && (accept == null || accept(item)))
                {
                    best = item;
                    bestDistance = distance;
                }
            }

            return best;
        }

        #endregion

        #region Helpers

        private static void Put(Dictionary<Vector2I, List<T>> map, Vector2I cell, T item)
        {
            if (!map.TryGetValue(cell, out var list))
            {
                map[cell] = list = new List<T>();
            }

            list.Add(item);
        }

        private static void Take(Dictionary<Vector2I, List<T>> map, Vector2I cell, T item)
        {
            if (!map.TryGetValue(cell, out var list))
            {
                return;
            }

            list.Remove(item);

            if (list.Count == 0)
            {
                map.Remove(cell);
            }
        }

        #endregion
    }
}
