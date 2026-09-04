using Godot;
using Jogo25D.Core;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Roteador entre o mundo e os nos de mapa de luz das dimensoes. Quem calcula e desenha e o
    // LightMap2D autorado em cada cena de dimensao; este manager existe porque o TerrainLayer
    // so conhece o id da dimensao, e alguem precisa traduzir isso no no certo.
    //
    // Ele ja teve uma copia do mapa de luz aqui dentro, criando o overlay por conta propria. Com
    // o no na cena eram dois desenhando o mesmo terreno, e junto o mundo escurecia em dobro.
    public partial class LightMapManager : Node
    {
        #region Dinamic properties

        /// <summary>Os nos de mapa de luz achados, por id de dimensao.</summary>
        public Dictionary<string, LightMap2D> Nodes { get; } = new();

        #endregion

        #region Core

        /// <summary>
        /// Acha o LightMap2D de cada dimensao e registra. Chamado quando o mundo nasce;
        /// idempotente, rodar de novo so reescreve o registro. A dimensao sem o no simplesmente
        /// nao tem mapa de luz - a cena e que manda.
        /// </summary>
        public void AttachToDimensions()
        {
            var dimensoes = Game.Managers.DimensionManager.Node;

            if (dimensoes == null)
            {
                return;
            }

            Nodes.Clear();

            foreach (var id in dimensoes.Ids)
            {
                var parent = dimensoes.ResolveParent(id);

                if (parent == null)
                {
                    continue;
                }

                var no = ResolverNo(parent);

                if (no != null)
                {
                    Nodes[id] = no;
                }
            }
        }

        /// <summary>Esquece os nos registrados. Chamado quando o mundo e destruido.</summary>
        public void Detach()
        {
            Nodes.Clear();
        }

        /// <summary>
        /// Avisa o no da dimensao que um bloco nasceu ou sumiu. Nao recalcula na hora: o no marca
        /// a janela como suja e o proximo quadro a refaz inteira.
        /// </summary>
        public void Invalidate(string dimensionId)
        {
            if (dimensionId == null)
            {
                return;
            }

            if (Nodes.TryGetValue(dimensionId, out var no) && no != null && IsInstanceValid(no))
            {
                no.Invalidate();
            }
        }

        // A dimensao sem o no simplesmente nao tem mapa de luz; a cena e que manda.
        private static LightMap2D ResolverNo(Node2D parent)
        {
            foreach (var filho in parent.GetChildren())
            {
                if (filho is LightMap2D no)
                {
                    return no;
                }
            }

            return null;
        }

        #endregion
    }
}
