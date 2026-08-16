# Registro de nodes estáticos e Bootstrap

Documentação do sistema implementado em `Features/World/Core/`. Cobre como os nós estáticos da árvore são registrados, como as classes os acessam, e como o jogo garante que só inicia depois que tudo carregou.

---

## O problema que isso resolve

Antes, cada classe procurava suas dependências sozinha:

```csharp
public WorldManager NetworkManager { get; set; }
public SaveManager Saves { get; set; }

public override void _Ready()
{
    NetworkManager = GetTree().Root.GetNodeOrNull<WorldManager>(StaticNodePathsConstants.WorldManager);
    Saves = GetTree().Root.GetNodeOrNull<SaveManager>(StaticNodePathsConstants.SaveManager);
}
// … e depois `Saves?.ListWorlds()` em todo uso
```

Eram **~175 lookups em 22 arquivos**, ~110 propriedades espelhando nós que já existiam, e ~40 `?.` defensivos. Funcionava por **sorte da ordem da árvore** (`Managers` vem antes de `Ui` no `Main.tscn`), não por contrato — e um caminho errado só aparecia como `NullReferenceException` aleatório, longe da causa.

> Prova disso: ao ligar a validação do Bootstrap, o caminho do `Minimap` estava errado desde sempre (`MarginContainer/MinimapPanel/Minimap`, faltando `TopRightColumn`). O `FullscreenMapUI.HudMinimap` era **sempre null** e ninguém percebia.

---

## Como funciona

### Two-phase init

`_Ready()` no Godot propaga **de baixo pra cima** — o root do `Main.tscn` é o último a ficar pronto. Ele é o composition root:

```
Main  (Bootstrap.cs)     ← _Ready roda POR ÚLTIMO, árvore inteira já existe
├── Managers/            ← _Ready dos managers roda antes
└── Ui/                  ← _Ready das telas roda antes
```

- **Fase 1 — `_Ready` de cada nó:** a classe só ajusta o próprio estado (`Layer`, `Visible`, `ProcessMode`) e registra o que quer fazer depois via `Game.WhenReady`. **Não declara nenhuma propriedade de nó** — nem de manager, nem de tela, nem dos próprios filhos.
- **Fase 2 — `Bootstrap._Ready`:** resolve todos os nós estáticos, valida, popula o registro, libera o jogo.

### O registro espelha a árvore

Acesso é sempre `Game.<NodeName>.Node` ou `Game.<NodeName>.<SubNodeName>.Node` — a forma do registro é a forma da cena:

```
Árvore                                   Acesso
──────────────────────────────────────   ──────────────────────────────────────────
/root/Main                               Game.Main.Node
      ├── Managers                       Game.Managers.Node
      │     ├── ScreenManager            Game.Managers.ScreenManager.Node
      │     ├── WorldManager             Game.Managers.WorldManager.Node
      │     ├── ChunkStreamingManager    Game.Managers.ChunkStreamingManager.Node
      │     └── SaveManager              Game.Managers.SaveManager.Node
      └── Ui                             Game.Ui.Node
            ├── StartUI                  Game.Ui.StartUI.Node
            ├── LoadingUI                Game.Ui.LoadingUI.Node
            ├── …                        …
            └── HudUI                    Game.Ui.HudUI.Node
                  └── …/Minimap          Game.Ui.HudUI.Minimap.Node
```

Tudo vive em **um arquivo só** (`Game.cs`), como classes estáticas aninhadas — mesma ideia do `Textures.cs`. Cada entrada é uma classe estática com um `Node` tipado de `internal set` (só o `Bootstrap` escreve) e o caminho como comentário:

```csharp
public static class Game
{
    public static class Managers
    {
        public const string Path = "/root/Main/Managers";

        public static Godot.Node Node { get; internal set; }

        public static class WorldManager
        {
            public const string Path = "/root/Main/Managers/WorldManager";

            public static global::Jogo25D.Systems.WorldManager Node { get; internal set; }
        }
    }

    public static class Ui
    {
        public static class StartUI
        {
            public const string Path = "/root/Main/Ui/StartUI";

            public static global::Jogo25D.UI.StartUI Node { get; internal set; }

            public static class PlayButton
            {
                public const string Path = "/root/Main/Ui/StartUI/MarginContainer/Root/MenuColumn/PlayButton";

                public static Godot.Button Node { get; internal set; }
            }
        }
    }
}
```

**Contrato de cada entrada: um `Path` e um `Node`.** É só isso que o `Bootstrap` precisa — ele não tem lista de nós, ele varre.

O nome da classe aninhada colide de propósito com o tipo do nó (`Managers.WorldManager` × o tipo `WorldManager`), por isso os tipos são escritos com `global::` — assim a entrada tem o mesmo nome do nó na árvore.

> Classe estática **não pode herdar** em C# (é implicitamente `sealed`), então não dá pra ter um `NodeRegistry` base que as entradas estendem. A varredura por reflexão resolve o mesmo problema sem precisar de herança.

### O portão: o jogo só inicia se o registro fechar

```csharp
public override void _Ready()
{
    Game.Reset();

    RegisterManagers();
    RegisterUi();

    if (_missingNodes.Count > 0)
    {
        GD.PushError($"[Bootstrap._Ready] {_missingNodes.Count} node(s) estatico(s) ausente(s), o jogo nao vai iniciar:\n - {string.Join("\n - ", _missingNodes)}");

        return;   // Game.IsReady continua false
    }

    GD.Print($"[Bootstrap._Ready] {_registeredCount} nodes estaticos registrados, jogo pronto");

    Game.NotifyReady();

    OpenStartScreen();   // StartUI.Visible = true
}
```

A `StartUI` nasce com `Visible = false` (no próprio `_Ready`) e **só o Bootstrap a abre**. Se qualquer nó faltar: `IsReady` fica `false`, nenhum callback de `WhenReady` dispara, a tela inicial nunca aparece, e o erro sai listando exatamente o que faltou.

### O gancho `Game.WhenReady`

O `_Ready` das telas roda **antes** do `Bootstrap._Ready`, então nenhuma classe pode tocar no registro dentro do próprio `_Ready`. Em vez de null-check, a classe declara *quando* quer agir:

```csharp
public static void WhenReady(Action action)
{
    if (IsReady)
    {
        action();      // Bootstrap já fechou → roda agora

        return;
    }

    ReadyCallbacks += action;   // ainda não → enfileira
}
```

Uso:

```csharp
public override void _Ready()
{
    Layer = 20;
    Visible = false;

    Game.WhenReady(Initialize);
}

private void Initialize()
{
    Game.Ui.MultiplayerUI.ConnectButton.Node.Pressed += OnConnectPressed;
    Game.Ui.MultiplayerUI.BackButton.Node.Pressed += OnBackPressed;

    Game.Managers.WorldManager.Node.ServerCharacterListAvailable += OnServerCharacterListAvailable;
}
```

O `_Ready` fica só com o estado próprio; **tudo que toca o registro — inclusive fiar sinal de botão filho — vai pro `Initialize`**.

Funciona pros dois casos sem a classe saber em qual está:
- **nó estático** (`MultiplayerUI`, `ConsoleUI`): Bootstrap ainda não fechou → enfileira
- **nó instanciado em runtime** (`Player`, `Portal`, `CameraController`): Bootstrap já fechou → roda na hora

Fora do `_Ready` — em handler de botão, `_Process`, RPC — **não precisa de `WhenReady`**: nesse momento o jogo já está rodando, então o acesso é direto (`Game.Managers.SaveManager.Node.ListWorlds()`).

---

## O que entra no registro (e o que não entra)

**Entra:** todo nó que existe desde o boot e nunca é reinstanciado — managers, telas, e **os filhos delas** (botões, inputs, containers, labels) e os **templates** (`WorldRowTemplate`, `AbilityTemplate`, …). O template é um nó estático da cena; ele entra.

**Não entra:** nó criado em runtime. Principalmente os **clones** de template (`template.Duplicate()`) — as linhas de lista, os slots de hotbar, os cards de habilidade. Esses não existem no boot, então continuam com `GetNode` local sobre o clone:

```csharp
var template = Game.Ui.WorldSelectUI.WorldRowTemplate.Node;   // estático → registro
var row = (Button)template.Duplicate();                        // clone → não registrável
var selectButton = row.GetNode<Button>("SelectButton");        // filho do clone → GetNode local
```

Também não entram os nós das cenas instanciadas em runtime (`World.tscn`, `Player.tscn`, `Portal.tscn`) — não existem quando o `Bootstrap` roda.

Medição no projeto: **113 nós estáticos registrados** e **~32 acessos a clone** que permanecem como `GetNode` local (7 classes usam `Duplicate()`).

---

## Como registrar um nó novo

**1 passo:** declarar a classe aninhada dentro do pai, em `Game.cs`. O `Bootstrap` acha sozinho.

```csharp
public static class PlayButton
{
    public const string Path = "/root/Main/Ui/StartUI/MarginContainer/Root/MenuColumn/PlayButton";

    public static Godot.Button Node { get; internal set; }
}
```

O caminho de acesso **achata os containers de layout**: é `Game.Ui.StartUI.PlayButton.Node`, não `Game.Ui.StartUI.MarginContainer.Root.MenuColumn.PlayButton.Node`. Os containers intermediários só aparecem no `Path`.

---

## Arquivos

```
Features/World/Core/Singletons/
  Game.cs        registro inteiro: 113 entradas (Path + Node), IsReady, WhenReady

Features/World/Core/Entities/
  Bootstrap.cs   script do root do Main.tscn; varre o Game, valida, libera
```

Dois arquivos, e o `Bootstrap` **não tem lista de nós** — quem sabe o caminho é a própria entrada.

---

## Resultado

| | Antes | Depois |
|---|---:|---:|
| Lookups na árvore | ~175 (22 arquivos) | 113 (só no `Bootstrap`) |
| Propriedades de nó nas classes | ~110 | 0 |
| `?.` defensivos em manager/tela | ~40 | 0 |
| Momento definido de "jogo pronto" | não existia | `Game.IsReady` / `Game.WhenReady` |
| Caminho errado | `NullReferenceException` aleatório | erro no boot listando o que faltou |

Validado com `dotnet build --no-incremental` limpo (0 erros) e execução headless: `[Bootstrap._Ready] 113 nodes estaticos registrados, jogo pronto`.
