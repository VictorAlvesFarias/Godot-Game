using Godot;
using System;
using System.Diagnostics;

namespace Jogo25D.Testing
{
    // Mede o custo do padrao de bind por frame do LightMap2D: para cada sprite de overlay, reescrever
    // os parametros de shader do feixe e da composicao solar, mais as leituras do recurso Settings.
    public partial class BindCostProbe : Node
    {
        public override void _Ready()
        {
            try
            {
                var shader = GD.Load<Shader>("res://Features/World/Light/Resources/TerrainLightOverlay.gdshader");
                var settings = GD.Load<Resource>("res://Assets/Data/LightMap.tres");
                var textura = (Texture2D)ImageTexture.CreateFromImage(Image.CreateEmpty(8, 8, false, Image.Format.Rgba8));

                foreach (int sprites in new[] { 32, 64 })
                {
                    var materiais = new ShaderMaterial[sprites];
                    for (int i = 0; i < sprites; i++) materiais[i] = new ShaderMaterial { Shader = shader };

                    float Setting(string nome, float padrao)
                    {
                        var v = settings?.Get(nome);
                        return v.HasValue && v.Value.VariantType == Variant.Type.Float ? (float)v.Value : padrao;
                    }

                    const int Frames = 300;
                    var relogio = Stopwatch.StartNew();
                    for (int f = 0; f < Frames; f++)
                    {
                        foreach (var m in materiais)
                        {
                            // WindowBeamCache.Bind
                            m.SetShaderParameter("window_sun_color", Colors.White);
                            m.SetShaderParameter("window_beam_ready", true);
                            m.SetShaderParameter("window_beam", textura);
                            m.SetShaderParameter("window_cells", textura);
                            m.SetShaderParameter("sky_access", textura);
                            m.SetShaderParameter("beam_origin", Vector2.Zero);
                            m.SetShaderParameter("beam_axis_x", Vector2.Right);
                            m.SetShaderParameter("beam_axis_y", Vector2.Down);
                            m.SetShaderParameter("beam_size", new Vector2(160, 128));
                            // BindSolarComposition
                            m.SetShaderParameter("include_skylight", true);
                            m.SetShaderParameter("shadow_geometry", textura);
                            m.SetShaderParameter("map_size", new Vector2(320, 256));
                            m.SetShaderParameter("sun_angle", 0.5f);
                            m.SetShaderParameter("penumbra", 0.5f);
                            m.SetShaderParameter("geometry_ready", true);
                            m.SetShaderParameter("shadow_strength", Setting("ShadowStrength", 0.65f));
                            m.SetShaderParameter("ambient_light_influence", Setting("AmbientLightInfluence", 0.75f));
                            m.SetShaderParameter("penumbra_shadow_transition", Setting("SunPenumbraShadowCurve", 1));
                            m.SetShaderParameter("penumbra_ambient_transition", Setting("SunPenumbraAmbientCurve", 1));
                        }
                    }
                    relogio.Stop();
                    double porFrame = relogio.Elapsed.TotalMilliseconds / Frames;
                    GD.Print($"{sprites,3} sprites x 19 parametros = {sprites * 19,5} chamadas/frame  ->  "
                        + porFrame.ToString("F3") + " ms por frame");
                }

                GD.Print("BIND OK");
                GetTree().Quit();
            }
            catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        }
    }
}
