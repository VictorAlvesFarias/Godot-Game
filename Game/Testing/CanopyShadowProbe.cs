using Godot;
using Jogo25D.Light;
using System;

namespace Jogo25D.Testing
{
    // Isola as duas queixas: miolo de copa preto com transicao brusca, e sombra de coluna fina.
    public partial class CanopyShadowProbe : Node
    {
        public override async void _Ready()
        {
            try
            {
                const int PX = 16;
                var world = new LogicalLightWorld(0, "canopy", 1, false);
                // Copa solida 15x15 flutuando, igual ao modelo grande da cena.
                for (int y = 4; y < 19; y++) for (int x = 4; x < 19; x++) world.SetTerrain(x, y, 0);
                // Coluna de 1 tile ao lado, para ver a sombra que ela lanca.
                for (int y = 6; y < 20; y++) world.SetTerrain(24, y, 0);
                var geometry = new AnalyticShadowGeometry();
                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/projected_shadow_debug.gdshader") };
                var pass = new SubViewport { Size = new(512, 512), Disable3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(pass);
                pass.AddChild(new ColorRect { Size = pass.Size, Color = Colors.White });
                pass.AddChild(new ColorRect { Size = pass.Size, Material = material });
                material.SetShaderParameter("map_size", new Vector2(64, 64));
                material.SetShaderParameter("geometry_ready", true);
                foreach (float penumbra in new[] { 0f, 0.5f, 1f })
                {
                    geometry.Begin(world, Vector2I.Zero, new(32, 32), -10, penumbra);
                    while (!geometry.Complete) geometry.Process(10000);
                    material.SetShaderParameter("shadow_geometry", ImageTexture.CreateFromImage(geometry.Image()));
                    material.SetShaderParameter("sun_angle", Mathf.DegToRad(-10));
                    material.SetShaderParameter("penumbra", penumbra);
                    for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var frame = pass.GetTexture().GetImage();
                    frame.SavePng(ProjectSettings.GlobalizePath($"res://../.images/copa-sombra-{penumbra}.png"));
                    // Perfil horizontal atravessando o meio da copa, linha y=11.
                    var perfil = "";
                    for (int x = 2; x < 22; x++) perfil += ((int)(frame.GetPixel(x * PX + PX / 2, 11 * PX + PX / 2).R * 99)).ToString("D2") + " ";
                    GD.Print("penumbra=" + penumbra + " perfil da copa (x=2..21, 00=preto 99=claro):");
                    GD.Print("   " + perfil);
                    // Largura da transicao na borda esquerda da copa, em pixels de tela.
                    int transicao = 0;
                    for (int x = 2 * PX; x < 12 * PX; x++)
                    {
                        float v = frame.GetPixel(x, 11 * PX + PX / 2).R;
                        if (v > 0.02f && v < 0.98f) transicao++;
                    }
                    GD.Print("   transicao na borda: " + transicao + " px de tela (" + (transicao / (float)PX).ToString("F1") + " tiles)");
                }
                GD.Print("PROBE OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
