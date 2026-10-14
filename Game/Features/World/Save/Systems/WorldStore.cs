using Godot;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Save;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Structures;
using Jogo25D.Utils.Coordinates;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
    // Liga a sessao de jogo ao banco do mundo: abre, carrega as edicoes
    // e entidades salvas e grava so o que mudou. O acesso ao banco em si fica no WorldRepository.
    public static class WorldStore
    {
        #region Dinamic properties

        public static bool IsOpen => _repository != null;

        private static WorldRepository _repository;

        // Entidades destruidas (item coletado, por exemplo) desde o ultimo save: saem do banco no proximo.
        private static readonly HashSet<string> _deletedEntities = new();

        #endregion

        #region Core - Abertura

        public static void Load(WorldSaveData save)
        {
            Close();

            _repository = WorldRepository.Open(save.WorldId);

            if (_repository.ReadMeta() == null)
            {
                _repository.WriteMeta(save);
            }

            var streaming = WorldStreaming.Current;
            var streamed = streaming != null && streaming.Enabled;
            var residentTypes = SaveSerializer.Types.Where(type => !WorldStreaming.StreamsClass(SaveSerializer.ClassOf(type))).ToList();

            foreach (var dimension in Dimension.All.Where(GodotObject.IsInstanceValid))
            {
                var terrain = dimension.EnsureTerrain();

                // No procedural as edicoes entram por chunk, perto dos jogadores (Dimension.EnsureChunkData).
                // O mundo autoral e finito: tudo fica residente e vai direto para os tilemaps da cena.
                if (!save.IsProcedural)
                {
                    foreach (var chunk in _repository.ReadChunksWithData(dimension.DimensionId))
                    {
                        var edits = _repository.ReadEdits(dimension.DimensionId, chunk);

                        terrain.LoadChunkEdits(chunk, edits);
                        dimension.ApplyEditsToAuthoredLayers(edits);
                        terrain.MarkClean(chunk);
                    }
                }

                // Com streaming, as entidades que ele descarrega ficam no banco ate um jogador chegar perto
                // (WorldStreaming); as que nunca saem da arvore (portais) entram ja.
                var rows = streamed
                    ? _repository.ReadEntities(dimension.DimensionId, residentTypes)
                    : _repository.ReadEntities(dimension.DimensionId);

                foreach (var row in rows)
                {
                    var node = Build(row);

                    if (node != null)
                    {
                        dimension.Entities?.AddChild(node);
                    }
                }
            }
        }

        public static void Close()
        {
            _repository?.Dispose();
            _repository = null;

            _deletedEntities.Clear();
        }

        #endregion

        #region Core - Gravacao

        // Grava, numa transacao, as edicoes dos chunks sujos, as entidades na memoria, as destruidas e os metadados.
        public static void Save(WorldSaveData save)
        {
            if (_repository == null || save == null)
            {
                return;
            }

            var snapshots = new Dictionary<(string, Vector2I), ChunkSnapshot>();
            var cleaned = new List<(TerrainCache Terrain, Vector2I Chunk)>();
            var deleted = _deletedEntities.ToList();

            foreach (var dimension in Dimension.All.Where(GodotObject.IsInstanceValid))
            {
                var terrain = dimension.Terrain;

                if (terrain != null)
                {
                    foreach (var chunk in terrain.DirtyChunks().ToList())
                    {
                        // A gravacao substitui as edicoes do chunk no banco: as salvas antes precisam estar na memoria.
                        dimension.EnsureChunkData(chunk);

                        var snapshot = SnapshotOf(snapshots, dimension.DimensionId, chunk);

                        snapshot.IncludesEdits = true;
                        snapshot.Edits.AddRange(terrain.CellEditsOf(chunk));

                        cleaned.Add((terrain, chunk));
                    }
                }

                // As entidades na arvore; as descarregadas ja estao no banco.
                foreach (var node in dimension.Entities?.GetChildren().OfType<Node2D>() ?? Enumerable.Empty<Node2D>())
                {
                    AddRow(snapshots, dimension.DimensionId, node);
                }
            }

            _repository.WriteChunks(snapshots.Values, save, deleted);

            foreach (var (terrain, chunk) in cleaned)
            {
                terrain.MarkClean(chunk);
            }

            _deletedEntities.ExceptWith(deleted);
        }

        #endregion

        #region Core - Residencia por chunk

        public static List<CellEdit> ReadEdits(string dimension, Vector2I chunk)
        {
            return _repository?.ReadEdits(dimension, chunk) ?? new List<CellEdit>();
        }

        // Grava as edicoes de um chunk que vai sair da memoria.
        public static void SaveChunkEdits(string dimension, TerrainCache terrain, Vector2I chunk)
        {
            if (_repository == null)
            {
                return;
            }

            var snapshot = new ChunkSnapshot(dimension, chunk) { IncludesEdits = true };

            snapshot.Edits.AddRange(terrain.CellEditsOf(chunk));

            _repository.WriteChunks(new[] { snapshot });

            terrain.MarkClean(chunk);
        }

        public static List<EntityRow> ReadEntities(string dimension, Vector2I chunk)
        {
            return _repository?.ReadEntities(dimension, chunk) ?? new List<EntityRow>();
        }

        // Grava uma entidade que vai sair da memoria. Falso se ela nao e persistivel (o chamador a mantem viva).
        public static bool SaveEntity(string dimension, Node2D node)
        {
            if (_repository == null)
            {
                return false;
            }

            var snapshots = new Dictionary<(string, Vector2I), ChunkSnapshot>();

            if (!AddRow(snapshots, dimension, node))
            {
                return false;
            }

            _repository.WriteChunks(snapshots.Values);

            _deletedEntities.Remove(node.Name);

            return true;
        }

        // Entidade destruida no jogo: a linha dela sai do banco no proximo save.
        public static void ForgetEntity(string entityId)
        {
            if (_repository != null && !string.IsNullOrEmpty(entityId))
            {
                _deletedEntities.Add(entityId);
            }
        }

        #endregion

        #region Core - Entidades

        public static EntityRow? RowOf(Node2D node)
        {
            var description = SaveSerializer.Describe(node);

            // So entidades do mundo tem SaveScene; personagens tem save proprio.
            if (description == null)
            {
                return null;
            }

            // A posicao fica nas colunas da linha; o state guarda so as propriedades [Save].
            var state = SaveSerializer.Write(node);

            return new EntityRow(node.Name, description.Type, node.Position.X, node.Position.Y, Json.Stringify(state));
        }

        // Recria a entidade pela cena do tipo, com o id como nome, a posicao das colunas e o state aplicado.
        public static Node2D Build(EntityRow row)
        {
            var path = SaveSerializer.SceneOf(row.Type);

            if (string.IsNullOrEmpty(path) || GD.Load<PackedScene>(path) is not PackedScene scene)
            {
                GD.PushError($"[WorldStore] cena nao encontrada para \"{row.Type}\"");

                return null;
            }

            var node = scene.Instantiate<Node2D>();
            var state = Json.ParseString(row.State);

            node.Name = row.EntityId;
            node.Position = new Vector2(row.X, row.Y);

            SaveSerializer.Read(node, state.VariantType == Variant.Type.Dictionary ? state.AsGodotDictionary() : new Godot.Collections.Dictionary());

            return node;
        }

        #endregion

        #region Utils

        private static bool AddRow(Dictionary<(string, Vector2I), ChunkSnapshot> snapshots, string dimension, Node2D node)
        {
            if (!GodotObject.IsInstanceValid(node) || RowOf(node) is not EntityRow row)
            {
                return false;
            }

            var chunk = CoordinateUtilities.WorldToChunk(node.Position, Dimension.TileSize);

            SnapshotOf(snapshots, dimension, chunk).Entities.Add(row);

            return true;
        }

        private static ChunkSnapshot SnapshotOf(Dictionary<(string, Vector2I), ChunkSnapshot> snapshots, string dimension, Vector2I chunk)
        {
            if (!snapshots.TryGetValue((dimension, chunk), out var snapshot))
            {
                snapshots[(dimension, chunk)] = snapshot = new ChunkSnapshot(dimension, chunk) { IncludesEdits = false };
            }

            return snapshot;
        }

        #endregion
    }
}
