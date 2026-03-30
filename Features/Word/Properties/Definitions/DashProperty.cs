using Jogo25D.Items;

namespace Jogo25D.Properties
{
    public class DashProperty : BaseProperty
    {
        public float DashSpeed { get; init; } = 800f;
        public float MovementInfluence { get; init; } = 0.4f;
    }
}