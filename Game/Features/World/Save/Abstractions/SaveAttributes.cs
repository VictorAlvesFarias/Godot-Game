using System;

namespace Jogo25D.Save
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class SaveSceneAttribute : Attribute
    {
        public string Type { get; }
        public string Scene { get; }

        public string Ref { get; init; } = "";

        public SaveSceneAttribute(string type, string scene)
        {
            Type = type;
            Scene = scene;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SaveAttribute : Attribute
    {
        public string Name { get; }

        public SaveAttribute(string name = "")
        {
            Name = name;
        }
    }
}
