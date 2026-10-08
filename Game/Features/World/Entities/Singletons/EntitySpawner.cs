using Godot;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Instances;
using Jogo25D.Props;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Entities
{
    public static class EntitySpawner
    {
        #region Core - Spawn generico

        public static Node2D Spawn(Godot.Collections.Dictionary record)
        {
            var node = EntityRecord.Build(record);

            if (node == null)
            {
                return null;
            }

            var entities = Dimension.Get(record[EntityRecord.DIMENSION].AsString())?.Entities;

            if (entities == null)
            {
                node.QueueFree();

                return null;
            }

            entities.AddChild(node);

            return node;
        }

        public static Node2D FindByInstanceId(long instanceId)
        {
            if (instanceId == 0)
            {
                return null;
            }

            var name = EntityRecord.NameOf(instanceId);

            foreach (var dimension in Dimension.All)
            {
                var node = dimension.Entities?.GetNodeOrNull<Node2D>(name);

                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }

        #endregion

        #region Core - Rpc

        public static void SpawnRequest(Godot.Collections.Dictionary record, long targetPeerId = 0)
        {
            if (GameLoop.Multiplayer == null || !GameLoop.Multiplayer.HasMultiplayerPeer())
            {
                return;
            }

            var root = SpawnRoot();

            if (targetPeerId == 0)
            {
                root?.Rpc(nameof(Dimension.SpawnReceive), record);
            }
            else
            {
                root?.RpcId(targetPeerId, nameof(Dimension.SpawnReceive), record);
            }
        }

        public static void DespawnRequest(long instanceId)
        {
            if (GameLoop.Multiplayer == null || !GameLoop.Multiplayer.HasMultiplayerPeer())
            {
                ApplyDespawn(instanceId);

                return;
            }

            SpawnRoot()?.Rpc(nameof(Dimension.DespawnReceive), instanceId);
        }

        public static void DespawnForPeer(long targetPeerId, long instanceId)
        {
            if (GameLoop.Multiplayer != null && GameLoop.Multiplayer.HasMultiplayerPeer())
            {
                SpawnRoot()?.RpcId(targetPeerId, nameof(Dimension.DespawnReceive), instanceId);
            }
        }

        public static void ApplyDespawn(long instanceId)
        {
            FindByInstanceId(instanceId)?.QueueFree();
        }

        private static Dimension SpawnRoot()
        {
            return Dimension.Get(Constants.ChunkStreamingConstants.UPSIDEDOWN_ID);
        }

        #endregion

        #region Core - Item no chao

        public static long SpawnWorldItemRequest(ItemData itemData, Vector2 position, string dimensionId)
        {
            if (itemData == null)
            {
                return 0;
            }

            var instanceId = InstanceIdGenerator.NextInstanceId();

            var record = new Godot.Collections.Dictionary
            {
                { EntityRecord.SCENE, "res://Scenes/World/Items/WorldItem.tscn" },
                { EntityRecord.DIMENSION, dimensionId },
                { EntityRecord.INSTANCE, instanceId },
                { EntityRecord.POSITION, EntityRecord.WriteVector(position) },
                { "Item", GodotDictionaryParser.ToDictionary(itemData) },
            };

            Spawn(record);
            SpawnRequest(record);

            return instanceId;
        }

        #endregion

        #region Core - Prop

        public static bool SpawnPropAuthoritative(string propId, Vector2 position, string dimensionId)
        {
            var dimension = Dimension.Get(dimensionId);
            var layer = dimension?.Layer;

            if (layer == null || dimension.Entities == null)
            {
                return false;
            }

            var cell = layer.LocalToMap(layer.ToLocal(position));

            if (layer.GetCellSourceId(cell) != -1 || layer.GetCellSourceId(cell + Vector2I.Down) == -1)
            {
                return false;
            }

            var definition = PropDB.Get(propId);

            if (definition == null)
            {
                return false;
            }

            var record = new Godot.Collections.Dictionary
            {
                { EntityRecord.SCENE, definition.ScenePath },
                { EntityRecord.DIMENSION, dimensionId },
                { EntityRecord.INSTANCE, InstanceIdGenerator.NextInstanceId() },
                { EntityRecord.POSITION, EntityRecord.WriteVector(position) },
                { "PropId", propId },
            };

            Spawn(record);
            SpawnRequest(record);

            return true;
        }

        #endregion
    }
}
