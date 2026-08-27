using System;

namespace Jogo25D.Entities
{
    // Politica de descarregamento da entidade. Fica na CLASSE, nao na instancia: um portal e
    // um portal - nao faz sentido cada copia ter um valor diferente no inspetor.
    //
    // Herdado: Portal : Prop pega o do Prop sem repetir. Sem atributo, o padrao e Global.
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
