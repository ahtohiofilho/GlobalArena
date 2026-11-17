# GA-Next — contrato arquitetural: plataforma autoritativa e participantes-clientes

**16/11/2026 · decisão direcional de arquitetura, não implementação de servidor de rede · PR #378**

## 1. A orientação de produto

O Global Arena é uma **plataforma de mundo e turnos com autoridade central**.
Cada civilização é um participante do mundo, capaz de enviar intenções
de ação segundo suas permissões. O Godot é o primeiro **cliente
visual**, hoje conectado *em processo* ao motor C#.

A formulação "civilizações são clientes" é útil para desenhar a
arquitetura, mas **não devem ser o mesmo tipo de objeto**:
- **Civilização:** entidade de domínio com identidade estável,
  recursos, trabalhadores, titularidades e autorização de ordens.
- **Agente/participante:** controlador autorizado de uma civilização
  durante uma sessão (jogador humano, IA ou futuro controlador remoto).
- **Cliente:** processo/interface que apresenta dados e envia ordens
  *em nome* de um participante. No futuro pode haver espectadores,
  clientes reconectando, outro front-end ou ferramentas de operação.
- **Servidor/plataforma:** componente autoritativo que guarda sessão,
  estado, validação, ordem de eventos, liquidação e publicação do turno.

No cenário atual, um host local pode interpretar papéis de servidor
e cliente sem sockets. **Isso não constitui multiplayer de rede e não
deve ser anunciado como servidor implantado.**

## 2. Fronteira de responsabilidades

### Núcleo / plataforma (C#)
- Mantém `WorldState` canônico, identidade de civilizações e
  `PhysicalWorkerId`, tile físico G(18,0), atividade e ocupação v11.
- Verifica autoria, permissões, movimentos por aresta física,
  alterações de atividade, disponibilidade e uma intenção efetiva
  por indivíduo por turno.
- Coleta ordens humanas e políticas de IA, resolve o turno
  coletivamente, atribui revisão, registra journal e prova replay/hash.
- Resolve futuramente capacidades de infraestrutura, mercado
  de frete/comércio, receitas, tributação, eficiência e demais
  mudanças sistêmicas **sem delegar a decisão ao cliente**.
- Oferece projeções de leitura com escopo de civilização; no futuro
  respeitará o que pode ser visto pela civilização (fog-of-war,
  inteligência, diplomacia), sem entregar por padrão o estado oculto.

### Cliente Godot
- Desenha terreno, células, proprietários, pessoas e grupos, sem
  criar/teleportar pessoas e sem mudar estado de domínio por hover
  ou clique.
- Exibe contagens, tooltip/painel, UI do tile habitado e ordens
  **pendentes**, distinguidas dos resultados **confirmados**.
- Coleta intenção de mover/produzir/trocar ocupação/gerenciar,
  encaminha ao host e apresenta o retorno de validação/erro.
- Não define preço, resultado de batalha, receita, elegibilidade
  de movimento ou hash final. Todo isso vem do servidor autoritativo.
- É substituível sem alterar IDs/censo/worldgen/journal/replay.

### Agente de IA
- Participa do mesmo protocolo lógico de intenção e autorização;
  o sistema não favorece um executor por ser humano/IA.
- Planejamento da IA pode consultar projeções específicas de sua
  civilização, não internals de outra civilização fora das regras.

## 3. Protocolo lógico (ainda conceitual)

Uma futura porta de aplicação pode expor:

`ReadView(civilizationId, sinceRevision) → authorized projection`

`SubmitIntent(civilizationId, turnId, workerId, command, idempotencyKey)
    → accepted / rejected / replaced pending`

`SetReady(civilizationId, turnId) → readiness result`

`CommitCollectiveTurn(all-ready) → revision, stateHash, events, views`

Esse é um **contrato de desenho**, não endpoints ou interfaces
já disponibilizados neste PR. Os nomes, payloads e transporte serão
validados quando houver ganho real em separar o host local do
front-end. Futuras conexões precisarão de autenticação, isolamento
de visibilidade, reconexão, controle de duplicidade/idempotência,
compatibilidade de versões e proteção contra envios atrasados.

A **revisão + hash** identifica a publicação autoritativa; ordens
pendentes não podem se passar por estado liquidado. Replay deve
ser determinístico no servidor; o cliente pode renderizar
animações intermediárias, mas nunca promovê-las a acontecimentos
canônicos sem recebê-los no resultado aceito.

## 4. Proibição de acoplamentos prematuros

Não criar agora TCP/HTTP, persistência distribuída, lobby, bancos,
filas, microserviços ou login apenas para simular a palavra servidor.
Não fragmentar o journal por cliente. A arquitetura C#/Godot deve
evoluir por **seams verificáveis**, permitindo tanto single-player
local quanto serviços headless futuros.

O mapa físico deve escalar para **muitos milhares de tiles** e
eventualmente muitos trabalhadores. A plataforma responde por
escala, determinismo e orçamentos de CPU; o cliente por agregação,
níveis de detalhe, seleção e legibilidade. Nunca criar objetos
de UI por pessoa por frame em grande escala quando uma projeção
agregada sustentar a mesma decisão.

## 5. Contrato de aceite do futuro boundary

Quando o primeiro adaptador de cliente for criado:
1. Uma ordem via adaptador precisa produzir o **mesmo journal,
   revisão e hash** da chamada direta local na mesma sessão.
2. Nenhum cliente pode alterar um trabalhador de outra civilização,
   enviar IDs inválidos ou confirmar turno de participante alheio.
3. Repetição de mensagem não pode executar a mesma ordem duas vezes.
4. Uma sessão pode conter humanos e IAs sem bifurcar regras.
5. Fluxo completo precisa funcionar **sem processo de rede**
   para conservar testes determinísticos baratos e debuggáveis.

**Estado agora:** arquitetura documental aprovada como orientação;
interfaces reais de transporte/serviço ainda não foram implementadas.
