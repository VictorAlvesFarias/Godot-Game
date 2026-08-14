using System;

namespace Jogo25D.Instances
{
    public static class InstanceIdGenerator
    {
        public static long CurrentId { get; set; } = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & 0x7FFFFFFFFFFFFFFL;

        public static long NextInstanceId()
        {
            return ++CurrentId;
        }
    }
}
