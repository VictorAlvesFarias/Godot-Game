namespace Jogo25D.Save
{
    public interface ISaveState
    {
        void WriteState(Godot.Collections.Dictionary state);

        void ReadState(Godot.Collections.Dictionary state);
    }
}
