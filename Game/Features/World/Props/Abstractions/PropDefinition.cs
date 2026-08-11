using Godot;

namespace Jogo25D.Props
{
    // Base pra qualquer "prop" que existe como node/cena propria no mundo (nao pintado no
    // tilemap) - Portal e futuros props do tipo. So cobre REGISTRO/spawn (id -> cena, instanciar
    // node) - ONDE e QUANDO colocar continua sendo decisao de quem chama (hoje: acao do
    // jogador, via WorldManager). Pra decoracao que nasce sozinha espalhada pelo bioma (arvore),
    // ver StructureDefinition em vez disso - aquela pinta celulas de tile, essa aqui instancia
    // um node de verdade.
    public class PropDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }
        public string ScenePath { get; init; }

        #endregion

        #region Core - Spawn

        // Carrega a cena so na primeira vez que essa prop e usada (nao no boot/registro do DB) e
        // guarda em cache - evita depender de uma ordem de inicializacao especifica so pra
        // carregar recurso.
        private PackedScene _scene;

        public virtual Node2D Spawn(Node2D parent, Vector2 position)
        {
            if (parent == null)
            {
                return null;
            }

            _scene ??= GD.Load<PackedScene>(ScenePath);

            if (_scene == null)
            {
                return null;
            }

            var instance = _scene.Instantiate<Node2D>();

            instance.Position = position;

            parent.AddChild(instance);

            return instance;
        }

        #endregion
    }
}
