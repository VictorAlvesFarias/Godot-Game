using System;

namespace Jogo25D.Utils.GodotDictionaryParser
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SaveTypeAttribute : Attribute
    {
        public string Id { get; }

        public SaveTypeAttribute(string id)
        {
            Id = id;
        }
    }
}
