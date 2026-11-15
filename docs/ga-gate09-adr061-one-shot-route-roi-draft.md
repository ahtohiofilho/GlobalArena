# Global Arena — ADR-061: uma única tentativa de otimização com ROI mensurável

**15/11/2026.** Base: PR #349 (ADR-060) `7712f852de630e89c1247235398efa6e60b41b46`, não mergeado. **Estado:** experimento reversível, sem produção.

## Mudança de prioridade e critério de parada

A sequência ADR-057→ADR-060 foi necessária para rejeitar o cache de rotas como causa principal da memória do solver. Prosseguir indefinidamente com microscopias de memória tem **ROI decrescente**. A partir desta ADR, fazemos **uma tentativa real** de redução de custo na própria execução comercial; se não demonstrar ganho, encerramos essa linha de profiling e voltamos à integração vertical/scheduler ou outra funcionalidade de jogo de alto ROI.

O ADR-056 localizou `certificate_r2.path_audit`, `gate09.weight_for`, `oracle_cache_r14.paths` e `gate09.congestion_cost` entre os maiores custos sob `cProfile`. O R13 `clearing_r5._reachable` e `_evaluate` repetem `path_audit` para grupos com a mesma rede e cargas: a rotina original calcula `edge.congestion_cost + float(edge.weight_for(seller))` **durante cada relaxação** e na segunda passagem de rotas, mesmo que esse peso seja constante dentro de um snapshot.

## Candidato de alto impacto, com reversibilidade rigorosa

Modificar **somente no escopo da chamada laboratorial** a ligação Python `clearing_r5.path_audit`, substituindo por uma rotina que:

1. Conserva **exatamente** a mesma Dijkstra em duas passagens, mesma soma de pontos flutuantes `d + w`, mesma tolerância ABS/REL, desempate por número de hops + sequência de IDs, ordem de adjacências, custos quárticos e tarifas originais.
2. Pré-computa os pesos de arestas **uma vez por vendedor e snapshot de cargas**, mantendo uma única tabela ativa para cargas não vazias; descarta a tabela quando muda a identidade do objeto `loads`. Para mapas vazios (análise topológica), compartilha pesos de carga zero entre diferentes grupos da mesma rede.
3. **Não altera** `certificate_r2.path_audit`, `oracle_cache_r14`, `R45`, `R47`, `R51`, nem `ADR42.static_certificate`; o patch temporário de R13 é restaurado inclusive após falha.
4. Depois que o solve termina e o patch foi restaurado, **reconstitui exatamente os fluxos finais físicos** do solver e executa de novo o certificado **ADR-042 original, independente e integralmente não modificado**. O resultado só pode PASS se tanto esse verificador original quanto o certificado emitido pelo solver coincidirem em gaps e se o hash final for o mesmo. A segunda execução original **não entra no cronômetro do solver** (é custo de homologação).

Assim, não fazemos o R13 reutilizar R14 ou confiar em uma auditoria acelerada para certificar a si mesma. Toda alteração é apenas de implementação de uma busca já definida e restaura a versão original.

## Escala e condição explícita de ROI

| Aspecto | Controle e candidato |
|---|---|
| Mundo | Goldberg 5.762 nós |
| Trabalhadores | 4.096 em ambos |
| Nós demandantes | 192 e 384 |
| Mercados | 384 e 768 |
| Regressões originais | 48/96/192/384 F072 intactas |
| Preço | `P=sqrt(D/Q)` |
| Custo | `c_free*(1+(B/K)^4)` e tarifas originais |
| Orçamentos | os originais ADR-054/055 sem ampliação |

Comparar `baseline` e `precompute` em processo Python isolado, no **mesmo HEAD e em Windows/Ubuntu**, exigindo igualdade **exata** de hash final, gaps originais, todos os work counters, gerações, ledger de trabalhadores e budgets. Divergência de economia = **falha**.

**Gate de desempenho predefinido:** redução de **ao menos 20% no tempo de CPU do solver completo para 384 nós em ambos os sistemas**, sem piora do pico RSS superior a 10%. Resultados intermediários podem ser apresentados, mas abaixo do gate a decisão será `NO_GO_ADR061_CPU_ROI_NOT_DEMONSTRATED` e **não haverá nova rodada de microprofiling para a mesma hipótese**. Uma rodada de CI é um filtro, não prova de ganho estatisticamente reproduzível nem benchmark C#/Godot. Mesmo passando, a integração exige repetição controlada e desenho de runtime; não há GO produção automático.

## Testes e trilha de auditoria

- Comparação de Dijkstra original e pré-computada para rotas e desempates de cada destino num mundo Goldberg real, tanto em carga zero quanto em carga física;
- testes negativos para snapshots diferentes, restauração do auditor e proibição de laudo sem hash correto;
- solves originais completos `192/384 × baseline/precompute × Windows/Ubuntu`, **cada candidato com verificador ADR42 original depois da restauração**;
- comparador com equivalência estrita da solução e ROI explícito;
- regressão F072 original não instrumentada de 48, 96, 192 e 384 nós em ambos SO.

Todos os arquivos são novos, no PR **Draft empilhado sobre #349**, sem merge. F059 financeira, scheduler cooperativo e integração C#/Godot continuam pendentes. 2.048 demandantes / 4.096 mercados continuam **somente ledger espacial**, não F072.

**Política de avanço:** se ≥20% em ambos sistemas, a única etapa posterior justificada é repetir o ganho com medidas pareadas/controle antes de levar a um port C#; se não, priorizar implementação vertical do ciclo de jogo em vez de continuar depurando cache e memória.
