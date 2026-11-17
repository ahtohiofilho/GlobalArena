# GA-Next — Gate 01B.2D-12: reutilização local da busca residual e equivalência exata

**17/11/2026 · PR Draft empilhado no #404 · alteração C# somente no fluxo global opt-in · sem merge nem Godot**

## Motivação

O PR #404 registrou, na execução real de 24 produtores, 288 mercados e 320 tiles, aproximadamente 1.306.404 KiB de alocações gerenciadas na thread do solver global e 1.470.192 KiB no multipath. A coordenação conjunta gerou cerca de 7.064.637 KiB de alocações **cumulativas**, não pico de RAM. Esses contadores são observações no runner de CI, e não limites de produção, nem perfis de heap retido.

A revisão dos caminhos confirmou que `FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate` **já possui cache de árvore de rota por tile de origem/civilização**. Não duplicar o cache nem desabilitar auditoria. O primeiro passo de menor risco é reduzir alocações temporárias no algoritmo de caminhos aumentantes do fluxo global residual, sem alterar a função côncava segmentada ou os resultados da distribuição.

## Mudança no algoritmo

`FoundationPhysicalConcaveCommodityFlow.Solve` agora pode reutilizar **somente durante a própria resolução**, entre aumentos de fluxo residual:

- Vetor de distâncias reduzidas (`BigInteger?[]`);
- Vetor de nós predecessores (`int[]`);
- Vetor de arestas predecessoras (`int[]`);
- Fila de prioridade do Dijkstra (`PriorityQueue<int,(BigInteger,int)>`).

Todos são **limpos antes de cada caminho aumentante**. Distâncias antigas, predecessores e prioridades de rotas anteriores não podem persistir. A rede residual, fluxos, potencial dual, sequência de atualizações, desempate, segmentos de mercado, frete, custos e auditoria **não mudam**. O workspace não é estático, compartilhado entre requests, civilizações, mercados, threads ou ciclos econômicos: cada chamada `Solve` cria seu próprio conjunto descartável de estruturas.

O caminho original de **novas alocações a cada aumento** continua acessível como modo de referência **somente interno** (argumento de validação `reuseSearchWorkspace=false`), não como novo controle de interface ou padrão do host. O `Coordinate` público preserva exatamente a assinatura e passa a usar `true`. `CoordinateWithSearchWorkspace` é ponto de teste interno para confrontar a mesma população física nos dois modos.

## Gate de equivalência

No mesmo caso adversarial de **8 produtores reais do mesmo grão, 104 mercados positivos e 112 tiles físicos**, são feitas comparações A/B nos níveis de segmentação 2, 4 e 8, verificando igualdade de:

- Identidade dos mercados, suas demandas e a quantidade alocada a cada comprador;
- Número de aumentos, potenciais segmentado e integrado e subestimação da aproximação;
- Entregas e rotas ordenadas, origens, destinos, civilizações, ocupações e microquantidades;
- Oferta total, produção alocada e soma auditada de carga por aresta;
- `StateHash` e snapshot imutáveis, além de `NoClosedCycle`.

Um segundo teste exige a mesma realocação de produtor através das **arestas residuais reversas** nos dois modos. Um terceiro confirma que orçamentos de fluxo insuficientes falham fechados nos dois, sem retornar entregas parciais. Testes antigos seguem ativos.

O teste registra tempo de relógio `Stopwatch` e `GC.GetAllocatedBytesForCurrentThread` para referência e modo novo, em cada resolução. **Não há assert sobre melhora de tempo ou bytes**: essas medições variam com o JIT, GC, thread e carga do runner. Só a **equivalência exata** é obrigatória; qualquer divergência invalida o incremento.

## Limites, riscos e governança

Esta é uma otimização de **alocações de buffers do Dijkstra residual**, não uma promessa de reduzir integralmente os gigabytes cumulativos observados no #404. Outras fontes relevantes permanecem: grafo físico, busca de caminhos por parcela multipath, custo quartico BigInteger e avaliação integrada de mercado. A sequência de benchmark mostrará se o efeito tem magnitude suficiente para continuar neste componente ou se devemos migrar o foco.

Não há alteração de oferta, demanda, satisfação, preço, função de custo, tarifa, titularidade, solver multipath, estabilidade do #401, dinheiro realizado, tesouro, journal, Godot ou `main`. Nenhum ciclo econômico é liquidado; `NoClosedCycle` permanece obrigatório. O PR é Draft empilhado sobre #404; o merge não deve ocorrer sem testes e evidência da CI.
