using System;

namespace Jogo25D.Instances
{
    public static class InstanceIdGenerator
    {
        private const long ID_MASK = 0x3FFFFFFFFFFFFL;

        public static long CurrentId { get; set; } = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & ID_MASK;

        public static long NextInstanceId()
        {
            return ++CurrentId;
        }
    }
}
