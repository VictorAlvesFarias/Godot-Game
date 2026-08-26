using System;

namespace Jogo25D.Utils.GodotDictionaryParser
{
    // Id estavel gravado no campo "$type" do save. Existe pra desacoplar o arquivo do nome
    // da classe: renomear, mover de pasta ou trocar de namespace nao pode quebrar mundo salvo.
    // Sem o atributo, cai no FullName do tipo - que ja resolve o problema de versao do assembly,
    // mas ainda amarra o save ao namespace.
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
