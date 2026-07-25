using Godot;
using Jogo25D.Characters;

namespace Jogo25D.TileEntities
{
    // Objeto de logica associado a uma celula marcada no TileMap (ver
    // TileEntityManager). Classe simples, sem herdar Node/Resource - vive
    // direto no dicionario do manager, sem o custo de um Node por instancia
    // (o ponto todo da arquitetura Tile/TileEntity: o TileMap guarda so
    // "o que existe", isto aqui guarda "como funciona").
    public abstract class TileEntity
    {
        public string TypeId { get; }
        public Vector2I Cell { get; }
        public Node2D World { get; }

        // Centro da celula, no espaco local de World - referencia pronta
        // pra qualquer TileEntity que precise instanciar um visual (Sprite,
        // etc) alem do que o proprio tile ja desenha no TileMap.
        public Vector2 CellPosition { get; }

        protected TileEntity(string typeId, Vector2I cell, Node2D world, Vector2 cellPosition)
        {
            TypeId = typeId;
            Cell = cell;
            World = world;
            CellPosition = cellPosition;
        }

        // Texto do prompt de interacao (ex: "Pressione [E] para viajar") -
        // null/vazio significa "essa entidade nao mostra prompt nenhum".
        // Usado pelo HUD pra saber o que exibir quando o player local esta
        // em cima da celula.
        public virtual string InteractPrompt => null;

        public virtual void OnReady() { }
        public virtual void OnPlayerEnter(Player player) { }
        public virtual void OnPlayerExit(Player player) { }

        // Disparado pelo TileEntityManager quando o player aperta a tecla
        // de interagir (E) estando na celula desta entidade - separado de
        // OnPlayerEnter de proposito, pra distinguir "presenca" (util pra
        // um prompt tipo "Aperte E" no futuro) de "acao confirmada".
        public virtual void OnPlayerInteract(Player player) { }
    }
}
