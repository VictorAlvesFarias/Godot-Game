# Performance da iluminação

A apresentação ativa usa `ComputeTextureAsync`: prepara a ocupação no thread principal, agenda a propagação no thread de renderização e apresenta o resultado por `Texture2Drd`. Os buffers de trabalho são reutilizados. A saída de cada região é copiada somente dentro da GPU para que outro job não a sobrescreva. Não há `Sync`, `TextureGetData` nem upload do resultado no caminho normal. Sem RenderingDevice, permanece o fallback CPU.

O caminho síncrono `Compute` foi mantido para diagnóstico e comparação numérica. Seu benchmark inclui readback e não representa o caminho de apresentação.

No editor, alterações de camadas/recursos/configuração invalidam a prévia. A comparação compartilhada dos dados compactados de tiles cobre edições diretas que não emitem `Changed`, sem consultar cada célula individualmente. A máscara é reaproveitada quando apenas a configuração da luz muda. O campo de céu é calculado sincronicamente na CPU quando sua chave muda. A geometria analítica é construída em fatias, e os parâmetros dos materiais são atualizados apenas quando muda a chave dos bindings ou surge um overlay.

A máscara guarda os pixels dos atlas e o alcance dos tiles em caches fracos, invalidados por alterações dos recursos. Tiles com translação inteira e sem escala/rotação usam cópia direta de alpha; os demais mantêm o rasterizador original. As regras de transparência não mudam.

## Medição

Medição local em 12 chunks, máscaras de 512×512: aproximadamente 125 ms/chunk antes e 13–15 ms/chunk depois para a reconstrução da máscara. Isto é o tempo dessa etapa, não um ganho de FPS global. A preparação de dados e as máscaras ainda usam CPU; a propagação, sombra analítica e composição visual usam GPU.

A prévia recria o dispatcher ao reentrar na árvore do editor. Resultados assíncronos de uma sessão anterior, ou de uma versão anterior dos tiles, são descartados e reagendados. A validação do editor inclui pintar/apagar Base e Compose, comparar os pixels da luz e editar após remover/reinserir o nó de prévia.
