# Jogo 2.5D - Godot (C#)

Um jogo de plataforma 2.5D criado em Godot com C# e estilo minimalista.

## Características

- **Player**: Quadrado com bordas brancas e fundo transparente
- **Plataformas**: Retângulos com bordas brancas e fundo transparente
- **Fundo**: Preto
- **Controles**:
  - **W**: Pular
  - **A**: Mover para esquerda
  - **S**: Não utilizado (reservado)
  - **D**: Mover para direita
  - **Mouse Esquerdo**: Atirar na direção do cursor

## Sistema de Tiro

- Os tiros são disparados do player em direção à posição do mouse
- Taxa de disparo limitada para evitar spam
- Projéteis desaparecem após 3 segundos ou ao colidir

## Requisitos

- Godot Engine 4.6 ou superior (versão Mono/.NET)
- .NET 8.0 SDK ou superior

## Como Executar

1. Abra o Godot Engine Mono (versão 4.6 ou superior)
2. Clique em "Importar" e selecione a pasta do projeto
3. Abra o projeto
4. O Godot irá construir automaticamente o projeto C# na primeira vez
5. Pressione F5 ou clique no botão "Play" para executar

## Estrutura do Projeto

```
TestingGameGodot/
├── Scripts/           # Scripts C#
│   ├── Player.cs
│   └── Bullet.cs
├── Scenes/            # Cenas do jogo
│   ├── Main.tscn
│   ├── Player.tscn
│   ├── Bullet.tscn
│   └── Platform.tscn
├── project.godot      # Configuração do projeto
├── Jogo25D.csproj     # Projeto C#
└── Jogo25D.sln        # Solução Visual Studio
```

## Personalização

Você pode ajustar as seguintes variáveis nos scripts (usando o atributo [Export]):

### Player.cs
- `Speed`: Velocidade de movimento horizontal (padrão: 300)
- `JumpVelocity`: Força do pulo (padrão: -600)
- `FireRate`: Taxa de disparo em segundos (padrão: 0.2)

### Configurações de Física (project.godot)
- `2d/default_gravity`: Gravidade do jogo (padrão: 1500)

### Bullet.cs
- `Speed`: Velocidade dos projéteis (padrão: 600)
- `Lifetime`: Tempo de vida dos projéteis em segundos (padrão: 3.0)

## Adicionar Mais Plataformas

1. Abra `Scenes/Main.tscn` no editor do Godot
2. Clique com botão direito em "Platforms" na árvore de cenas
3. Selecione "Instantiate Child Scene"
4. Escolha `Scenes/Platform.tscn`
5. Posicione a plataforma onde desejar

## Desenvolvimento

Para recompilar o projeto C# após fazer alterações:

```powershell
dotnet build
```

Ou pressione F6 dentro do Godot para compilar automaticamente.
