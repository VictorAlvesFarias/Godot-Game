using System;

namespace Jogo25D.Instances
{
    public static class InstanceIdGenerator
    {
        // 2^50 - 1. NAO aumentar: o save e JSON, e JSON so tem double. Inteiro acima de 2^53
        // volta da leitura com valor diferente - medido: 165877808694513759 virou ...760, o que
        // faz o no perder a identidade no reload. A mascara antiga era 59 bits e quebrava isso.
        private const long ID_MASK = 0x3FFFFFFFFFFFFL;

        public static long CurrentId { get; set; } = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & ID_MASK;

        public static long NextInstanceId()
        {
            return ++CurrentId;
        }
    }
}
