using Godot;
using System.Collections.Generic;

namespace Jogo25D.TileEntities
{
    // Registro estatico de tipos de TileEntity, no mesmo formato de
    // ItemDB/EffectDB/ActionDB/SkillTreeDB (Dictionary<string, Definition>
    // com Initialize()/Register() preguicosos). Diferente do ItemDB, nao ha
    // contador de instancia (NextInstanceId) - a identidade de rede de uma
    // TileEntity e a propria celula (Vector2I), entao nao precisa de um id
    // artificial separado.
    public static class TileEntityDB
    {
        public static Dictionary<string, TileEntityDefinition> Definitions { get; private set; }
        public static bool Initialized { get; set; }

        public static void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            Definitions = new Dictionary<string, TileEntityDefinition>();

            Register(new TileEntityDefinition
            {
                TypeId = "portal",
                Factory = (cell, world, position) => new PortalTileEntity(cell, world, position)
            });

            Initialized = true;
        }

        public static void Register(TileEntityDefinition definition)
        {
            Definitions[definition.TypeId] = definition;
        }

        public static bool TryGet(string typeId, out TileEntityDefinition definition)
        {
            Initialize();

            return Definitions.TryGetValue(typeId, out definition);
        }

        public static TileEntity CreateInstance(string typeId, Vector2I cell, Node2D world, Vector2 cellPosition)
        {
            Initialize();

            if (!Definitions.TryGetValue(typeId, out var definition))
            {
                GD.PushWarning($"[TileEntityDB.CreateInstance] tipo desconhecido: {typeId}");

                return null;
            }

            return definition.Factory(cell, world, cellPosition);
        }
    }
}
