namespace Jogo25D.Entities
{
    // Como uma entidade se comporta quando nao ha player perto. Declarado no Register, nao
    // por metodo virtual: a entidade diz o que ela e, e o manager aplica.
    public enum UnloadMode
    {
        // Nunca descarrega. Boss de arena, estrutura de quest, o que nao pode sumir.
        Never,

        // Ninguem tem, nem o servidor. A simulacao para. E o caso comum: prop, item largado.
        Global,

        // O servidor mantem e continua simulando; o peer longe perde o no. Pra coisa que
        // precisa rodar sem ninguem olhando - maquina que produz, plantacao que cresce.
        PeerOnly,
    }
}
