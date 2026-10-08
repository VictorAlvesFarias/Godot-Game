using Godot;
using Jogo25D.Constants;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Save.Resources;
using Jogo25D.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Save
{
    public static class WorldDocument
    {
        #region Chaves

        public const string TYPE = "$type";
        public const string REF = "$ref";
        public const string ID = "id";
        public const string POSITION = "position";
        public const string STATE = "state";
        public const string DIMENSIONS = "dimensions";
        public const string NODES = "nodes";
        public const string ENTITIES = "Entities";

        #endregion

        #region Core - Arvore e disco

        public static void Load(WorldSaveData save)
        {
            var document = SaveStorage.LoadWorldDocument(save.WorldId);

            if (document == null)
            {
                return;
            }

            var streaming = WorldStreaming.Current;

            foreach (var raw in DimensionsOf(document))
            {
                var entry = raw.AsGodotDictionary();
                var dimensionId = TextOf(entry, TYPE);
                var dimension = Dimension.Get(dimensionId);

                if (dimension == null)
                {
                    continue;
                }

                SaveSerializer.Read(dimension, StateOf(entry));

                foreach (var rawNode in NodesOf(entry))
                {
                    var entryNode = rawNode.AsGodotDictionary();

                    if (IsReference(entryNode))
                    {
                        continue;
                    }

                    var node = Build(entryNode);

                    if (node == null)
                    {
                        continue;
                    }

                    if (streaming != null && streaming.Enabled)
                    {
                        streaming.Adopt(node, dimensionId);
                    }
                    else
                    {
                        dimension.Entities?.AddChild(node);
                    }
                }
            }
        }

        public static void Save(WorldSaveData save)
        {
            var streaming = WorldStreaming.Current;

            if (save == null || streaming == null)
            {
                return;
            }

            var dimensions = new List<Node2D>();

            foreach (var dimensionId in new[] { ChunkStreamingConstants.OVERWORLD_ID, ChunkStreamingConstants.UPSIDEDOWN_ID })
            {
                var dimension = Dimension.Get(dimensionId);

                if (dimension != null)
                {
                    dimensions.Add(dimension);
                }
            }

            var document = Write(streaming, dimensions, d => streaming.Unloaded(d is Dimension dim ? dim.DimensionId : d.Name));

            document[STATE] = StateOfMeta(save);

            SaveStorage.SaveWorldDocument(save.WorldId, document);
        }

        #endregion

        #region Core - Escrita

        public static Godot.Collections.Dictionary Write(Node2D world, IEnumerable<Node2D> dimensions, Func<Node2D, IEnumerable<Node2D>> unloaded = null)
        {
            var document = NewNode(world, withId: false);

            var list = new Godot.Collections.Array();

            foreach (var dimension in dimensions)
            {
                var entry = NewNode(dimension, withId: false);

                entry[NODES] = WriteChildren(dimension.GetNodeOrNull<Node2D>(ENTITIES), unloaded?.Invoke(dimension));

                list.Add(entry);
            }

            document[DIMENSIONS] = list;

            return document;
        }

        private static Godot.Collections.Array WriteChildren(Node dimension, IEnumerable<Node2D> unloaded)
        {
            var list = new Godot.Collections.Array();
            var seen = new HashSet<ulong>();
            var candidates = dimension?.GetChildren().OfType<Node2D>() ?? Enumerable.Empty<Node2D>();

            if (unloaded != null)
            {
                candidates = candidates.Concat(unloaded);
            }

            foreach (var child in candidates)
            {
                if (!SaveSerializer.IsPersistable(child) || !seen.Add(child.GetInstanceId()))
                {
                    continue;
                }

                var entry = NewNode(child);

                if (entry != null)
                {
                    list.Add(entry);
                }
            }

            return list;
        }

        private static Godot.Collections.Dictionary NewNode(Node2D node, bool withId = true)
        {
            var description = SaveSerializer.Describe(node);
            var entry = new Godot.Collections.Dictionary
            {
                { TYPE, description.Type },
            };

            var identity = string.IsNullOrEmpty(description.Ref) ? node.Name.ToString() : ExternalIdentity(node);

            if (withId && !string.IsNullOrEmpty(identity))
            {
                entry[ID] = identity;
            }

            if (!string.IsNullOrEmpty(description.Ref))
            {
                if (string.IsNullOrEmpty(identity))
                {
                    return null;
                }

                entry[REF] = string.Format(description.Ref, entry.TryGetValue(ID, out var id) ? id.AsString() : "");

                return entry;
            }

            var state = SaveSerializer.Write(node);

            if (withId)
            {
                state[POSITION] = new Godot.Collections.Dictionary { { "x", node.Position.X }, { "y", node.Position.Y } };
            }

            entry[STATE] = state;

            return entry;
        }

        private static string ExternalIdentity(Node2D node)
        {
            return node is Jogo25D.Characters.Player player ? player.CharacterId : node.Name.ToString();
        }

        public static Godot.Collections.Dictionary NewReference(string type, string id, string path)
        {
            return new Godot.Collections.Dictionary
            {
                { TYPE, type },
                { ID, id },
                { REF, path },
            };
        }

        #endregion

        #region Core - Leitura

        public static Godot.Collections.Array DimensionsOf(Godot.Collections.Dictionary document)
        {
            return document != null && document.TryGetValue(DIMENSIONS, out var list)
                ? list.AsGodotArray()
                : new Godot.Collections.Array();
        }

        public static Godot.Collections.Array NodesOf(Godot.Collections.Dictionary dimension)
        {
            return dimension != null && dimension.TryGetValue(NODES, out var list)
                ? list.AsGodotArray()
                : new Godot.Collections.Array();
        }

        public static Godot.Collections.Dictionary StateOf(Godot.Collections.Dictionary entry)
        {
            return entry != null && entry.TryGetValue(STATE, out var state)
                ? state.AsGodotDictionary()
                : new Godot.Collections.Dictionary();
        }

        public static string TextOf(Godot.Collections.Dictionary entry, string key)
        {
            return entry != null && entry.TryGetValue(key, out var value) ? value.AsString() : "";
        }

        public static Godot.Collections.Dictionary StateOfMeta(Resource meta)
        {
            var state = new Godot.Collections.Dictionary();

            foreach (var (key, value) in Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToDictionary(meta))
            {
                var name = key.AsString();

                if (name == TYPE)
                {
                    continue;
                }

                state[char.ToLowerInvariant(name[0]) + name[1..]] = value;
            }

            return state;
        }

        public static T MetaOf<T>(Godot.Collections.Dictionary document) where T : Resource
        {
            var state = new Godot.Collections.Dictionary();

            foreach (var (key, value) in StateOf(document))
            {
                var name = key.AsString();

                state[char.ToUpperInvariant(name[0]) + name[1..]] = value;
            }

            return Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToResource<T>(state);
        }

        public static bool IsReference(Godot.Collections.Dictionary entry)
        {
            return entry != null && entry.ContainsKey(REF);
        }

        public static Node2D Build(Godot.Collections.Dictionary entry)
        {
            var path = SaveSerializer.SceneOf(TextOf(entry, TYPE));

            if (string.IsNullOrEmpty(path) || GD.Load<PackedScene>(path) is not PackedScene scene)
            {
                GD.PushError($"[WorldDocument] cena nao encontrada para \"{TextOf(entry, TYPE)}\"");

                return null;
            }

            var node = scene.Instantiate<Node2D>();
            var id = TextOf(entry, ID);

            if (!string.IsNullOrEmpty(id))
            {
                node.Name = id;
            }

            var state = StateOf(entry);

            if (state.TryGetValue(POSITION, out var position))
            {
                var pair = position.AsGodotDictionary();

                node.Position = new Vector2(pair["x"].AsSingle(), pair["y"].AsSingle());
            }

            SaveSerializer.Read(node, state);

            return node;
        }

        #endregion
    }
}
