using Godot;
using System;
using System.Collections.Generic;

namespace Jogo25D.TileEntities
{
    public static class TileEntityFactory
    {
        private static readonly Dictionary<string, Func<Vector2I, Node2D, Vector2, TileEntity>> _factories = new()
        {
            { "portal", (cell, world, position) => new PortalTileEntity(cell, world, position) },
        };

        public static TileEntity CreateInstance(string typeId, Vector2I cell, Node2D world, Vector2 cellPosition)
        {
            if (!_factories.TryGetValue(typeId, out var factory))
            {
                GD.PushWarning($"[TileEntityFactory.CreateInstance] tipo desconhecido: {typeId}");

                return null;
            }

            return factory(cell, world, cellPosition);
        }
    }
}
