using Godot;

namespace Jogo25D.Biomes
{
    // Centraliza (1) as regras de quais biomas se conectam entre si pra fins de autotile -
    // editavel no Inspector do Godot (Connections), em vez de precisar marcar uma Custom Data
    // Layer em cada tile do TileSet manualmente - e (2) as proprias TileMapLayer (Texture/
    // BorderCap/Base) como FILHAS DE VERDADE desse node na arvore de cena (nao so uma
    // referencia guardada em C#). Uma instancia existe por dimensao (Overworld, Upsidedown),
    // criada como filha do respectivo dimensionParent - por isso e um Node2D (as TileMapLayer
    // filhas precisam de um ancestral Node2D pra posicionar certo no mundo). Mesmo padrao usado
    // no Tilesetter4Free: cada terreno guarda uma lista de outros terrenos com quem se funde
    // sem borda crua.
    public partial class BiomeConnectionGraph : Node2D
    {
        #region Regras de conexao

        [Export] public Godot.Collections.Array<BiomeConnectionRule> Connections { get; set; } = new();

        public override void _Ready()
        {
            // Comportamento padrao (caso ninguem configure nada no Inspector): Lime e Olive
            // conectam entre si, igual ja estava acontecendo antes dessa centralizacao.
            if (Connections.Count == 0)
            {
                Connections.Add(new BiomeConnectionRule
                {
                    LayerA = BiomeType.LimeGround,
                    LayerB = BiomeType.OliveGround,
                });
            }
        }

        public bool AreConnected(BiomeType a, BiomeType b)
        {
            if (a == b)
            {
                return true;
            }

            foreach (var rule in Connections)
            {
                if (rule == null)
                {
                    continue;
                }

                if ((rule.LayerA == a && rule.LayerB == b) || (rule.LayerA == b && rule.LayerB == a))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Camadas (TileMapLayer) - filhas de verdade desse node

        public TileMapLayer TextureLayer { get; set; }
        public TileMapLayer BorderCapLayer { get; set; }
        public TileMapLayer BaseLayer { get; set; }

        #endregion
    }
}
