using Godot;
using Jogo25D.Biomes;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Instances;
using Jogo25D.Props;
using Jogo25D.Utils.Coordinates;
using System.Linq;

namespace Jogo25D.Enemies
{
    // Servidor: faz inimigos nascerem perto de cada jogador vivo na dimensao invertida, fora da tela, com um limite por
    // jogador e por tipo. Slimes no chao da superficie (na altura do jogador); morcegos em bolsoes de ar abaixo da
    // superficie gerada. Nunca na agua nem sobre props. Os inimigos sao entidades salvas: o limite conta os que ja estao
    // por perto.
    public static class EnemySpawner
    {
        #region Properties

        public const string SlimeScene = "res://src/Game/Scenes/World/Enemies/Slime.tscn";
        public const string BatScene = "res://src/Game/Scenes/World/Enemies/Bat.tscn";

        private const float Interval = 4f;
        private const int LimitPerPlayer = 3;
        private const int CountRadiusCells = 48;
        private const int AttemptsPerPlayer = 6;

        // Slime: distancia horizontal e faixa vertical da busca de chao.
        private const int SlimeMinDistanceCells = 26;
        private const int SlimeMaxDistanceCells = 40;
        private const int SearchHalfHeightCells = 24;

        // Morcego: distancia e faixa vertical do bolsao de ar, e profundidade minima abaixo da superficie gerada.
        private const int BatMinDistanceCells = 18;
        private const int BatMaxDistanceCells = 30;
        private const int BatVerticalCells = 14;
        private const int BatMinDepthCells = 6;

        private static float _timer;

        #endregion

        #region Core - Ciclo

        // Chamado pelo WorldStreaming no servidor (ou sozinho).
        public static void Tick(float delta)
        {
            _timer += delta;

            if (_timer < Interval)
            {
                return;
            }

            _timer = 0f;

            var dimension = Dimension.Get(ChunkStreamingConstants.UPSIDEDOWN_ID);

            if (dimension?.Layer == null || dimension.Entities == null)
            {
                return;
            }

            var cell = dimension.Layer.TileSet.TileSize.X;

            foreach (var player in Players.All())
            {
                if (player is NPC || !GodotObject.IsInstanceValid(player) || player.CurrentHealth <= 0 || Dimension.IdOf(player) != dimension.DimensionId)
                {
                    continue;
                }

                var origin = player.GlobalPosition;

                if (CountNear<Slime>(dimension, origin) < LimitPerPlayer)
                {
                    TrySpawn(SlimeScene, () =>
                    {
                        var side = GD.Randf() < 0.5f ? -1 : 1;
                        var cells = GD.RandRange(SlimeMinDistanceCells, SlimeMaxDistanceCells);

                        return FindGround(dimension, origin + new Vector2(side * cells * cell, 0f));
                    }, dimension);
                }

                if (dimension.Procedural && CountNear<Bat>(dimension, origin) < LimitPerPlayer)
                {
                    TrySpawn(BatScene, () =>
                    {
                        var side = GD.Randf() < 0.5f ? -1 : 1;
                        var dx = side * GD.RandRange(BatMinDistanceCells, BatMaxDistanceCells);
                        var dy = GD.RandRange(-BatVerticalCells, BatVerticalCells);

                        return FindCaveAir(dimension, origin + new Vector2(dx * cell, dy * cell));
                    }, dimension);
                }
            }
        }

        private static void TrySpawn(string scene, System.Func<Vector2?> pick, Dimension dimension)
        {
            for (var attempt = 0; attempt < AttemptsPerPlayer; attempt++)
            {
                if (pick() is Vector2 spot)
                {
                    Spawn(scene, dimension, spot);

                    return;
                }
            }
        }

        private static int CountNear<T>(Dimension dimension, Vector2 position) where T : Enemy
        {
            var radius = CountRadiusCells * dimension.Layer.TileSet.TileSize.X;

            return dimension.Entities.GetChildren().OfType<T>().Count(enemy => !enemy.Dying && enemy.GlobalPosition.DistanceTo(position) <= radius);
        }

        #endregion

        #region Core - Nascer

        // A vida comeca cheia (o inimigo usa a propria vida maxima quando o registro nao traz Health).
        public static long Spawn(string scene, Dimension dimension, Vector2 position)
        {
            var instanceId = InstanceIdGenerator.NextInstanceId();
            var record = new Godot.Collections.Dictionary
            {
                { EntityRecord.SCENE, scene },
                { EntityRecord.DIMENSION, dimension.DimensionId },
                { EntityRecord.INSTANCE, instanceId },
                { EntityRecord.POSITION, EntityRecord.WriteVector(position) },
            };

            EntitySpawner.Spawn(record);
            EntitySpawner.SpawnRequest(record);

            return instanceId;
        }

        public static long SpawnSlime(Dimension dimension, Vector2 position)
        {
            return Spawn(SlimeScene, dimension, position);
        }

        public static long SpawnBat(Dimension dimension, Vector2 position)
        {
            return Spawn(BatScene, dimension, position);
        }

        // Chao na coluna de `around`, na altura do jogador: a primeira celula que colide, descendo de 24 celulas acima ate
        // 24 abaixo, com 2 celulas livres em cima dela e das duas colunas vizinhas (o corpo do slime passa da largura de
        // uma celula), sem agua e sem prop, num chunk carregado. Devolve o centro do corpo.
        public static Vector2? FindGround(Dimension dimension, Vector2 around)
        {
            var layer = dimension.Layer;
            var start = layer.LocalToMap(layer.ToLocal(around));
            var size = layer.TileSet.TileSize.Y;

            for (var y = start.Y - SearchHalfHeightCells; y <= start.Y + SearchHalfHeightCells; y++)
            {
                var ground = new Vector2I(start.X, y);

                if (!Loaded(dimension, ground) || !TileQueries.Collides(layer, dimension.BaseLayer, ground))
                {
                    continue;
                }

                if (Free(dimension, ground + new Vector2I(-1, -2), 3, 2))
                {
                    var top = layer.ToGlobal(layer.MapToLocal(ground)).Y - size / 2f;

                    return new Vector2(layer.ToGlobal(layer.MapToLocal(ground)).X, top - 9f);
                }
            }

            return null;
        }

        // Bolsao de ar para o morcego: a celula de `around` e as vizinhas (3x3) livres, num chunk carregado, pelo menos
        // BatMinDepthCells abaixo da superficie gerada da coluna (caverna ou tunel, nao o ceu). Devolve o centro.
        public static Vector2? FindCaveAir(Dimension dimension, Vector2 around)
        {
            var layer = dimension.Layer;
            var cell = layer.LocalToMap(layer.ToLocal(around));

            if (!Loaded(dimension, cell) || cell.Y < dimension.EnsureTerrain().GroundHeight(cell.X) + BatMinDepthCells)
            {
                return null;
            }

            return Free(dimension, cell + new Vector2I(-1, -1), 3, 3) ? layer.ToGlobal(layer.MapToLocal(cell)) : null;
        }

        // Ar livre perto de `around` (sem exigir profundidade), para o comando de console.
        public static Vector2? FindAir(Dimension dimension, Vector2 around)
        {
            var layer = dimension.Layer;
            var cell = layer.LocalToMap(layer.ToLocal(around));

            return Loaded(dimension, cell) && Free(dimension, cell + new Vector2I(-1, -1), 3, 3) ? layer.ToGlobal(layer.MapToLocal(cell)) : null;
        }

        // Retangulo de celulas sem bloco, sem agua e sem prop.
        private static bool Free(Dimension dimension, Vector2I from, int width, int height)
        {
            var layer = dimension.Layer;

            for (var dx = 0; dx < width; dx++)
            {
                for (var dy = 0; dy < height; dy++)
                {
                    var cell = from + new Vector2I(dx, dy);

                    if (TileQueries.HasBlock(layer, dimension.BaseLayer, cell)
                        || (dimension.Water?.IsWet(cell) ?? false)
                        || PropCells.At(dimension.DimensionId, cell) != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool Loaded(Dimension dimension, Vector2I cell)
        {
            var layer = dimension.Layer;

            return dimension.IsChunkLoaded(CoordinateUtilities.WorldToChunk(layer.ToGlobal(layer.MapToLocal(cell)), Dimension.TileSize));
        }

        #endregion
    }
}
