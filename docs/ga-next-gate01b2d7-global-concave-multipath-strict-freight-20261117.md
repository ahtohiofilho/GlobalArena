# GA-Next — Gate 01B.2D-7: distribuição global côncava + multipath com aceitação por custo integrado

**17/11/2026 · novo incremento C# opt-in · PR Draft sobre #399 · sem merge · Godot/StateHash/main intocados**

## Motivação e posição matemática

No #398, os trabalhadores produtores da mesma mercadoria são alocados **simultaneamente** a mercados reais por fluxo de custo mínimo com benefício de mercado côncavo segmentado. No #399, o recálculo quartico das taxas de frete em ciclos completos apresentou um limite real: **oito produtores da mesma mercadoria e 104 mercados Goldberg oscilam entre vetores de custo**, e nenhuma distribuição instável pode ser divulgada como ciclo fechado.

Este gate estabelece um artefato intermediário que não dependa de fingir convergência: depois de fixar as quantidades e compradores da solução global, testar **divisão física da mesma entrega entre trajetos alternativos** e aceitar os trajetos somente quando **reduzam estritamente** o objetivo integrado de custo da rede, comparado com os caminhos únicos originais. Esta operação atua **exclusivamente sobre rotas**, preservando o plano global de mercados intacto. Não é um novo algoritmo de preços, nem uma solução conjunta ótima de congestionamento e demanda.

## Contrato

API: `FoundationPhysicalGlobalConcaveMultiPathPlanner.Plan(...)`, não vinculada ao host em produção.

1. Consome o censo autoritativo v11, demanda e conversão por mercadoria, cotações externas explícitas, estradas físicas com titularidade/tarifa e **uma referência de carga por aresta**. Nenhuma estrada, capacidade, comprador ou item é inferido.
2. Obtém `FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate` uma única vez com taxas livres fixas. Confronta cada lote com auditoria independente, sem tomar o plano anterior como liquidação.
3. Divide cada entrega-pai em quantidades logísticas **exatamente representáveis** até `maxRouteBatchesPerParent`; exige `maxTotalRouteBatches` global, sem truncamento silencioso. Cada lote percorre a rota mais barata segundo o custo **prospectivo quartico** calculado com a carga já atribuída + esta parcela, e eventual tarifa de terceiro.
4. O mesmo cruzamento físico recebe uma só carga total, independentemente de sentido, mercadoria ou civilização. Partes com caminhos idênticos são reagregadas por entrega-pai.
5. Verifica **cada** identidade de vendedor, comprador, tile, ocupação e item por pai e a soma exata de suas quantidades. Repete toda a auditoria de atravessamentos, carga e conservação.
6. Calcula o objetivo integrado completo, em `BigInteger`, tanto para a solução original de uma rota quanto para a proposta multipath:
   
   `F = Σ_e freeRate_e × [L_e + ceil(L_e^5 / reference_e^4)] + Σ_{remessas e terceirizadas} tariff_e × cargo_e`.
   
   A mesma função `IntegratedPhysicalCost` do #396 determina o custo. Tarifa permanece termo linear por carga e proprietário; **não** existe cobrança monetária nem preço liquidado.
7. Se a heurística de divisão produzir custo integrado **estritamente menor**, aceita apenas as rotas (não altera compradores). Caso contrário descarta a proposta e conserva **as entregas originais integralmente**, inclusive suas rotas. Em ambos os caminhos, os resultados físicos são rechecados por auditoria independente.
8. Publica somente `MarketAllocation` (candidato global ainda não liquidado), `Parts`, `AllocationAudit`, `ProjectedEdges`, `RouteBatchDecisions`, `AppliedStrictImprovement`, `BaselineIntegratedFreightCost` e `FinalIntegratedFreightCost`.

**Invariantes de aceite:** `FinalIntegratedFreightCost <= BaselineIntegratedFreightCost`, 100% de produção preservada, nenhuma divisão abaixo da granularidade logística, nenhuma alteração de destinatário/civilização/mercadoria ou hash/snapshot, nenhuma carga de aresta não auditada e `NoClosedCycle`. Descontinuidade, orçamentos inválidos, ausência de escala, overflow e falha na reconciliação rejeitam o plano **sem retornar entregas parciais**.

## Testes projetados

- **Diamante Goldberg real**: dois corredores físicos de mesmo comprimento, um produtor e comprador distante; alta carga cria incentivo para dividir uma entrega entre pelo menos duas rotas. A política deve reduzir estritamente o custo integrado da rede e conservar 100% da mercadoria.
- **Stress de mercado real**: oito vendedores da mesma mercadoria, 104 mercados e 112 tiles conectados; conservar pai/filhos, frete por aresta e titularidade, sem aumento do custo integrado, com observabilidade do benchmark.
- **Budget adversarial**: máximo total de lotes insuficiente recusa explicitamente sem publicar distribuição parcial, sem modificar o estado do host.
- Regressões históricas Windows/Ubuntu, Godot Mono e StateHash/Kernel.

## Limites obrigatórios

Este gate **não garante** que o greedy multipath sempre melhore; por isso a aceitação global é conservadora. Mesmo quando melhora, não constitui ponto fixo de custos e cargas, ótima distribuição conjunta com congestionamento, equilíbrio de Wardrop, ótimo contínuo dos mercados, execução de rotas em tempo real ou liquidação econômica. Nenhuma tentativa de realimentar custos de congestionamento é considerada aprovada com base apenas nesta redução de potencial de rota. Este é um **building block** verificável para o próximo gate de otimização conjunta.

A complexidade ainda cresce com o número de entregas e lotes × tamanho do grafo; o cenário de 104 mercados não garante escala para dezenas de milhares de produtores. Sem banco/crédito, inventário ou intervenção tarifária artificial, sem novas regras econômicas.

**Governança:** novo PR Draft empilhado sobre #399; nenhum merge. `main`, Godot, cenas e estado autoritativo inalterados. `GetLastClosedEconomicTradeView` continua `NoClosedCycle`. Não anunciar gate aprovado sem CI.
