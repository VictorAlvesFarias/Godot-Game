using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Items;
using Jogo25D.Save;
using Jogo25D.Save.Resources;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Props
{
    [Unload(UnloadMode.Global)]
    public partial class Prop : Area2D
    {
        #region Dinamic properties

        [Save, GodotDictionaryField]
        public string PropId { get; set; } = "";

        // Jogador local dentro da area do prop (so ele ve o aviso e usa o E).
        public Player OverlappingLocalPlayer { get; set; }

        // Props interagiveis tratam o E; os outros (a mesa de criacao) deixam o E para a coleta.
        public virtual bool Interactable => false;

        public virtual string InteractPrompt => "";

        public PropDefinition Definition => PropDB.Get(PropId);

        // Celulas do mundo ocupadas pela forma do prop (ancora + Footprint), registradas em PropCells enquanto ele existe.
        public Vector2I[] OccupiedCells { get; private set; } = System.Array.Empty<Vector2I>();

        private string _occupiedDimension;

        #endregion

        #region Node children references

        public Label PromptLabel { get; set; }

        #endregion

        #region Godot implementation

        // Props na arvore: a mira da picareta procura o prop da celula aqui, sem varrer as entidades a cada passo.
        public static readonly System.Collections.Generic.HashSet<Prop> Active = new();

        public override void _EnterTree()
        {
            Active.Add(this);
        }

        public override void _ExitTree()
        {
            Active.Remove(this);
            ReleaseCells();
        }

        public override void _Ready()
        {
            PromptLabel = GetNodeOrNull<Label>("Labels/PromptLabel");

            // A forma em celulas (Footprint) e desenhada so para o editor.
            var footprint = GetNodeOrNull<TileMapLayer>(PropFootprints.NodeName);

            if (footprint != null)
            {
                footprint.Visible = false;
            }

            OccupyCells();

            if (PromptLabel != null)
            {
                PromptLabel.Visible = false;
            }

            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Interactable || OverlappingLocalPlayer?.Input == null || !OverlappingLocalPlayer.Input.Interact)
            {
                return;
            }

            Interact(OverlappingLocalPlayer);
        }

        #endregion

        #region Core - Celulas

        // O registro (PropId e posicao) ja foi aplicado antes de o prop entrar na arvore, tanto ao nascer quanto ao carregar.
        private void OccupyCells()
        {
            var dimensionId = Dimension.IdOf(this);
            var layer = Dimension.Get(dimensionId)?.Layer;

            if (layer == null)
            {
                return;
            }

            var anchor = layer.LocalToMap(layer.ToLocal(GlobalPosition));
            var offsets = PropFootprints.Read(this);
            var cells = new Vector2I[offsets.Length];

            for (var i = 0; i < offsets.Length; i++)
            {
                cells[i] = anchor + offsets[i];
            }

            OccupiedCells = cells;
            _occupiedDimension = dimensionId;
            PropCells.Occupy(this, dimensionId, cells);
        }

        private void ReleaseCells()
        {
            if (_occupiedDimension == null)
            {
                return;
            }

            PropCells.Release(this, _occupiedDimension, OccupiedCells);

            OccupiedCells = System.Array.Empty<Vector2I>();
            _occupiedDimension = null;
        }

        #endregion

        #region Core - Interacao

        // Chamado enquanto o jogador local, dentro da area, segura o E.
        protected virtual void Interact(Player player)
        {
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is not Player player || !player.IsOwner())
            {
                return;
            }

            OverlappingLocalPlayer = player;

            if (PromptLabel != null && Interactable)
            {
                PromptLabel.Text = InteractPrompt;
                PromptLabel.Visible = true;
            }
        }

        private void OnBodyExited(Node2D body)
        {
            if (body is not Player player || player != OverlappingLocalPlayer)
            {
                return;
            }

            if (PromptLabel != null)
            {
                PromptLabel.Visible = false;
            }

            OverlappingLocalPlayer = null;
        }

        #endregion

        #region Core - Quebra

        public void BreakClientRequest()
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                ProcessBreak();

                return;
            }

            RpcId(1, nameof(BreakServerReceive));
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void BreakServerReceive()
        {
            if (!Multiplayer.IsServer())
            {
                return;
            }

            ProcessBreak();
        }

        // Servidor: avisa os clientes, solta o item do prop no lugar dele e remove o prop.
        private void ProcessBreak()
        {
            Rpc(nameof(BreakBroadcast));
            OnBeforeBreak();
            DropItem();
            QueueFree();
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void BreakBroadcast()
        {
            OnBeforeBreak();
            QueueFree();
        }

        protected virtual void OnBeforeBreak()
        {
        }

        private void DropItem()
        {
            var itemId = Definition?.ItemId;

            if (string.IsNullOrEmpty(itemId) || ItemFactory.Create(itemId) == null)
            {
                return;
            }

            EntitySpawner.SpawnWorldItemRequest(ItemFactory.CreateInstance(itemId), GlobalPosition, Dimension.IdOf(this));
        }

        #endregion
    }
}
