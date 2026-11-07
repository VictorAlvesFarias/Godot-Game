using System;

namespace Jogo25D.Entities
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class UnloadAttribute : Attribute
    {
        public UnloadMode Mode { get; }

        public UnloadAttribute(UnloadMode mode)
        {
            Mode = mode;
        }
    }
}
