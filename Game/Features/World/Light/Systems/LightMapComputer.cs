using Godot;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // A CPU nao calcula iluminacao. Ela so rasteriza a geometria numa textura pequena, uma celula
    // por texel, e ja resolve a profundidade de cada celula solida ate o ar mais proximo. O shader
    // usa isso para saber onde esta a casca do terreno e quanta materia a luz precisa atravessar.
    public sealed class LightMapComputer
    {
        // Chebyshev cabe em um byte e satura bem antes de qualquer caverna real.
        private const int PROFUNDIDADE_MAXIMA = 255;

        private bool[] _solido = System.Array.Empty<bool>();
        private int[] _profundidade = System.Array.Empty<int>();
        private byte[] _pixels = System.Array.Empty<byte>();

        public int Largura { get; private set; }
        public int Altura { get; private set; }

        public void Redimensionar(int largura, int altura)
        {
            largura = Mathf.Max(1, largura);
            altura = Mathf.Max(1, altura);

            if (Largura == largura && Altura == altura)
            {
                return;
            }

            Largura = largura;
            Altura = altura;

            int total = largura * altura;

            _solido = new bool[total];
            _profundidade = new int[total];
            _pixels = new byte[total];
        }

        // Para onde a luz do sol viaja. Rotacao 0 aponta para baixo: sol acima do mundo.
        public static Vector2 DirecaoDaLuz(float rotacaoEmRadianos)
        {
            return Vector2.Down.Rotated(rotacaoEmRadianos);
        }

        public void PreencherGrade(IReadOnlyList<TileMapLayer> camadas, Vector2I origem)
        {
            if (camadas == null || camadas.Count == 0)
            {
                System.Array.Clear(_solido);
                return;
            }

            for (int y = 0; y < Altura; y++)
            {
                for (int x = 0; x < Largura; x++)
                {
                    _solido[y * Largura + x] = DadosDaCelula(camadas, new Vector2I(origem.X + x, origem.Y + y)) != null;
                }
            }
        }

        public Image Calcular()
        {
            if (Largura <= 0 || Altura <= 0)
            {
                return null;
            }

            CalcularProfundidade();

            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = (byte)Mathf.Min(_profundidade[i], PROFUNDIDADE_MAXIMA);
            }

            return Image.CreateFromData(Largura, Altura, false, Image.Format.R8, _pixels);
        }

        // Distancia de Chebyshev ate o ar, em duas varreduras. Ar = 0, casca exposta = 1, miolo > 1.
        // Fora da janela conta como ar, senao a borda vira materia infinitamente funda.
        private void CalcularProfundidade()
        {
            for (int i = 0; i < _profundidade.Length; i++)
            {
                _profundidade[i] = _solido[i] ? PROFUNDIDADE_MAXIMA : 0;
            }

            for (int y = 0; y < Altura; y++)
            {
                for (int x = 0; x < Largura; x++)
                {
                    int i = y * Largura + x;

                    if (_profundidade[i] == 0)
                    {
                        continue;
                    }

                    int vizinho = Vizinho(x - 1, y);
                    vizinho = Mathf.Min(vizinho, Vizinho(x - 1, y - 1));
                    vizinho = Mathf.Min(vizinho, Vizinho(x, y - 1));
                    vizinho = Mathf.Min(vizinho, Vizinho(x + 1, y - 1));

                    _profundidade[i] = Mathf.Min(_profundidade[i], vizinho + 1);
                }
            }

            for (int y = Altura - 1; y >= 0; y--)
            {
                for (int x = Largura - 1; x >= 0; x--)
                {
                    int i = y * Largura + x;

                    if (_profundidade[i] == 0)
                    {
                        continue;
                    }

                    int vizinho = Vizinho(x + 1, y);
                    vizinho = Mathf.Min(vizinho, Vizinho(x + 1, y + 1));
                    vizinho = Mathf.Min(vizinho, Vizinho(x, y + 1));
                    vizinho = Mathf.Min(vizinho, Vizinho(x - 1, y + 1));

                    _profundidade[i] = Mathf.Min(_profundidade[i], vizinho + 1);
                }
            }
        }

        private int Vizinho(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Largura || y >= Altura)
            {
                return 0;
            }

            return _profundidade[y * Largura + x];
        }

        private static TileData DadosDaCelula(IReadOnlyList<TileMapLayer> camadas, Vector2I celula)
        {
            for (int i = 0; i < camadas.Count; i++)
            {
                TileData dados = camadas[i]?.GetCellTileData(celula);

                if (dados != null)
                {
                    return dados;
                }
            }

            return null;
        }
    }
}
