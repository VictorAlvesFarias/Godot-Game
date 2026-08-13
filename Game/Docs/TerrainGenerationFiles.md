# Arquivos envolvidos na geração procedural do terreno

Este documento lista os arquivos do projeto que participam direta ou indiretamente na geração procedural do terreno, pintura de tiles e colocação de estruturas (árvores, props). Para cada arquivo há uma breve descrição do papel que desempenha.

- **Game/Features/World/Chunks/Systems/ChunkGenerator.cs**: Núcleo do gerador de chunks. Resolve superfícies de coluna (altura), agrupa células por bioma, pinta as layers (`Connect`/`ReconnectForeignBorder`) e chama `PlaceStructures` para instanciar estruturas procedurais.

- **Game/Features/World/Chunks/Systems/ChunkStreamingManager.cs**: Gerencia carregamento e descarregamento de chunks em tempo de execução; invoca `PaintAsync`/`EraseAsync` para carregar chunks sem travar o jogo.

- **Game/Constants/ChunkStreamingConstants.cs**: Constantes relacionadas às names das layers procedurais (`PROCEDURAL_BASE_LAYER_NAME`, `PROCEDURAL_LAYER_NAME`) e outros parâmetros usados pelo streaming/geração.

- **Game/Features/World/Chunks/View/ChunkGridOverlay.cs**: Ferramenta de visualização/overlay para debugging dos chunks (não altera a geração, mas ajuda a inspecionar resultados).

- **Game/Features/World/Chunks/Resources/ChunkStateData.cs** e **ChunkMutationData.cs**: Estruturas que representam estado persistente/alterações em chunks, usadas pelo sistema de streaming/salvamento.

- **Game/Features/World/Structures/Abstractions/StructureDefinition.cs**: Classe base que descreve comportamento comum de estruturas (bounds, coleta de células, etc.).

- **Game/Features/World/Structures/Definitions/TreeStructureDefinition.cs**: Implementação concreta para árvores (geração de forma, seleção de tiles, opções de debug/preview).

- **Game/Features/World/Structures/Database/StructureDB.cs**: Registro/lookup de `StructureDefinition`s disponíveis por id; usado durante `PlaceStructures`.

- **Game/Features/World/Biomes/Singletons/BiomeResolver.cs**: Resolve o bioma de uma posição (usado por `ResolveSolidCellsByBiome`).

- **Game/Features/World/Biomes/Database/BiomeDB.cs** e **BiomeDefinition.cs**: Banco de dados e definição de biomas (alturas, noise frequency, terrain sets, lista de structures permitidas por bioma).

- **Game/Features/World/Biomes/Structures/TerrainConnectionRule.cs**: Regras para conectar tiles entre biomas/terrain sets.

- **Game/Features/World/Biomes/Singletons/TerrainLayer.cs**: Implementa a camada de tile/tileset com métodos `Connect`/`ReconnectForeignBorder` e variantes assíncronas usadas pelo `ChunkGenerator`.

- **Game/Features/World/Levels/Entities/UpsidedownLevel.cs**: Classe de nível/editor que manipula backups de tilemaps e gera terrenos para preview; contém lógica de serialização usada nas cenas de nível.

- **Game/Features/World/Chunks/Systems/WorldRandom.cs** (ou equivalente `WorldRandom.*`): Utilitários de geração de números pseudo-aleatórios estáveis por mundo/dimensão (usado para decidir `Structure` placement e spacing).

- **Game/Assets/Textures/Tiles/world_biomes_tileset.tres**: TileSet usado pelas camadas procedurais para pintar as tiles corretas por `TerrainSet`.

Observações:
- A geração depende de uma interação entre resolver colunas (altura), pintar tiles e colocar estruturas. Mudanças em semente/ruído, ou em como `Connect` trata bordas, afetam continuidade entre chunks.
- Algumas classes podem ter nomes ligeiramente diferentes no projeto (por exemplo utilitários de random). Se precisar, eu faço uma listagem exata de todas as referências por símbolo (ex.: onde `PlaceStructures` é chamado) e gero links para cada arquivo.

---
Gerado automaticamente por assistência de desenvolvimento. Se quiser, eu:
- adiciono exemplos de fluxo (sequência de chamadas) com linhas de código; ou
- removo comentários nesses arquivos primeiro, conforme solicitado — confirme se deseja remover comentários de todo o repositório ou apenas dos arquivos listados acima.