using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;

namespace Jogo25D.Testing
{
    // Redesenha as camadas de tile da Upsidedown seguindo o padrao que a cena ja usava:
    //   arvore  = tronco de 1 tile de largura + copa larga, achatada e de borda irregular
    //   torre   = madeira, parede de 1 tile, OCA, topo em degraus, vao de porta ou janela
    //   caverna = galerias ligadas entre si E abertas para a superficie por um barranco
    //
    // Mapeamento conferido renderizando a paleta (.images/paleta-terrenos.png):
    //   0 lime_grass  capa fina de grama   2 lime_bordercap corpo de terra
    //   4 lime_base   terra lisa           6 wood           madeira   7 sheet folhagem
    public partial class UpsidedownBuilder : Node
    {
        const int GRASS = 0, BODY = 2, DIRT = 4, WOOD = 6, SHEET = 7;
        const int LEFT = -110, RIGHT = 110, BOTTOM = 34;

        static int Surface(int x)
        {
            double h = -20
                + 3.0 * Math.Sin(x * 0.055)
                + 1.8 * Math.Sin(x * 0.17 + 1.3)
                + 0.9 * Math.Sin(x * 0.41 + 0.4);
            return (int)Math.Round(h);
        }
        static readonly (int X, int Width)[] Plateaus = { (-72, 14), (-16, 14), (46, 14) };
        static int Ground(int x)
        {
            foreach (var p in Plateaus)
                if (x >= p.X - 1 && x < p.X + p.Width + 1) return Surface(p.X + p.Width / 2);
            return Surface(x);
        }

        public override void _Ready()
        {
            try
            {
                const string path = "res://Scenes/World/Levels/Upsidedown.tscn";
                var packed = GD.Load<PackedScene>(path);
                var root = packed.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
                var compose = root.GetNode<TileMapLayer>("Compose");
                var baseLayer = root.GetNode<TileMapLayer>("Base");
                var walls = root.GetNode<TileMapLayer>("BackgroundWalls");
                compose.Clear(); baseLayer.Clear(); walls.Clear();

                var earth = new HashSet<Vector2I>();
                var cave = new HashSet<Vector2I>();
                var body = new HashSet<Vector2I>();
                var grass = new HashSet<Vector2I>();
                var trunks = new HashSet<Vector2I>();
                var leaves = new HashSet<Vector2I>();
                var wood = new HashSet<Vector2I>();
                var woodInside = new HashSet<Vector2I>();
                var baseTrunks = new HashSet<Vector2I>();
                var baseLeaves = new HashSet<Vector2I>();
                var wallTrunks = new HashSet<Vector2I>();
                var wallLeaves = new HashSet<Vector2I>();

                for (int x = LEFT; x <= RIGHT; x++)
                    for (int y = Ground(x); y <= BOTTOM; y++) earth.Add(new Vector2I(x, y));

                var rng = new Random(20260915);
                void Carve(int cx, int cy, double rx, double ry, bool allowSurface)
                {
                    for (int x = (int)(cx - rx) - 1; x <= cx + rx + 1; x++)
                        for (int y = (int)(cy - ry) - 1; y <= cy + ry + 1; y++)
                        {
                            double dx = (x - cx) / rx, dy = (y - cy) / ry;
                            if (dx * dx + dy * dy > 1.0) continue;
                            if (!allowSurface && y <= Ground(x) + 2) continue;
                            var cell = new Vector2I(x, y);
                            if (earth.Contains(cell)) cave.Add(cell);
                        }
                }
                var centres = new List<(int X, int Y, int RX, int RY)>();
                for (int i = 0; i < 9; i++)
                {
                    int cx = LEFT + 12 + i * 24 + rng.Next(-5, 6);
                    int cy = Ground(cx) + 7 + rng.Next(0, 11);
                    centres.Add((cx, cy, rng.Next(5, 10), rng.Next(3, 6)));
                }
                foreach (var c in centres) Carve(c.X, c.Y, c.RX, c.RY, false);
                for (int i = 0; i + 1 < centres.Count; i++)
                {
                    var a = centres[i]; var b = centres[i + 1];
                    for (int s = 0; s <= 140; s++)
                    {
                        double t = s / 140.0;
                        Carve((int)Math.Round(a.X + (b.X - a.X) * t), (int)Math.Round(a.Y + (b.Y - a.Y) * t), 2.4, 1.7, false);
                    }
                }
                // Entradas: barrancos que descem da superficie ate a galeria, alargando para cima
                // como na cena antiga. Sem isto a caverna fica fechada e nao da para entrar nela.
                var entradas = new List<int> { 1, 4, 7 };
                foreach (int indice in entradas)
                {
                    var alvo = centres[indice];
                    int bocaX = alvo.X + rng.Next(-3, 4);
                    int topo = Ground(bocaX);
                    for (int y = topo - 1; y <= alvo.Y; y++)
                    {
                        double t = (y - (topo - 1)) / (double)Math.Max(1, alvo.Y - (topo - 1));
                        double largura = 3.4 - 1.4 * t;                 // mais aberta na boca
                        int desvio = (int)Math.Round(2.5 * Math.Sin((y - topo) * 0.22));
                        Carve(bocaX + desvio, y, largura, 1.2, true);
                    }
                }
                foreach (var cell in earth) if (!cave.Contains(cell)) body.Add(cell);
                // Capa de grama em toda superficie exposta ao ceu, inclusive nas bordas do barranco.
                foreach (var cell in body)
                    if (!body.Contains(new Vector2I(cell.X, cell.Y - 1)) && !cave.Contains(new Vector2I(cell.X, cell.Y - 1)))
                        grass.Add(cell);
                foreach (var cell in grass) body.Remove(cell);

                // Arvore no padrao da cena: tronco de 1 tile, copa larga e achatada, borda irregular.
                void Tree(int x, int groundY, HashSet<Vector2I> trunkSet, HashSet<Vector2I> leafSet)
                {
                    int height = 3 + rng.Next(0, 4);
                    int top = groundY - 1;
                    for (int y = top; y > top - height; y--) trunkSet.Add(new Vector2I(x, y));
                    int cy = top - height;
                    int half = 2 + rng.Next(0, 3);          // copa de 5 a 9 de largura
                    int tall = 2 + rng.Next(0, 2);          // e de 3 a 4 de altura
                    for (int dx = -half; dx <= half; dx++)
                    {
                        int recuo = Math.Abs(dx) >= half ? 1 + rng.Next(0, 2) : (Math.Abs(dx) == half - 1 ? rng.Next(0, 2) : 0);
                        for (int dy = -tall + recuo; dy <= 1 - recuo; dy++)
                            leafSet.Add(new Vector2I(x + dx, cy + dy));
                    }
                }
                for (int x = LEFT + 6; x < RIGHT - 6; x += 5 + rng.Next(0, 4))
                {
                    bool ocupado = false;
                    foreach (var p in Plateaus) if (x > p.X - 4 && x < p.X + p.Width + 4) ocupado = true;
                    int chao = Ground(x);
                    if (cave.Contains(new Vector2I(x, chao)) || cave.Contains(new Vector2I(x, chao + 1))) ocupado = true;
                    if (ocupado) continue;
                    int roll = rng.Next(0, 10);
                    if (roll < 6) Tree(x, chao, trunks, leaves);
                    else if (roll < 8) Tree(x, chao, baseTrunks, baseLeaves);
                    else Tree(x, chao, wallTrunks, wallLeaves);
                }

                // Torre de madeira: parede de 1 tile, oca, topo em degraus, como as da cena antiga.
                void Tower(int x0, int floor, int w, int h, string opening)
                {
                    int right = x0 + w - 1, roof = floor - h;
                    for (int y = roof + 3; y <= floor; y++)          // corpo reto
                        for (int x = x0; x <= right; x++)
                        {
                            bool border = x == x0 || x == right || y == floor;
                            if (border) wood.Add(new Vector2I(x, y));
                            else woodInside.Add(new Vector2I(x, y));
                        }
                    for (int passo = 0; passo < 3; passo++)          // topo recuando um tile por linha
                    {
                        int y = roof + passo, recuo = 2 - passo;
                        for (int x = x0 + recuo; x <= right - recuo; x++) wood.Add(new Vector2I(x, y));
                    }
                    // O vao e vazado nas DUAS camadas: com wall atras ele nao aparece, porque o
                    // fundo e da mesma madeira da parede. Na cena antiga a janela e um buraco preto.
                    if (opening == "porta")
                        for (int y = floor - 3; y <= floor - 1; y++)
                        { var c = new Vector2I(right, y); wood.Remove(c); woodInside.Remove(c); }
                    if (opening == "janela")
                        for (int y = roof + 4; y <= roof + 5; y++)
                        { var c = new Vector2I(x0, y); wood.Remove(c); woodInside.Remove(c); }
                }
                Tower(Plateaus[0].X + 4, Ground(Plateaus[0].X) - 1, 6, 9, "fechada");
                Tower(Plateaus[1].X + 4, Ground(Plateaus[1].X) - 1, 6, 10, "porta");
                Tower(Plateaus[2].X + 4, Ground(Plateaus[2].X) - 1, 6, 11, "janela");
                foreach (var cell in wood) { body.Remove(cell); grass.Remove(cell); leaves.Remove(cell); trunks.Remove(cell); }
                foreach (var cell in woodInside) { body.Remove(cell); grass.Remove(cell); leaves.Remove(cell); trunks.Remove(cell); }

                static Array<Vector2I> A(IEnumerable<Vector2I> cells)
                {
                    var a = new Array<Vector2I>();
                    foreach (var c in cells) a.Add(c);
                    return a;
                }
                compose.SetCellsTerrainConnect(A(body), BODY, 0, false);
                compose.SetCellsTerrainConnect(A(grass), GRASS, 0, false);
                compose.SetCellsTerrainConnect(A(trunks), WOOD, 0, false);
                compose.SetCellsTerrainConnect(A(leaves), SHEET, 0, false);
                compose.SetCellsTerrainConnect(A(wood), WOOD, 0, false);

                baseLayer.SetCellsTerrainConnect(A(earth), DIRT, 0, false);
                baseLayer.SetCellsTerrainConnect(A(baseTrunks), WOOD, 0, false);
                baseLayer.SetCellsTerrainConnect(A(baseLeaves), SHEET, 0, false);

                var wallCells = new HashSet<Vector2I>(earth);
                foreach (var c in cave) wallCells.Add(c);
                walls.SetCellsTerrainConnect(A(wallCells), DIRT, 0, false);
                walls.SetCellsTerrainConnect(A(wallTrunks), WOOD, 0, false);
                walls.SetCellsTerrainConnect(A(wallLeaves), SHEET, 0, false);
                walls.SetCellsTerrainConnect(A(woodInside), WOOD, 0, false);

                var novo = new PackedScene();
                novo.Pack(root);
                var erro = ResourceSaver.Save(novo, path);
                GD.Print("UPSIDEDOWN: corpo=" + body.Count + " grama=" + grass.Count + " caverna=" + cave.Count
                    + " entradas=" + entradas.Count + " troncos=" + trunks.Count + " folhas=" + leaves.Count
                    + " madeira=" + wood.Count + " interior=" + woodInside.Count + " wall=" + wallCells.Count
                    + " arvores_base=" + baseLeaves.Count + " arvores_wall=" + wallLeaves.Count
                    + " salvar=" + erro);
                GetTree().Quit(erro == Error.Ok ? 0 : 1);
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
