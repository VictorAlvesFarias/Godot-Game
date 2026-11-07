using Godot;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Systems;
using Jogo25D.Utils.Coordinates;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Dimensions
{
    // Topo optico por coluna (so no procedural): o servidor mantem, a cada edicao de terreno, o y da celula opaca mais
    // alta de cada coluna considerando todas as edicoes do mundo (inclusive as que nao estao na memoria), guarda no banco
    // os que diferem do gerado e manda a cada jogador os topos da faixa de colunas que a sombra do sol dele alcanca.
    public partial class Dimension
    {
        #region Dinamic properties

        // Quantas celulas a busca do proximo opaco desce numa coluna antes de considerar a coluna aberta.
        private const int ColumnScanLimit = 4096;

        // Inclinacao maxima do sol considerada na faixa (tan 20 graus): o sol inclina -3,5 graus mais ate 8 de penumbra.
        private const float SunTiltForBand = 0.364f;

        // Sentinela de "sem topo" (coluna segue a geracao) e de "sem topo mais alto" nos RPCs.
        private const int NoTop = int.MinValue;

        // Faixa de colunas [From, To] ja enviada a cada peer remoto.
        private readonly Dictionary<long, (int From, int To)> _columnBands = new();

        #endregion

        #region Core - Servidor

        // Servidor: uma edicao de terreno na celula (ja aplicada no cache) pode mudar o topo da coluna.
        private void UpdateColumnTop(Vector2I cell)
        {
            if (!Procedural || !WorldStore.IsOpen || !IsServerAuthoritative())
            {
                return;
            }

            var terrain = EnsureTerrain();
            var x = cell.X;
            var generated = terrain.GeneratedTop(x, Math.Max(cell.Y, terrain.TerrainBottom) + ColumnScanLimit);
            var stored = WorldStore.ReadColumnTop(DimensionId, x);
            var current = stored ?? generated;
            var opaque = terrain.Opacity(cell.X, cell.Y) > 0;
            int top;

            if (opaque && cell.Y < current)
            {
                top = cell.Y;
            }
            else if (!opaque && cell.Y == current)
            {
                top = NextOpaqueBelow(x, cell.Y);
            }
            else
            {
                return;
            }

            int? next = top == generated ? null : top;

            if (next == stored)
            {
                return;
            }

            WorldStore.SetColumnTop(DimensionId, x, next);

            PublishColumnTop(x, next, WorldStore.ReadHighestColumnTop(DimensionId));
        }

        // Proxima celula opaca da coluna abaixo de y. Em cada celula vale a edicao na memoria, se houver; senao a salva no
        // banco (so se o chunk nao veio do banco para a memoria); senao a geracao. TerrainCache.OpenColumn quando nao ha
        // ate o limite.
        private int NextOpaqueBelow(int x, int y)
        {
            var terrain = EnsureTerrain();
            var saved = WorldStore.ReadColumnEdits(DimensionId, x, y).ToDictionary(edit => edit.Y, edit => edit.BlockId);

            for (var cy = y + 1; cy <= y + ColumnScanLimit; cy++)
            {
                byte opacity;

                if (terrain.TryEdit(x, cy, out var material))
                {
                    opacity = TerrainCache.TerrainOpacity(material);
                }
                else if (!terrain.IsChunkLoaded(CoordinateUtilities.CellToChunk(new Vector2I(x, cy))) && saved.TryGetValue(cy, out var blockId))
                {
                    opacity = TerrainCache.TerrainOpacity(TerrainCache.TerrainOf(blockId));
                }
                else
                {
                    opacity = terrain.GeneratedOpacity(x, cy);
                }

                if (opacity > 0)
                {
                    return cy;
                }
            }

            return TerrainCache.OpenColumn;
        }

        // O topo mudou: o cache desta maquina (a luz do host) e todos os peers (que aplicam se a coluna esta na faixa deles;
        // o topo mais alto do mundo vale para todos).
        private void PublishColumnTop(int x, int? top, int? highest)
        {
            EnsureTerrain().SetKnownTop(x, top, highest);

            var ownPeerId = OwnPeerId();

            foreach (var peerId in _columnBands.Keys)
            {
                if (peerId != ownPeerId && IsPeerConnected(peerId))
                {
                    RpcId(peerId, nameof(ColumnTopReceive), x, top ?? NoTop, highest ?? NoTop);
                }
            }
        }

        // Servidor, a cada avaliacao: a faixa de colunas de cada jogador (os chunks carregados em volta dele mais o
        // alcance da sombra inclinada ate o topo mais alto do mundo). Quando muda, os topos da faixa vao em lote.
        private void UpdateColumnBands(List<Player> players)
        {
            if (!Procedural || !WorldStore.IsOpen)
            {
                return;
            }

            var terrain = EnsureTerrain();
            var highest = WorldStore.ReadHighestColumnTop(DimensionId);
            var ownPeerId = OwnPeerId();
            var chunkSize = ChunkStreamingConstants.CHUNK_SIZE;
            var radius = ChunkStreamingConstants.UNLOAD_RADIUS_CHUNKS;

            foreach (var player in players)
            {
                var chunk = CoordinateUtilities.WorldToChunk(player.GlobalPosition, TileSize);
                var bottom = (chunk.Y + radius + 1) * chunkSize;
                var sky = Math.Min(terrain.SkyTop, highest ?? int.MaxValue);
                var margin = (int)Math.Ceiling(SunTiltForBand * Math.Max(0, bottom - sky)) + chunkSize;

                // Faixa arredondada para fora em blocos de 4 chunks: andar dentro do bloco nao reenvia nada.
                var step = chunkSize * 4;
                var from = (int)Math.Floor((double)((chunk.X - radius) * chunkSize - margin) / step) * step;
                var to = (int)Math.Ceiling((double)((chunk.X + radius + 1) * chunkSize + margin) / step) * step - 1;

                if (player.PeerId == ownPeerId)
                {
                    if (terrain.KnownTopsBand != (from, to))
                    {
                        terrain.SetKnownTops(from, to, WorldStore.ReadColumnTops(DimensionId, from, to), highest);
                    }

                    continue;
                }

                if (_columnBands.TryGetValue(player.PeerId, out var band) && band == (from, to))
                {
                    continue;
                }

                _columnBands[player.PeerId] = (from, to);

                var tops = WorldStore.ReadColumnTops(DimensionId, from, to);

                RpcId(player.PeerId, nameof(ColumnTopsReceive), from, to, tops.Keys.ToArray(), tops.Values.ToArray(), highest ?? NoTop);
            }
        }

        // Migracao de um mundo salvo antes dos topos: calcula o topo de cada coluna com edicoes de terreno a partir do
        // banco (o cache ainda nao tem as edicoes) e guarda os que diferem do gerado.
        public void MigrateColumnTops()
        {
            if (!Procedural || !WorldStore.IsOpen)
            {
                return;
            }

            var terrain = EnsureTerrain();

            foreach (var x in WorldStore.ReadEditedColumns(DimensionId))
            {
                var saved = WorldStore.ReadColumnEdits(DimensionId, x, int.MinValue).ToDictionary(edit => edit.Y, edit => edit.BlockId);
                var start = Math.Min(terrain.SkyTop, saved.Keys.Min());
                var top = TerrainCache.OpenColumn;

                for (var cy = start; cy <= Math.Max(start, terrain.TerrainBottom) + ColumnScanLimit; cy++)
                {
                    var opacity = saved.TryGetValue(cy, out var blockId)
                        ? TerrainCache.TerrainOpacity(TerrainCache.TerrainOf(blockId))
                        : terrain.GeneratedOpacity(x, cy);

                    if (opacity > 0)
                    {
                        top = cy;

                        break;
                    }
                }

                var generated = terrain.GeneratedTop(x, Math.Max(start, terrain.TerrainBottom) + ColumnScanLimit);

                if (top != generated)
                {
                    WorldStore.SetColumnTop(DimensionId, x, top);
                }
            }
        }

        #endregion

        #region Core - Rpc - Topos

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ColumnTopsReceive(int fromX, int toX, int[] xs, int[] tops, int highest)
        {
            var known = new Dictionary<int, int>();

            for (var i = 0; i < xs.Length && i < tops.Length; i++)
            {
                known[xs[i]] = tops[i];
            }

            EnsureTerrain().SetKnownTops(fromX, toX, known, highest == NoTop ? null : highest);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void ColumnTopReceive(int x, int top, int highest)
        {
            var terrain = EnsureTerrain();
            var band = terrain.KnownTopsBand;

            if (band is (int from, int to) && x >= from && x <= to)
            {
                terrain.SetKnownTop(x, top == NoTop ? null : top, highest == NoTop ? null : highest);
            }
            else
            {
                terrain.SetHighestKnownTop(highest == NoTop ? null : highest);
            }
        }

        #endregion
    }
}
