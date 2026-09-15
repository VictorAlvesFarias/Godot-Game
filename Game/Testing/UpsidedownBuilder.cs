using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;

namespace Jogo25D.Testing
{
    // Redesenha as camadas de tile da Upsidedown.
    //
    // Camadas, como pedido:
    //   BackgroundWalls = terra de fundo, onde ha terra E onde ha caverna, mais arvores de fundo
    //   Base            = corpo de terra, em TODA celula solida
    //   Compose         = grama SOBRE as mesmas celulas do Base, mais troncos, copas e torres
    //
    // Base e Compose se sobrepoem de proposito: na cena anterior 4006 celulas estavam nas duas, que
    // e exatamente a contagem de grama. Medido com UpsidedownInspect, nao suposto.
    //
    // As formas de copa, tronco e torre NAO sao inventadas: foram extraidas celula por celula da
    // cena anterior com UpsidedownInspect e estao copiadas abaixo como mascaras.
    public partial class UpsidedownBuilder : Node
    {
        // DIRT (lime_base) esta fora de uso: a terra usa bordercap em todo lugar, inclusive no fundo.
        const int GRASS = 0, BODY = 2, WOOD = 6, SHEET = 7;
        const int LEFT = -110, RIGHT = 110, BOTTOM = 34;

        // ---- modelos extraidos celula por celula da cena antiga (UpsidedownInspect) ----
        // S = folha, W = madeira. A ultima linha e a base do tronco; Ancora e a coluna do tronco.
        static readonly string[] ArvoreGrande = {
            "......SSS......", "....SSSSSSS....", "..SSSSSSSSSSS..", ".SSSSSSSSSSSSS.",
            ".SSSSSSSSSSSSS.", "SSSSSSSSSSSSSSS", "SSSSSSSSSSSSSSS", "SSSSSSSSSSSSSSS",
            "SSSSSSSSSSSSSSS", "SSSSSSSSSSSSSSS", "SSSSSSSSSSSSSSS", ".SSSSSSSSSSSSS.",
            ".SSSSSSSSSSSSS.", "..SSSSSSSSSSS..", "....SSSSSSS....", ".......W.......",
            ".......W.......", ".......W.......", ".......W.......", ".......W.......",
            ".......W.......", ".......W......." };
        const int AncoraGrande = 7;
        static readonly string[] ArvoreMedia = {
            "....SS...", ".SSSSSSS.", "SSSSSSSSS", "..SSSSSSS", ".SSSSSSSS",
            "SSSSSSSS.", "SSSSW....", "SSSWW....", "..SSW....", "....W....",
            "....W....", "....W....", "....W....", "....W....", "....W...." };
        const int AncoraMedia = 4;
        static readonly string[] ArvorePequena = {
            "..SS.", ".SSS.", "SSSSS", "SSSSS", ".SSSS",
            "..SW.", "...W.", "...WW", "....W", "....W" };
        const int AncoraPequena = 4;
        // Topo em bico da torre; abaixo dele repete "#.....#" ate o chao.
        static readonly string[] TopoTorre = { "...#...", "..###..", ".##.##.", "##...##" };

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
                var interior = new HashSet<Vector2I>();   // fundo de madeira dentro das torres
                var wallTrunks = new HashSet<Vector2I>();
                var wallLeaves = new HashSet<Vector2I>();
                var baseTrunks = new HashSet<Vector2I>();
                var baseLeaves = new HashSet<Vector2I>();

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
                // Barrancos abrindo a caverna para a superficie.
                var entradas = new List<int> { 1, 4, 7 };
                foreach (int indice in entradas)
                {
                    var alvo = centres[indice];
                    int bocaX = alvo.X + rng.Next(-3, 4);
                    int topo = Ground(bocaX);
                    for (int y = topo - 1; y <= alvo.Y; y++)
                    {
                        double t = (y - (topo - 1)) / (double)Math.Max(1, alvo.Y - (topo - 1));
                        int desvio = (int)Math.Round(2.5 * Math.Sin((y - topo) * 0.22));
                        Carve(bocaX + desvio, y, 3.4 - 1.4 * t, 1.2, true);
                    }
                }
                foreach (var cell in earth) if (!cave.Contains(cell)) body.Add(cell);
                // A grama cobre as MESMAS celulas do corpo: quem decide onde ela aparece de fato e
                // o autotile, que so desenha a faixa verde nas faces expostas.
                foreach (var cell in body) grass.Add(cell);

                static bool Encosta(Vector2I c, HashSet<Vector2I> ocupado)
                {
                    for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                        if (ocupado.Contains(new Vector2I(c.X + dx, c.Y + dy))) return true;
                    return false;
                }
                void Carimbo(string[] forma, int x0, int y0, HashSet<Vector2I> alvo)
                {
                    for (int j = 0; j < forma.Length; j++)
                        for (int i = 0; i < forma[j].Length; i++)
                            if (forma[j][i] == '#') alvo.Add(new Vector2I(x0 + i, y0 + j));
                }
                // Carimba um dos modelos da cena antiga, opcionalmente espelhado. Nada e inventado:
                // a forma vem inteira do modelo, tronco e copa juntos.
                bool Tree(int x, int chao, HashSet<Vector2I> trunkSet, HashSet<Vector2I> leafSet,
                          HashSet<Vector2I> ocupado)
                {
                    int sorteio = rng.Next(0, 10);
                    var modelo = sorteio < 2 ? ArvoreGrande : sorteio < 6 ? ArvoreMedia : ArvorePequena;
                    int ancora = sorteio < 2 ? AncoraGrande : sorteio < 6 ? AncoraMedia : AncoraPequena;
                    bool espelha = rng.Next(0, 2) == 0;
                    int largura = modelo[0].Length, altura = modelo.Length;
                    if (espelha) ancora = largura - 1 - ancora;
                    var tronco = new HashSet<Vector2I>();
                    var folha = new HashSet<Vector2I>();
                    for (int j = 0; j < altura; j++)
                        for (int i = 0; i < largura; i++)
                        {
                            char marca = modelo[j][espelha ? largura - 1 - i : i];
                            if (marca == '.') continue;
                            var c = new Vector2I(x - ancora + i, chao - 1 - (altura - 1) + j);
                            if (marca == 'W') tronco.Add(c); else folha.Add(c);
                        }
                    // Encostar conta como colidir, entao sobra sempre um tile de folga.
                    foreach (var c in tronco) if (Encosta(c, ocupado)) return false;
                    foreach (var c in folha) if (Encosta(c, ocupado)) return false;
                    foreach (var c in tronco) { trunkSet.Add(c); ocupado.Add(c); }
                    foreach (var c in folha) { leafSet.Add(c); ocupado.Add(c); }
                    return true;
                }

                // Uma reserva por camada: a arvore so entra se couber sem encostar em nenhuma outra.
                // Tenta em muitas colunas e deixa a recusa decidir a densidade final.
                var tomadoCompose = new HashSet<Vector2I>();
                var tomadoWall = new HashSet<Vector2I>();
                var tomadoBase = new HashSet<Vector2I>();
                foreach (var cell in wood) { tomadoCompose.Add(cell); tomadoWall.Add(cell); tomadoBase.Add(cell); }
                for (int x = LEFT + 8; x < RIGHT - 8; x += 2)
                {
                    bool bloqueado = false;
                    foreach (var p in Plateaus) if (x > p.X - 5 && x < p.X + p.Width + 5) bloqueado = true;
                    int chao = Ground(x);
                    if (cave.Contains(new Vector2I(x, chao)) || cave.Contains(new Vector2I(x, chao + 1))) bloqueado = true;
                    if (bloqueado) continue;
                    // As tres camadas tem mata: o plano de jogo, o fundo e o Base entre eles.
                    Tree(x, chao, trunks, leaves, tomadoCompose);
                    Tree(x + rng.Next(0, 2), chao, wallTrunks, wallLeaves, tomadoWall);
                    Tree(x + rng.Next(0, 3), chao, baseTrunks, baseLeaves, tomadoBase);
                }

                // Torre: bico de 4 linhas e paredes "#.....#" ate o chao, oca, fundo de madeira.
                void Tower(int x0, int chao, int altura, string abertura)
                {
                    int yTopo = chao - altura + 1;
                    Carimbo(TopoTorre, x0, yTopo, wood);
                    for (int y = yTopo + TopoTorre.Length; y <= chao; y++)
                    {
                        wood.Add(new Vector2I(x0, y));
                        wood.Add(new Vector2I(x0 + 6, y));
                        for (int i = 1; i <= 5; i++) interior.Add(new Vector2I(x0 + i, y));
                    }
                    for (int j = 2; j < TopoTorre.Length; j++)      // miolo do bico tambem e oco
                        for (int i = 1; i <= 5; i++)
                            if (TopoTorre[j][i] == '.') interior.Add(new Vector2I(x0 + i, yTopo + j));
                    if (abertura == "porta")
                        for (int y = chao; y > chao - 3; y--)
                        { var c = new Vector2I(x0 + 6, y); wood.Remove(c); interior.Remove(c); }
                    if (abertura == "janela")
                        for (int y = yTopo + 6; y <= yTopo + 8; y++)
                            for (int i = 2; i <= 3; i++) interior.Remove(new Vector2I(x0 + i, y));
                }
                Tower(Plateaus[0].X + 4, Ground(Plateaus[0].X) - 1, 12, "fechada");
                Tower(Plateaus[1].X + 4, Ground(Plateaus[1].X) - 1, 14, "porta");
                Tower(Plateaus[2].X + 4, Ground(Plateaus[2].X) - 1, 18, "janela");
                foreach (var cell in wood) { body.Remove(cell); grass.Remove(cell); leaves.Remove(cell); trunks.Remove(cell); }
                foreach (var cell in interior) { body.Remove(cell); grass.Remove(cell); leaves.Remove(cell); trunks.Remove(cell); }

                static Array<Vector2I> A(IEnumerable<Vector2I> cells)
                {
                    var a = new Array<Vector2I>();
                    foreach (var c in cells) a.Add(c);
                    return a;
                }
                // Base: corpo de terra em toda celula solida, mais a mata dessa camada.
                baseLayer.SetCellsTerrainConnect(A(body), BODY, 0, false);
                baseLayer.SetCellsTerrainConnect(A(baseTrunks), WOOD, 0, false);
                baseLayer.SetCellsTerrainConnect(A(baseLeaves), SHEET, 0, false);

                // Compose: grama sobre as mesmas celulas, mais troncos, copas e torres.
                compose.SetCellsTerrainConnect(A(grass), GRASS, 0, false);
                compose.SetCellsTerrainConnect(A(trunks), WOOD, 0, false);
                compose.SetCellsTerrainConnect(A(leaves), SHEET, 0, false);
                compose.SetCellsTerrainConnect(A(wood), WOOD, 0, false);

                // Wall: terra de fundo onde ha terra E onde ha caverna, mais arvores de fundo e o
                // forro de madeira das torres. A janela fica sem forro, entao aparece como vao.
                var wallCells = new HashSet<Vector2I>(earth);
                foreach (var c in cave) wallCells.Add(c);
                walls.SetCellsTerrainConnect(A(wallCells), BODY, 0, false);
                walls.SetCellsTerrainConnect(A(wallTrunks), WOOD, 0, false);
                walls.SetCellsTerrainConnect(A(wallLeaves), SHEET, 0, false);
                walls.SetCellsTerrainConnect(A(interior), WOOD, 0, false);

                var novo = new PackedScene();
                novo.Pack(root);
                var erro = ResourceSaver.Save(novo, path);
                GD.Print("UPSIDEDOWN: corpo=" + body.Count + " grama=" + grass.Count + " caverna=" + cave.Count
                    + " troncos=" + trunks.Count + " folhas=" + leaves.Count + " madeira=" + wood.Count
                    + " interior=" + interior.Count + " wall=" + wallCells.Count
                    + " arvores_wall=" + wallLeaves.Count + " arvores_base=" + baseLeaves.Count
                    + " salvar=" + erro);
                GetTree().Quit(erro == Error.Ok ? 0 : 1);
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
