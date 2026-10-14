using Godot;
using Jogo25D.Constants;
using Jogo25D.Save.Resources;
using Jogo25D.Save.Structures;
using Jogo25D.Save.Types;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
    // Unico ponto que fala com o banco de um mundo (saves/worlds/<worldId>/world.db).
    // Toda escrita acontece numa transacao: ou entra inteira, ou nao entra.
    public sealed class WorldRepository : IDisposable
    {
        #region Constructors

        private WorldRepository(string filePath)
        {
            FilePath = filePath;

            _connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString());

            _connection.Open();

            Execute("PRAGMA journal_mode = WAL;");
            Execute("PRAGMA synchronous = NORMAL;");

            EnsureSchema();
        }

        #endregion

        #region Dinamic properties

        public const int SchemaVersion = 1;
        public const string FileName = "world.db";

        public string FilePath { get; }

        private readonly SqliteConnection _connection;

        #endregion

        #region Core - Abertura

        public static string PathOf(string worldId)
        {
            return ProjectSettings.GlobalizePath($"{SavesConstants.WORLDS_DIR}/{worldId}/{FileName}");
        }

        public static bool Exists(string worldId)
        {
            return System.IO.File.Exists(PathOf(worldId));
        }

        public static WorldRepository Open(string worldId)
        {
            var path = PathOf(worldId);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            return new WorldRepository(path);
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        private void EnsureSchema()
        {
            Execute(@"
                CREATE TABLE IF NOT EXISTS world (
                    world_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    seed INTEGER NOT NULL,
                    character_mode INTEGER NOT NULL,
                    is_procedural INTEGER NOT NULL,
                    multiplayer_key TEXT NOT NULL DEFAULT '',
                    autosave_interval_min INTEGER NOT NULL,
                    created_utc INTEGER NOT NULL,
                    last_played_utc INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS cell_edits (
                    dimension TEXT NOT NULL,
                    cx INTEGER NOT NULL,
                    cy INTEGER NOT NULL,
                    x INTEGER NOT NULL,
                    y INTEGER NOT NULL,
                    layer INTEGER NOT NULL,
                    block_id TEXT,
                    PRIMARY KEY (dimension, cx, cy, x, y, layer)
                ) WITHOUT ROWID;
                CREATE TABLE IF NOT EXISTS entities (
                    entity_id TEXT PRIMARY KEY,
                    dimension TEXT NOT NULL,
                    type TEXT NOT NULL,
                    cx INTEGER NOT NULL,
                    cy INTEGER NOT NULL,
                    x REAL NOT NULL,
                    y REAL NOT NULL,
                    state TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS entities_by_chunk ON entities (dimension, cx, cy);");

            Execute($"PRAGMA user_version = {SchemaVersion};");
        }

        #endregion

        #region Core - Metadados

        public WorldSaveData ReadMeta()
        {
            using var command = Command(@"
                SELECT world_id, name, seed, character_mode, is_procedural, multiplayer_key,
                       autosave_interval_min, created_utc, last_played_utc
                FROM world LIMIT 1;");
            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            return new WorldSaveData
            {
                WorldId = reader.GetString(0),
                Name = reader.GetString(1),
                Seed = reader.GetInt64(2),
                CharacterMode = (WorldCharacterMode)reader.GetInt32(3),
                IsProcedural = reader.GetInt32(4) != 0,
                MultiplayerKey = reader.GetString(5),
                AutosaveIntervalMinutes = reader.GetInt32(6),
                CreatedUtc = reader.GetInt64(7),
                LastPlayedUtc = reader.GetInt64(8),
            };
        }

        public void WriteMeta(WorldSaveData meta)
        {
            InTransaction(transaction => WriteMeta(meta, transaction));
        }

        private void WriteMeta(WorldSaveData meta, SqliteTransaction transaction)
        {
            using var clear = Command("DELETE FROM world;", transaction);

            clear.ExecuteNonQuery();

            using var insert = Command(@"
                INSERT INTO world (world_id, name, seed, character_mode, is_procedural, multiplayer_key,
                                   autosave_interval_min, created_utc, last_played_utc)
                VALUES ($id, $name, $seed, $mode, $procedural, $key, $autosave, $created, $played);", transaction);

            insert.Parameters.AddWithValue("$id", meta.WorldId);
            insert.Parameters.AddWithValue("$name", meta.Name ?? "");
            insert.Parameters.AddWithValue("$seed", meta.Seed);
            insert.Parameters.AddWithValue("$mode", (int)meta.CharacterMode);
            insert.Parameters.AddWithValue("$procedural", meta.IsProcedural ? 1 : 0);
            insert.Parameters.AddWithValue("$key", meta.MultiplayerKey ?? "");
            insert.Parameters.AddWithValue("$autosave", meta.AutosaveIntervalMinutes);
            insert.Parameters.AddWithValue("$created", meta.CreatedUtc);
            insert.Parameters.AddWithValue("$played", meta.LastPlayedUtc);
            insert.ExecuteNonQuery();
        }

        #endregion

        #region Core - Chunks

        public List<EntityRow> ReadEntities(string dimension, Vector2I chunk)
        {
            using var command = ChunkCommand("SELECT entity_id, type, x, y, state FROM entities", dimension, chunk);

            return ReadEntityRows(command);
        }

        // Entidades da dimensao inteira; com types, so as desses tipos.
        public List<EntityRow> ReadEntities(string dimension, IReadOnlyCollection<string> types = null)
        {
            var filter = "";
            var parameters = new List<(string, string)>();

            if (types != null)
            {
                var names = types.Select((type, index) => $"$type{index}").ToList();

                filter = names.Count == 0 ? " AND 0" : $" AND type IN ({string.Join(", ", names)})";
                parameters.AddRange(types.Select((type, index) => ($"$type{index}", type)));
            }

            using var command = Command($"SELECT entity_id, type, x, y, state FROM entities WHERE dimension = $dimension{filter};");

            command.Parameters.AddWithValue("$dimension", dimension);

            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            return ReadEntityRows(command);
        }

        private static List<EntityRow> ReadEntityRows(SqliteCommand command)
        {
            var result = new List<EntityRow>();

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                result.Add(new EntityRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    (float)reader.GetDouble(2),
                    (float)reader.GetDouble(3),
                    reader.GetString(4)));
            }

            return result;
        }

        public List<CellEdit> ReadEdits(string dimension, Vector2I chunk)
        {
            var result = new List<CellEdit>();

            using var command = ChunkCommand("SELECT x, y, layer, block_id FROM cell_edits", dimension, chunk);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                result.Add(new CellEdit(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    (TerrainLayerType)reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }

            return result;
        }

        // Chunks com algum dado salvo na dimensao (edicao ou entidade).
        public List<Vector2I> ReadChunksWithData(string dimension)
        {
            var result = new List<Vector2I>();

            using var command = Command(@"
                SELECT cx, cy FROM cell_edits WHERE dimension = $dimension
                UNION
                SELECT cx, cy FROM entities WHERE dimension = $dimension;");

            command.Parameters.AddWithValue("$dimension", dimension);

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                result.Add(new Vector2I(reader.GetInt32(0), reader.GetInt32(1)));
            }

            return result;
        }

        // As edicoes de cada snapshot substituem as do chunk no banco; as entidades sao gravadas por chave.
        // Tudo numa transacao: edicoes e entidades dos snapshots, entidades destruidas e metadados.
        public void WriteChunks(IEnumerable<ChunkSnapshot> snapshots, WorldSaveData meta = null, IEnumerable<string> deletedEntityIds = null)
        {
            InTransaction(transaction =>
            {
                foreach (var snapshot in snapshots)
                {
                    WriteChunk(snapshot, transaction);
                }

                if (deletedEntityIds != null)
                {
                    DeleteEntities(deletedEntityIds, transaction);
                }

                if (meta != null)
                {
                    WriteMeta(meta, transaction);
                }
            });
        }

        public void DeleteEntities(IEnumerable<string> entityIds)
        {
            InTransaction(transaction => DeleteEntities(entityIds, transaction));
        }

        private void DeleteEntities(IEnumerable<string> entityIds, SqliteTransaction transaction)
        {
            using var command = Command("DELETE FROM entities WHERE entity_id = $id;", transaction);
            var id = command.Parameters.Add("$id", SqliteType.Text);

            foreach (var entityId in entityIds)
            {
                id.Value = entityId;
                command.ExecuteNonQuery();
            }
        }

        private void WriteChunk(ChunkSnapshot snapshot, SqliteTransaction transaction)
        {
            if (snapshot.IncludesEdits)
            {
                WriteEdits(snapshot, transaction);
            }

            if (snapshot.Entities.Count > 0)
            {
                WriteEntities(snapshot, transaction);
            }
        }

        private void WriteEdits(ChunkSnapshot snapshot, SqliteTransaction transaction)
        {
            using (var clear = ChunkCommand("DELETE FROM cell_edits", snapshot.Dimension, snapshot.Chunk, transaction))
            {
                clear.ExecuteNonQuery();
            }

            using (var insert = Command(@"
                INSERT INTO cell_edits (dimension, cx, cy, x, y, layer, block_id)
                VALUES ($dimension, $cx, $cy, $x, $y, $layer, $block);", transaction))
            {
                insert.Parameters.AddWithValue("$dimension", snapshot.Dimension);
                insert.Parameters.AddWithValue("$cx", snapshot.Chunk.X);
                insert.Parameters.AddWithValue("$cy", snapshot.Chunk.Y);

                var x = insert.Parameters.Add("$x", SqliteType.Integer);
                var y = insert.Parameters.Add("$y", SqliteType.Integer);
                var layer = insert.Parameters.Add("$layer", SqliteType.Integer);
                var block = insert.Parameters.Add("$block", SqliteType.Text);

                foreach (var edit in snapshot.Edits)
                {
                    x.Value = edit.X;
                    y.Value = edit.Y;
                    layer.Value = (int)edit.Layer;
                    block.Value = (object)edit.BlockId ?? DBNull.Value;
                    insert.ExecuteNonQuery();
                }
            }
        }

        // Grava cada entidade pela chave, sem limpar o chunk: as entidades que so estao no banco (longe dos jogadores)
        // continuam la. Uma entidade que mudou de chunk sai da linha antiga pelo INSERT OR REPLACE.
        private void WriteEntities(ChunkSnapshot snapshot, SqliteTransaction transaction)
        {
            using (var insert = Command(@"
                INSERT OR REPLACE INTO entities (entity_id, dimension, type, cx, cy, x, y, state)
                VALUES ($id, $dimension, $type, $cx, $cy, $x, $y, $state);", transaction))
            {
                insert.Parameters.AddWithValue("$dimension", snapshot.Dimension);
                insert.Parameters.AddWithValue("$cx", snapshot.Chunk.X);
                insert.Parameters.AddWithValue("$cy", snapshot.Chunk.Y);

                var id = insert.Parameters.Add("$id", SqliteType.Text);
                var type = insert.Parameters.Add("$type", SqliteType.Text);
                var x = insert.Parameters.Add("$x", SqliteType.Real);
                var y = insert.Parameters.Add("$y", SqliteType.Real);
                var state = insert.Parameters.Add("$state", SqliteType.Text);

                foreach (var entity in snapshot.Entities)
                {
                    id.Value = entity.EntityId;
                    type.Value = entity.Type;
                    x.Value = entity.X;
                    y.Value = entity.Y;
                    state.Value = entity.State;
                    insert.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Utils

        private void InTransaction(Action<SqliteTransaction> work)
        {
            using var transaction = _connection.BeginTransaction();

            work(transaction);

            transaction.Commit();
        }

        private SqliteCommand Command(string sql, SqliteTransaction transaction = null)
        {
            var command = _connection.CreateCommand();

            command.CommandText = sql;
            command.Transaction = transaction;

            return command;
        }

        private SqliteCommand ChunkCommand(string statement, string dimension, Vector2I chunk, SqliteTransaction transaction = null)
        {
            var command = Command($"{statement} WHERE dimension = $dimension AND cx = $cx AND cy = $cy;", transaction);

            command.Parameters.AddWithValue("$dimension", dimension);
            command.Parameters.AddWithValue("$cx", chunk.X);
            command.Parameters.AddWithValue("$cy", chunk.Y);

            return command;
        }

        private void Execute(string sql)
        {
            using var command = Command(sql);

            command.ExecuteNonQuery();
        }

        #endregion
    }
}
