using System;

namespace Jogo25D.Utils.GodotDictionaryParser
{
    /// <summary>
    /// Marca uma propriedade para ser incluida na (de)serializacao feita por GodotDictionaryParser.
    /// Independente do [Export] do Godot: controla apenas o que trafega via RPC como Dictionary.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class GodotDictionaryFieldAttribute : Attribute
    {
    }
}
