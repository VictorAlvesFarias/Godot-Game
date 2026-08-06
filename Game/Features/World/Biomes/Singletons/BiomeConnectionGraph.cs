using Godot;
using Jogo25D.Constants;

namespace Jogo25D.Biomes
{
    // Centraliza (1) as regras de quais biomas se conectam entre si pra fins de autotile -
    // editavel no Inspector do Godot (Connections), em vez de precisar marcar uma Custom Data
    // Layer em cada tile do TileSet manualmente - e (2) as proprias TileMapLayer (Base/
    // BorderCap/Texture) como FILHAS DE VERDADE desse node na arvore de cena. Uma instancia
    // existe por dimensao (Overworld, Upsidedown), ja pre-autorada na cena respectiva
    // (Overworld.tscn/Upsidedown.tscn) junto com as 3 TileMapLayer - assim da pra ver/editar
    // tudo no Inspector sem precisar rodar o jogo. Quando o mundo procedural e gerado, o
    // ChunkStreamingManager REUTILIZA essas mesmas 3 camadas (nunca cria camadas novas por
    // cima) - ele so limpa o conteudo delas e repinta, entao qualquer tile de teste deixado no
    // editor sempre e substituido pela geracao real. E um Node2D pra que as TileMapLayer filhas
    // tenham um ancestral capaz de posicionar certo no mundo. Mesmo padrao usado no
    // Tilesetter4Free: cada terreno guarda uma lista de outros terrenos com quem se funde sem
    // borda crua.
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

            BaseLayer = GetNodeOrNull<TileMapLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME);
            BorderCapLayer = GetNodeOrNull<TileMapLayer>(ChunkStreamingConstants.PROCEDURAL_BORDER_CAP_LAYER_NAME);
            TextureLayer = GetNodeOrNull<TileMapLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);
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

        // So usado como rede de seguranca quando esse node e criado dinamicamente por codigo (nao
        // deveria acontecer no fluxo normal - as 3 camadas ja vem pre-autoradas na cena junto com
        // esse node). Nao sobrescreve nenhuma camada que ja exista.
        public void EnsureLayers(TileSet tileSet)
        {
            BaseLayer ??= CreateLayer(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME, tileSet);
            BorderCapLayer ??= CreateLayer(ChunkStreamingConstants.PROCEDURAL_BORDER_CAP_LAYER_NAME, tileSet);
            TextureLayer ??= CreateLayer(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME, tileSet);
        }

        private TileMapLayer CreateLayer(string name, TileSet tileSet)
        {
            var layer = new TileMapLayer
            {
                Name = name,
                TileSet = tileSet,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };

            AddChild(layer);

            return layer;
        }

        // Apaga o conteudo das 3 camadas SEM remove-las da arvore - usado ao criar um mundo
        // procedural novo, pra garantir que nenhum tile deixado de teste no editor sobreviva a
        // geracao (o ChunkStreamingManager reutiliza essas mesmas camadas em seguida).
        public void ClearLayers()
        {
            BaseLayer?.Clear();
            BorderCapLayer?.Clear();
            TextureLayer?.Clear();
        }

        #endregion
    }
}
