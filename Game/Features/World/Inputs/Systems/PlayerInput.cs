using Godot;
using Jogo25D.Characters;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
    public partial class PlayerInput : Node
    {
        #region Dinamic properties

        public HashSet<string> Blockers { get; set; } = new();
        public float MoveX { get; private set; }
        public float MoveY { get; private set; }
        public bool Jump { get; private set; }
        public bool Dash { get; private set; }
        public bool Attack { get; private set; }
        public bool Reload { get; private set; }
        public bool Interact { get; private set; }
        public bool Ability { get; private set; }
        public bool PrevAbility2Held { get; private set; }
        public bool Ability2Held { get; private set; }
        public bool Ability2JustReleased { get; private set; }
        public bool ScrollNext { get; private set; }
        public bool ScrollPrev { get; private set; }
        public int ScrollDirection => ScrollNext ? 1 : (ScrollPrev ? -1 : 0);
        public bool Pause { get; private set; }
        public bool ToggleInventory { get; private set; }
        public bool DropItem { get; private set; }
        public Vector2 MousePosition { get; private set; }
        public bool RestrictMiningToAccessible { get; private set; }

        #endregion

        #region Node references

        public Player PlayerRef { get; set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            PlayerRef = GetParent().GetParentOrNull<Player>();
        }

        public override void _PhysicsProcess(double delta)
        {
            Poll();
        }

        #endregion

        #region Core - Input

        public bool IsBlocked()
        {
            return Blockers.Count > 0;
        }

        public bool IsBlockedByOther(string excludeId)
        {
            return Blockers.Any(id => id != excludeId);
        }

        public void Poll()
        {
            if (PlayerRef == null)
            {
                Clear();

                return;
            }

            if (!PlayerRef.IsOwner())
            {
                return;
            }

            if (IsBlocked())
            {
                Clear();

                return;
            }
            var moveX = Input.GetAxis("move_left", "move_right");
            var moveY = Input.GetAxis("move_up", "move_down");
            var jump = Input.IsActionJustPressed("move_up");
            var dash = Input.IsActionJustPressed("dash");
            var attack = Input.IsActionPressed("shoot");
            var reload = Input.IsActionJustPressed("reload");
            var interact = Input.IsActionJustPressed("interact");
            var ability = Input.IsActionJustPressed("ability");
            var ability2Held = Input.IsActionPressed("ability_2");
            var ability2JustReleased = PrevAbility2Held && !ability2Held;
            var scrollNext = Input.IsActionJustPressed("weapon_next");
            var varScrollPrev = Input.IsActionJustPressed("weapon_prev");
            var pause = Input.IsActionJustPressed("pause");
            var toggleInventory = Input.IsActionJustPressed("toggle_inventory");
            var dropItem = Input.IsActionJustPressed("drop_item");
            var mousePosition = PlayerRef.GetGlobalMousePosition();

            if (Input.IsActionJustPressed("toggle_mining_mode"))
            {
                RestrictMiningToAccessible = !RestrictMiningToAccessible;
            }

            if (MoveX != moveX)
            {
                Rpc(nameof(ServerSetMoveX), moveX);
            }

            if (MoveY != moveY)
            {
                Rpc(nameof(ServerSetMoveY), moveY);
            }

            if (Jump != jump)
            {
                Rpc(nameof(ServerSetJump), jump);
            }

            if (Dash != dash)
            {
                Rpc(nameof(ServerSetDash), dash);
            }

            if (Attack != attack)
            {
                Rpc(nameof(ServerSetAttack), attack);
            }

            if (Reload != reload)
            {
                Rpc(nameof(ServerSetReload), reload);
            }

            if (Interact != interact)
            {
                Rpc(nameof(ServerSetInteract), interact);
            }

            if (Ability != ability)
            {
                Rpc(nameof(ServerSetAbility), ability);
            }

            if (Ability2Held != ability2Held)
            {
                Rpc(nameof(ServerSetAbility2Held), ability2Held);
            }

            if (Ability2JustReleased != ability2JustReleased)
            {
                Rpc(nameof(ServerSetAbility2Released), ability2JustReleased);
            }

            if (ScrollNext != scrollNext)
            {
                Rpc(nameof(ServerSetScrollNext), scrollNext);
            }

            if (ScrollPrev != varScrollPrev)
            {
                Rpc(nameof(ServerSetScrollPrev), varScrollPrev);
            }

            if (Pause != pause)
            {
                Rpc(nameof(ServerSetPause), pause);
            }

            if (ToggleInventory != toggleInventory)
            {
                Rpc(nameof(ServerSetToggleInventory), toggleInventory);
            }

            if (DropItem != dropItem)
            {
                Rpc(nameof(ServerSetDropItem), dropItem);
            }

            if (MousePosition != mousePosition)
            {
                Rpc(nameof(ServerSetMousePosition), mousePosition);
            }

            PrevAbility2Held = ability2Held;
        }

        public void Clear()
        {
            MoveX = 0;
            MoveY = 0;
            Jump = false;
            Dash = false;
            Attack = false;
            Reload = false;
            Interact = false;
            Ability = false;
            Ability2Held = false;
            Ability2JustReleased = false;
            ScrollNext = false;
            ScrollPrev = false;
            Pause = false;
            ToggleInventory = false;
            DropItem = false;
            MousePosition = Vector2.Zero;
        }

        public void AddBlocker(string id)
        {
            Blockers.Add(id);
        }

        public void RemoveBlocker(string id)
        {
            Blockers.Remove(id);
        }

        #endregion

        #region Core - Rpc - Input

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetMoveX(float value)
        {
            MoveX = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetMoveY(float value)
        {
            MoveY = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetJump(bool value)
        {
            Jump = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetDash(bool value)
        {
            Dash = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAttack(bool value)
        {
            Attack = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetReload(bool value)
        {
            Reload = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetInteract(bool value)
        {
            Interact = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAbility(bool value)
        {
            Ability = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAbility2Held(bool value)
        {
            Ability2Held = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAbility2Released(bool value)
        {
            Ability2JustReleased = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetScrollNext(bool value)
        {
            ScrollNext = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetScrollPrev(bool value)
        {
            ScrollPrev = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetPause(bool value)
        {
            Pause = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetToggleInventory(bool value)
        {
            ToggleInventory = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetDropItem(bool value)
        {
            DropItem = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetMousePosition(Vector2 value)
        {
            MousePosition = value;
        }

        #endregion
    }
}