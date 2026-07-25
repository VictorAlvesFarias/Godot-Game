using Godot;
using System;

namespace Jogo25D.TileEntities
{
    // Entrada de registro no TileEntityDB - espelha o par
    // ItemDefinition/ItemDB (Features/World/Items/Singletons/ItemDB.cs):
    // dado estatico/compartilhado (aqui, so a factory) separado da
    // instancia viva por posicao.
    public class TileEntityDefinition
    {
        public string TypeId { get; set; }
        public Func<Vector2I, Node2D, Vector2, TileEntity> Factory { get; set; }
    }
}
