using Godot;
using System.Collections.Generic;

namespace Jogo25D.Dimensions
{
    // Edicoes do jogador num chunk: estado final de cada celula por camada (id do BlockDB, ou nulo = removido).
    // Dirty: mudou desde a ultima gravacao no banco.
    public sealed class ChunkEdits
    {
        #region Dinamic properties

        public Dictionary<Vector2I, string> Terrain { get; } = new();
        public Dictionary<Vector2I, string> Walls { get; } = new();

        public bool Dirty { get; set; }

        #endregion
    }
}
