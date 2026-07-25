using Godot;
using System;
using System.Collections.Generic;

namespace Jogo25D.TileEntities
{
    // Registro estatico de "como construir" cada tipo de TileEntity - so
    // um Dictionary<string, Func<...>> direto (mesmo formato de
    // ItemIndicatorFactory/ActionIndicatorFactory), sem classe wrapper: o
    // unico metodo real e CreateInstance, que sempre fabrica um TileEntity
    // NOVO, cada celula com a sua propria instancia, nunca compartilhada
    // entre celulas.
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
