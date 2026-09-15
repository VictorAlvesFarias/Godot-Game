using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;

namespace Jogo25D.Testing
{
    // Redesenha as camadas de tile da Upsidedown: floresta, cavernas, wall de terra e tres casas.
    // A cena e instanciada FORA da arvore, entao nenhum _Ready de dimensao roda: so os tiles mudam.
    //
    // Mapeamento de terreno, conferido renderizando a paleta (.images/paleta-terrenos.png):
    //   0 lime_grass    capa fina de grama, nao preenche
    //   2 lime_bordercap corpo de terra com borda        <- massa solida
    //   4 lime_base     terra lisa                        <- Base e Wall
    //   6 wood          madeira                           <- troncos e casas
    //   7 sheet         folhagem                          <- copas
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
        static readonly (int X, int Width)[] Plateaus = { (-72, 16), (-14, 16), (48, 16) };
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
                var baseTrunks = new HashSet<Vector2I>();
                var baseLeaves = new HashSet<Vector2I>();
                var wallTrunks = new HashSet<Vector2I>();
                var wallLeaves = new HashSet<Vector2I>();

                for (int x = LEFT; x <= RIGHT; x++)
                    for (int y = Ground(x); y <= BOTTOM; y++) earth.Add(new Vector2I(x, y));

                var rng = new Random(20260915);
                var centres = new List<(int X, int Y, int RX, int RY)>();
                for (int i = 0; i < 9; i++)
                {
                    int cx = LEFT + 12 + i * 24 + rng.Next(-5, 6);
                    int cy = Ground(cx) + 7 + rng.Next(0, 11);
                    centres.Add((cx, cy, rng.Next(5, 10), rng.Next(3, 6)));
                }
                void Carve(int cx, int cy, double rx, double ry)
                {
                    for (int x = (int)(cx - rx) - 1; x <= cx + rx + 1; x++)
                        for (int y = (int)(cy - ry) - 1; y <= cy + ry + 1; y++)
                        {
                            double dx = (x - cx) / rx, dy = (y - cy) / ry;
                            if (dx * dx + dy * dy > 1.0) continue;
                            if (y <= Ground(x) + 2) continue;   // a caverna nao abre buraco na superficie
                            var cell = new Vector2I(x, y);
                            if (earth.Contains(cell)) cave.Add(cell);
                        }
                }
                foreach (var c in centres) Carve(c.X, c.Y, c.RX, c.RY);
                for (int i = 0; i + 1 < centres.Count; i++)
                {
                    var a = centres[i]; var b = centres[i + 1];
                    for (int s = 0; s <= 140; s++)
                    {
                        double t = s / 140.0;
                        Carve((int)Math.Round(a.X + (b.X - a.X) * t), (int)Math.Round(a.Y + (b.Y - a.Y) * t), 2.4, 1.7);
                    }
                }
                foreach (var cell in earth) if (!cave.Contains(cell)) body.Add(cell);
                // Capa de grama: so a celula mais alta de cada coluna, que e a superficie exposta.
                for (int x = LEFT; x <= RIGHT; x++)
                {
                    var topo = new Vector2I(x, Ground(x));
                    if (body.Contains(topo)) { body.Remove(topo); grass.Add(topo); }
                }

                void Tree(int x, int top, int height, int canopy, HashSet<Vector2I> trunkSet, HashSet<Vector2I> leafSet)
                {
                    for (int y = top; y > top - height; y--)
                    {
                        trunkSet.Add(new Vector2I(x, y));
                        trunkSet.Add(new Vector2I(x + 1, y));
                    }
                    int cy = top - height;
                    for (int dx = -canopy; dx <= canopy + 1; dx++)
                        for (int dy = -canopy; dy <= canopy - 1; dy++)
                            if (dx * dx * 0.7 + dy * dy <= canopy * canopy)
                                leafSet.Add(new Vector2I(x + dx, cy + dy));
                }
                for (int x = LEFT + 6; x < RIGHT - 6; x += 7 + rng.Next(0, 5))
                {
                    bool onPlateau = false;
                    foreach (var p in Plateaus) if (x > p.X - 4 && x < p.X + p.Width + 4) onPlateau = true;
                    if (onPlateau) continue;
                    int top = Ground(x) - 1, height = 4 + rng.Next(0, 4), canopy = 3 + rng.Next(0, 3);
                    int roll = rng.Next(0, 10);
                    if (roll < 6) Tree(x, top, height, canopy, trunks, leaves);
                    else if (roll < 8) Tree(x, top, height, canopy, baseTrunks, baseLeaves);
                    else Tree(x, top, height, canopy, wallTrunks, wallLeaves);
                }

                // Casas em corte lateral: contorno de madeira. A porta e um vao na parede DIREITA,
                // assentado no chao; a janela e um vao 2x2 na parede direita, acima do chao.
                void House(int x0, int floor, int w, int h, string opening)
                {
                    int right = x0 + w - 1, roof = floor - h;
                    for (int x = x0; x < x0 + w; x++)
                        for (int y = roof; y <= floor; y++)
                        {
                            bool border = x == x0 || x == right || y == roof || y == floor;
                            if (!border) continue;
                            if (opening == "porta" && x == right && y >= floor - 3 && y <= floor - 1) continue;
                            if (opening == "janela" && x == right && y >= roof + 2 && y <= roof + 3) continue;
                            wood.Add(new Vector2I(x, y));
                        }
                }
                House(Plateaus[0].X + 2, Ground(Plateaus[0].X) - 1, 12, 7, "fechada");
                House(Plateaus[1].X + 2, Ground(Plateaus[1].X) - 1, 12, 7, "porta");
                House(Plateaus[2].X + 2, Ground(Plateaus[2].X) - 1, 12, 7, "janela");
                foreach (var cell in wood) { body.Remove(cell); grass.Remove(cell); leaves.Remove(cell); trunks.Remove(cell); }

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

                // Base: terra lisa atras de todo o macico, mais as arvores dessa camada.
                baseLayer.SetCellsTerrainConnect(A(earth), DIRT, 0, false);
                baseLayer.SetCellsTerrainConnect(A(baseTrunks), WOOD, 0, false);
                baseLayer.SetCellsTerrainConnect(A(baseLeaves), SHEET, 0, false);

                // Wall: terra onde ha terra E onde ha caverna, mais as arvores dessa camada.
                var wallCells = new HashSet<Vector2I>(earth);
                foreach (var c in cave) wallCells.Add(c);
                walls.SetCellsTerrainConnect(A(wallCells), DIRT, 0, false);
                walls.SetCellsTerrainConnect(A(wallTrunks), WOOD, 0, false);
                walls.SetCellsTerrainConnect(A(wallLeaves), SHEET, 0, false);
                // Dentro das casas, wall de madeira: sem isso o interior fica sem fundo nenhum.
                var inside = new HashSet<Vector2I>();
                foreach (var p in Plateaus)
                {
                    int floor = Ground(p.X) - 1, x0 = p.X + 2;
                    for (int x = x0 + 1; x < x0 + 11; x++)
                        for (int y = floor - 6; y < floor; y++) inside.Add(new Vector2I(x, y));
                }
                walls.SetCellsTerrainConnect(A(inside), WOOD, 0, false);

                var novo = new PackedScene();
                novo.Pack(root);
                var erro = ResourceSaver.Save(novo, path);
                GD.Print("UPSIDEDOWN: corpo=" + body.Count + " grama=" + grass.Count + " terra=" + earth.Count
                    + " caverna=" + cave.Count + " troncos=" + trunks.Count + " folhas=" + leaves.Count
                    + " madeira=" + wood.Count + " wall=" + wallCells.Count
                    + " arvores_base=" + baseLeaves.Count + " arvores_wall=" + wallLeaves.Count
                    + " salvar=" + erro);
                GetTree().Quit(erro == Error.Ok ? 0 : 1);
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
