using Godot;
using System.Collections.Generic;

namespace Jogo25D.Structures
{
    // Um grupo de celulas de uma mesma instancia que devem ser pintadas com o mesmo terrain_set
    // (ex: tronco e copa de uma arvore sao 2 grupos separados).
    public readonly struct StructureCellGroup
    {
        public readonly int TerrainSet;
        public readonly List<Vector2I> Cells;

        public StructureCellGroup(int terrainSet, List<Vector2I> cells)
        {
            TerrainSet = terrainSet;
            Cells = cells;
        }
    }

    // Caixa delimitadora REAL de uma instancia (o bloco mais a esquerda, mais a direita e mais
    // ao topo entre TODAS as celulas geradas - tronco, copa, tufo, galho, tudo), relativa ao
    // groundCell (X=0/Y=0). Left/Right sao distancias positivas (quantas celulas pra cada lado),
    // nao um raio simetrico - um galho puxando mais pra um lado faz Left != Right. Usada pelo
    // ChunkGenerator pra validar se cabe uma instancia ali E garantir folga minima entre
    // instancias vizinhas da mesma estrutura.
    public readonly struct StructureBounds
    {
        public readonly int Left;
        public readonly int Right;
        public readonly int Top;

        public StructureBounds(int left, int right, int top)
        {
            Left = left;
            Right = right;
            Top = top;
        }
    }

    // Base pra decoracao que nasce SOZINHA espalhada pelo bioma, pintada direto nas celulas do
    // tilemap (nao e um node/cena - pra isso, ver PropDefinition em Jogo25D.Props). Uma estrutura
    // concreta (ex: TreeStructureDefinition) so decide a PROPRIA chance de spawn e o PROPRIO
    // desenho (CollectCells) - quem decide COMO plantar no chunk (limite de borda, espacamento
    // minimo entre instancias, escala pelo tile_size) e o ChunkGenerator.PlaceStructures,
    // igual pra qualquer estrutura registrada, sem repetir essa logica em cada uma.
    //
    // Pra aparecer no mundo, uma estrutura precisa estar registrada no StructureDB E listada em
    // BiomeDefinition.StructureIds do(s) bioma(s) onde deve nascer.
    public abstract class StructureDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }

        // Chance (0..1) de uma instancia nascer em cada coluna elegivel do bioma que a lista -
        // rolada pelo ChunkGenerator (salt reservado 0), nao pela propria estrutura.
        public float Chance { get; init; }

        #endregion

        #region Core - Abstract

        // terrain_sets que essa estrutura pode desenhar - usado pelo ChunkGenerator pra excluir
        // essas celulas da deteccao de "vizinho estrangeiro do bioma" (ReconnectForeignBorder):
        // sem isso, uma instancia encostada na borda de um chunk seria detectada como tal pelo
        // chunk vizinho ao carregar e apagada, porque decoracao nao espelha nas 3 camadas do
        // jeito que bioma espelha.
        public abstract IReadOnlyCollection<int> TerrainSets { get; }

        // Caixa delimitadora REAL (esquerda/direita/topo mais extremos entre TODAS as celulas
        // que essa instancia vai gerar - ver StructureBounds) - usada pelo ChunkGenerator pra
        // validar espacamento minimo entre instancias da MESMA estrutura. Precisa ser
        // consistente com o que CollectCells realmente desenha pra essa mesma coluna (mesmo
        // worldSeed/dimensionId/worldX/worldScale) - senao a validacao nao tem serventia.
        public abstract StructureBounds GetBounds(long worldSeed, string dimensionId, int worldX, int worldScale);

        // Gera as celulas (agrupadas por terrain_set) de uma instancia ancorada em groundCell -
        // so chamado depois que o ChunkGenerator ja confirmou que ela cabe (bounds + espacamento).
        public abstract List<StructureCellGroup> CollectCells(Vector2I groundCell, long worldSeed, string dimensionId, int worldScale);

        #endregion
    }
}
