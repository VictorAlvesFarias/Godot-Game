# Multiplayer - Guia de Uso

## Como Jogar em Multiplayer

### 1. Hospedar um Jogo (Servidor)
1. Inicie o jogo
2. Pressione **ESC** para abrir o menu de pausa
3. Na seção **MULTIPLAYER**, clique no botão **HOST**
4. O servidor será criado na porta padrão 7777
5. Outros jogadores podem se conectar ao seu IP

### 2. Conectar-se a um Jogo (Cliente)
1. Inicie o jogo
2. Pressione **ESC** para abrir o menu de pausa
3. Na seção **MULTIPLAYER**, digite o endereço do servidor no formato:
   - Local: `127.0.0.1:7777` ou apenas `127.0.0.1`
   - Rede: `192.168.1.X:7777` (substitua X pelo IP do host)
4. Clique no botão **CONNECT**
5. Aguarde a conexão ser estabelecida

### 3. Desconectar
- Se você for o **HOST**: clique em **STOP SERVER**
- Se você for o **CLIENTE**: clique em **DISCONNECT**

## Status de Conexão

O menu mostra o status atual:
- **Desconectado** (branco): Não conectado
- **SERVIDOR** (verde): Você está hospedando
- **CONECTADO** (verde): Você está conectado a um servidor

## Recursos Sincronizados

- ✅ Posição dos jogadores
- ✅ Movimentação (WASD)
- ✅ Pulos
- ✅ Dash
- ✅ Tiros (projéteis)
- ✅ Spawn dinâmico de jogadores

## Notas Técnicas

- Máximo de 4 jogadores por servidor
- Porta padrão: 7777
- O host precisa liberar a porta no firewall/roteador para jogo online
- Para jogar na mesma rede local, não é necessário port forwarding

## Controles

- **WASD**: Mover
- **Espaço**: Pular
- **Shift**: Dash
- **Mouse**: Mirar
- **Botão Esquerdo do Mouse**: Atirar
- **ESC**: Menu de pausa/Multiplayer

## Problemas Comuns

### "Falha ao conectar ao servidor"
- Verifique se o IP está correto
- Confirme que o servidor está rodando
- Verifique as configurações de firewall

### "Players não aparecem"
- Aguarde alguns segundos para sincronização
- Verifique a conexão de rede
- Tente reconectar

### Lag ou desincronização
- Verifique a latência da conexão
- O host deve ter uma conexão estável
- Reduza o número de jogadores se necessário
