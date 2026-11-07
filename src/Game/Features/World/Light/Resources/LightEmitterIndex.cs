using Godot;
using Jogo25D.Blocks;
using System.Collections.Generic;

namespace Jogo25D.Light
{
    // Blocos que emitem luz, por tile (source + atlas) e por source: um bloco de terreno emite em qualquer tile do source
    // dele (todas as variacoes do autotile ficam no mesmo source).
    public sealed class LightEmitterIndex
    {
        public Dictionary<(int SourceId, Vector2I AtlasCoord), BlockDefinition> ByTile { get; } = new();
        public Dictionary<int, BlockDefinition> BySource { get; } = new();

        // Por material do TerrainCache (o terreno do bloco, ou o source quando ele nao tem terreno).
        public Dictionary<int, BlockDefinition> ByMaterial { get; } = new();
    }
}
