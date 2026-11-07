using Godot;
using Jogo25D.Constants;
using Jogo25D.Dimensions;
using Jogo25D.Utils.Coordinates;
using System.Collections.Generic;

namespace Jogo25D.Water
{
    // Exibicao da agua da dimensao: um tile por celula com agua, na altura do nivel (8 alturas) e com a linha de superficie
    // quando nao ha agua em cima. So redesenha as celulas que mudaram (e a de baixo, cuja superficie depende delas).
    // Tileset: atlas 8x2, coluna = altura - 1, linha 0 = com superficie, linha 1 = sem.
    public partial class WaterLayer : TileMapLayer
    {
        #region Dinamic properties

        private const int SourceId = 0;

        private Dimension _dimension;
        private WaterField _field;

        private readonly HashSet<Vector2I> _pending = new();

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            _dimension = GetParent<Dimension>();
            _dimension.ChunkUnloaded += EraseChunk;
        }

        public override void _ExitTree()
        {
            if (_dimension != null)
            {
                _dimension.ChunkUnloaded -= EraseChunk;
            }

            Bind(null);
        }

        public override void _Process(double delta)
        {
            // O campo e recriado ao trocar de mundo ou de papel na rede: redesenha tudo com o novo.
            if (_dimension.Water != _field)
            {
                Bind(_dimension.Water);
            }

            if (_pending.Count == 0)
            {
                return;
            }

            foreach (var cell in _pending)
            {
                Draw(cell);
            }

            _pending.Clear();
        }

        #endregion

        #region Core - Desenho

        private void Bind(WaterField field)
        {
            if (_field != null)
            {
                _field.CellChanged -= OnCellChanged;
            }

            _field = field;
            _pending.Clear();

            Clear();

            if (_field == null)
            {
                return;
            }

            _field.CellChanged += OnCellChanged;

            foreach (var chunk in _field.LoadedChunks)
            {
                for (var index = 0; index < WaterField.ChunkCells; index++)
                {
                    var cell = WaterField.CellOf(chunk, index);

                    if (_field.Level(cell) > 0)
                    {
                        Queue(cell);
                    }
                }
            }
        }

        private void OnCellChanged(Vector2I cell, byte level)
        {
            Queue(cell);
        }

        private void Queue(Vector2I cell)
        {
            _pending.Add(cell);
            _pending.Add(cell + Vector2I.Down);
        }

        private void Draw(Vector2I cell)
        {
            var height = WaterField.Height(_field.Level(cell));

            if (height == 0)
            {
                EraseCell(cell);

                return;
            }

            var surface = WaterField.Height(_field.Level(cell + Vector2I.Up)) == 0;

            SetCell(cell, SourceId, new Vector2I(height - 1, surface ? 0 : 1));
        }

        private void EraseChunk(Vector2I chunk)
        {
            var origin = CoordinateUtilities.ChunkToCell(chunk);
            var size = ChunkStreamingConstants.CHUNK_SIZE;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cell = origin + new Vector2I(x, y);

                    _pending.Remove(cell);
                    EraseCell(cell);
                }
            }
        }

        #endregion
    }
}
