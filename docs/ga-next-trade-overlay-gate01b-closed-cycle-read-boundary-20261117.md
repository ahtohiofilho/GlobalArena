# GA-Next — TRADE-OVERLAY-001 / Gate 01B.1: fronteira de leitura do ciclo econômico fechado

**17/11/2026 · implementação incremental sobre PR #386 · Draft, sem merge · não representa mercado v11 liquidado**

## Situação fundamentada no código

- O host `FoundationTacticalHumanAiTurnHost` fecha turnos coletivos de **trabalhadores** (v10/v11): é capaz de produzir `FoundationTacticalCollectiveReceipt`, revisão, journal e hash, mas o motor é apenas `PhysicalTacticalWorkerMovementTurnPhase`. **Não liquida comércio.**
- A economia de referência `PhysicalWorkerTradeCycle` tem cargas `FoundationTurn.EdgeLoads` e IDs de aresta locais; o próprio código a identifica como protótipo separado do `WorldState`, sem liquidação financeira nem certificação de equilíbrio. **Não fornece uma publicação autoritativa ao host jogável.**
- O PR #386 implementou `FoundationPhysicalTradeEdgeLoadAuditor` para cargas em arestas da malha fina real, recebendo **itinerários, unidades de carga e titularidade** de fora. Auditar não comprova a proveniência de uma entrega.

**Consequência:** no estágio atual o resultado visual correto é **“No completed economic cycle yet”**, inclusive depois de confirmar diversos turnos `E`. **Não mostrar uma camada com linhas ou volume zero atribuído a ciclos inexistentes.**

## Entrega deste corte

1. `FoundationTacticalHumanAiTurnHost.GetLastClosedEconomicTradeView(CivilizationId)`: porta de leitura no host já usado pelo Godot. Como nenhuma fase de mercado está conectada ao fechamento do turno, devolve **`NoClosedCycle`**, lista vazia, ciclo, revisão, hash e unidade logística ausentes. A chamada não altera snapshot, jornal, identidade do trabalhador, plano pendente ou câmera. Rejeita civilizações inexistentes, não confunde a função de humano com autorização de leitura (a IA também é participante).
2. `FoundationEconomicTradeCycleReadBoundary`: arquivo histórico em memória vinculado a **`RuntimeWorldBinding` + `GoldbergParameters` fino** e conjunto explícito de participantes. O tipo é interno ao `GlobalArena.Simulation`. Não existe `Publish` público a clientes ou plugins.
3. `FoundationEconomicTradeOverlayAvailability` distingue `NoClosedCycle`, `Ready` e `AuthorizedSubset`. Um ciclo fechado **sem transporte** deverá ser `Ready` com `UsedEdges=[]`, não `NoClosedCycle`. O caso `AuthorizedSubset` significa **parte autorizada** da fotografia real do ciclo, sem revelar lista de corredores que não pertencem à visão da civilização.
4. `FoundationSettledEconomicTradeCycleArtifact` e `PublishFromSettledEconomicPhase` são **internos** e **não chamados pelo host atual**: marcam o ponto de integração reservado à futura etapa econômica real. O teste de integração usa fixtures construídas por assembly amiga exclusivamente para validar o boundary. Uma fixture **não** é um recibo financeiro ou uma simulação de produção sendo executada no produto.
5. Quando for usado pelo publisher real, o boundary verifica antes de substituir a fotografia: identidade mundial imutável, mesma instância de snapshot confirmado, hash canônico do estado v11, turno já confirmado, ordinal econômico estritamente crescente, topologia fina consistente e lista **explícita** de arestas autorizadas **para cada civilização participante**. Aresta desconhecida, repetida, inexistente ou omitida na política causa falha sem substituição parcial.
6. O snapshot histórico mantém **seu próprio hash, número de ciclo, revisão, turno e titularidade naquele ciclo** quando turnos de trabalhadores posteriores acontecem. Uma civilização sem acesso a uma aresta não recebe volume, titular, decomposição por mercadoria nem a própria identidade da aresta escondida; o mecanismo não presume que todas as civs vejam tudo.
7. Identificadores, volumes e cores continuam **dados autoritativos** de origem externa; o cliente não define estradas, rotas nem compradores. A presente implementação não instala nenhum `T` como atalho nem altera o mapa.

## Gates/testes

`FoundationLastClosedEconomicTradePublicationTests` passa a confirmar:

- Ausência explícita antes e depois de confirmações `E` e de mudanças de ocupação pendentes; jogo e hash intocados pela consulta.
- Rejeição de leitor não participante e de snapshot de outro mundo.
- No harness interno de publicação: saída `Ready` para participante com acesso, `AuthorizedSubset` para participante sem acesso; sem vazamento de arestas proibidas, mesmo quando o dicionário de política externo é alterado depois.
- O ciclo de transporte zero **realmente publicado no harness** é distinto da ausência de ciclo.
- Snapshot antigo com identidade ou hash incorretos, ordem de ciclos inválida, turno futuro, topologia estrangeira, duplicação, audiência incompleta e aresta não usada são rejeitados sem destruir o último ciclo válido.
- Cores/titulares históricos não são reescritos quando uma publicação posterior ou um turno comum acontece.
- Regressão do Godot Mono 4.7.2 e testes C# Windows/Ubuntu, hashes e mercado previamente preservados.

O `InternalsVisibleTo("GlobalArena.Tests")` existe **somente para o test harness** poder provar os caminhos internos de publicação sem criar uma API pública para fabricar ciclos econômicos.

## Limites explícitos e próximo avanço

Esta entrega é **Gate 01B.1 — camada de leitura e recusa segura**, não o Gate 01B completo:

- **Ainda faltam** mercado físico autoritativo v11, determinação de rotas efetivas no ciclo, catálogo real de produtos, conversão para carga comum, infraestrutura e sua titularidade, política de visibilidade real (fog-of-war), armazenamento/replay de economic-cycle settlements e o ponto onde o motor irá gerar `FoundationSettledEconomicTradeCycleArtifact` de **resultados finais** em vez de fixtures.
- Até a existência desse publisher, `GetLastClosedEconomicTradeView` deve retornar **`NoClosedCycle` em todas as execuções reais**. O read model não afirma a venda integral, o cálculo de preços ou o equilíbrio do mercado.
- O arquivo em memória preparado não substitui persistência/journal econômico. Se não houver mecanismo de reidratação histórica, uma reconexão de cliente/servidor ainda não terá histórico; isso será tratado na integração da sessão autoritativa.

**Próximo Gate 01B.2:** conectar a liquidação econômica real ao auditor e emitir evidência de titularidade, unidade logística e itinerário **após** fechamento coletivo do ciclo; então publicar artifact com visibilidade autoritativa no boundary. Depois disso o Gate 02 pode normalizar largura e cores e o Gate 03 pode desenhar o overlay em Godot.

**Governança:** PR Draft empilhado sobre #386, sem merge e sem alterar a interface provisoriamente homologada.
