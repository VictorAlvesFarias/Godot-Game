using Godot;
using Jogo25D.Save.Types;
using System.Collections.Generic;

namespace Jogo25D.Save.Structures
{
    // Estado final de uma celula numa camada. BlockId nulo = removido (quebrado).
    public readonly record struct CellEdit(int X, int Y, TerrainLayerType Layer, string BlockId);

    // Entidade persistida: State e o dicionario do SaveSerializer em JSON.
    public readonly record struct EntityRow(string EntityId, string Type, float X, float Y, string State);

    // Tudo o que o banco guarda de um chunk. Gravar um snapshot substitui o que havia do chunk.
    public sealed class ChunkSnapshot
    {
        #region Constructors

        public ChunkSnapshot(string dimension, Vector2I chunk)
        {
            Dimension = dimension;
            Chunk = chunk;
        }

        #endregion

        #region Dinamic properties

        public string Dimension { get; }
        public Vector2I Chunk { get; }

        public List<CellEdit> Edits { get; } = new();

        // Entidades do chunk a gravar (por chave); as que nao estao aqui ficam no banco como estao.
        public List<EntityRow> Entities { get; } = new();

        // Falso quando o snapshot nao traz as edicoes: as edicoes do chunk no banco ficam como estao.
        public bool IncludesEdits { get; set; } = true;

        // Niveis de agua do chunk (byte[1024]); nulo = a agua do chunk no banco fica como esta.
        public byte[] Water { get; set; }

        #endregion
    }
}
