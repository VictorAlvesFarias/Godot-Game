using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Entities
{
    public static class EntityRecord
    {
        #region Chaves

        public const string SCENE = "ScenePath";
        public const string DIMENSION = "DimensionId";
        public const string INSTANCE = "InstanceId";
        public const string POSITION = "Position";

        #endregion

        #region Core - Nome e identidade

        public static string NameOf(long instanceId)
        {
            return $"E{instanceId}";
        }

        public static long InstanceIdOf(Node node)
        {
            var name = node?.Name.ToString();

            return name != null && name.Length > 1 && name[0] == 'E' && long.TryParse(name[1..], out var id)
                ? id
                : 0;
        }

        #endregion

        #region Core - Vetor

        public static Godot.Collections.Dictionary WriteVector(Vector2 value)
        {
            return new Godot.Collections.Dictionary { { "x", value.X }, { "y", value.Y } };
        }

        public static Vector2 ReadVector(Godot.Collections.Dictionary record, string key)
        {
            if (record == null || !record.TryGetValue(key, out var raw))
            {
                return Vector2.Zero;
            }

            var dict = raw.AsGodotDictionary();

            return new Vector2(
                dict.TryGetValue("x", out var x) ? x.AsSingle() : 0f,
                dict.TryGetValue("y", out var y) ? y.AsSingle() : 0f);
        }

        #endregion

        #region Core - Construcao

        public static Node2D Build(Godot.Collections.Dictionary record)
        {
            if (record == null || !record.ContainsKey(SCENE))
            {
                return null;
            }

            var instanceId = record.TryGetValue(INSTANCE, out var id) ? id.AsInt64() : 0;

            if (instanceId != 0 && EntitySpawner.FindByInstanceId(instanceId) != null)
            {
                return null;
            }

            var scene = GD.Load<PackedScene>(record[SCENE].AsString());

            if (scene == null)
            {
                return null;
            }

            var node = scene.Instantiate<Node2D>();

            GodotDictionaryParser.ApplyTo(node, record);

            node.Position = ReadVector(record, POSITION);
            node.Name = NameOf(instanceId);

            return node;
        }

        #endregion
    }
}
