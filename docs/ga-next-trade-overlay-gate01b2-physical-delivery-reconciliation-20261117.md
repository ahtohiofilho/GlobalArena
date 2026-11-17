# GA-Next — Gate 01B.2A: reconciliação física de entregas e compradores reais

**17/11/2026 · incremento C# empilhado sobre PR #387 · Draft/sem merge · sem interface**

## Fundamentação

O cenário jogável v11 possui **trabalhadores físicos e ocupações autoritativos**; PR #383 produz capacidade por `tile+civ+ocupação+mercadoria`; PR #384 calcula demanda **positiva por atividade e por mercadoria** em tiles de trabalhadores reais. O auditor do PR #386 aceita itinerários e comprimentos já escolhidos, enquanto o PR #387 protege o histórico do último ciclo fechado.

Ainda não existe no host v11 um resolvedor de mercado com **valor marginal, preço, custos, tarifas, capacidade logística, liquidação monetária e tesouros**. Não é correto publicar no mapa as cargas de um algoritmo candidato como comércio financeiro realizado.

Este corte liga os componentes existentes num **verificador de alocações físicas propostas**, como contrato que o futuro resolvedor do mercado precisará satisfazer **antes de qualquer publicação econômica**.

## O que é executável agora

`FoundationPhysicalDeliveryAllocationAuditor.Audit(` **censo v11, incidência fina, política explícita de demanda, política explícita de carga, propostas de entrega, evidências de titularidade** `)`:

- **Sellers reais:** toda proposta identifica `(tile físico de origem, civilização produtora, ocupação produtiva, CommodityId)`, e tem quantidade positiva em `GoodsMicro`. Essa chave deve existir em `FoundationPhysicalCommodityMarketInputs.SupplyLots` do snapshot. Não são criados produtores ou mercadorias adicionais.
- **Buyers reais:** toda proposta identifica `(tile físico consumidor, civilização consumidora, CommodityId)`, e deve existir como uma linha com demanda positiva calculada a partir do trabalhador real, inclusive os viajantes. **A demanda é uma intensidade de atração, não um limite rígido de compras**; nenhuma carteira ou comprador artificial é introduzido.
- **Alocação integral sem estoque:** exige que `Σ GoodsMicro` das propostas de cada lote seja **exatamente igual** à capacidade produtiva real desse lote, uma vez. Rejeita qualquer quantidade a mais, faltante, duplicação de ID ou lote sem alocação. Isso **audita uma proposta candidata**; não significa ainda que o mercado chegou a um preço de equilíbrio.
- **Mercadorias completas:** o catálogo do valor de demanda deve ser exatamente o catálogo da conversão de carga física. Os dois IDs do cenário inicial continuam placeholders sem associação canônica a alimento, madeira ou outro recurso.
- **Conversão explícita:** `FoundationCommodityCargoConversionPolicy` recebe a unidade logística compartilhada `CargoUnitCode` e um coeficiente `CargoMicroPerWholeCommodityUnit` **positivo por mercadoria**. Não existe default universal 1:1. Para cada proposta `CargoMicro = GoodsMicro × factor / 1_000_000`, com `UInt128`; conversões não exatas na escala micro e overflow são recusados, evitando criar/destruir carga quando uma venda é repartida.
- **Rotas verificadas:** cada proposta traz a **sequência completa** de tiles físicos, que deve começar no produtor e terminar no comprador. O auditor anterior verifica cada aresta adjacente, soma volumes bidirecionais e exige evidência explícita de `Owned/Unknown/Unowned/NoInfrastructure` nas arestas cruzadas. **O resolver não deduz donos, não escolhe rota nem atribui custo arbitrário.**
- **Censo confirmado, não intenções:** `FoundationTacticalHumanAiTurnHost.AuditDeliveryCandidates(...)` é **interno ao servidor/simulação**; lê apenas `Snapshot.Workers` já confirmado. Não fornece a todas as civilizações um relatório completo de comércio — consulta por participante continua apenas na porta protegida do PR #387. Ordens humanas pendentes não alteram o resultado; após `E`, propostas com profissão de origem antiga deixam de ser válidas.
- **Nenhum fechamento econômico:** o retorno `FoundationPhysicalDeliveryAllocationAudit` agrega quantidade total alocada, capacidade potencial, dados de mercado por mercadoria, e `FoundationPhysicalTradeEdgeLoadAudit`. Não escreve `WorldState`, não altera hash, não publica `FoundationSettledEconomicTradeCycleArtifact`, não aciona tarifas, não assume preços ou receita.

## Gates de validação

- Cenário real G(18,0) com 12 trabalhadores: 11 produtores ativos e um viajante; auditor valida **100% da capacidade** de 11 unidades físicas propostas em entregas locais, sem criar arestas de tráfego para as vendas locais.
- Caso de exportação entre duas civilizações: um lote tem comprador na outra capital; o caminho test-only é construído por BFS sobre a topologia real (o núcleo de gameplay **não** seleciona uma rota automaticamente nesta etapa). Cada aresta atravessada recebe a carga exata e titularidade explícita; produtores e compradores são reais.
- Propostas incompletas ou acima da capacidade, comprador inexistente, dono/tile da origem errado, profissão antiga após E, catálogo incompleto, rota com salto inválido e evidências ausentes de infraestrutura **não** produzem uma auditoria aprovada.
- Conversões físicas exigem precisão micro e catálogo completo. Rejeitar unidade vazia, coeficiente zero/duplicado, submicro e overflow.
- Stress de **10 mil pessoas físicas** no mesmo mercado: capacidade total consolidada em um lote e uma entrega, sem instanciar dez mil objetos de transporte.
- A função preserva o hash e o snapshot do host, e `GetLastClosedEconomicTradeView` permanece `NoClosedCycle` independentemente do resultado da auditoria.
- CI Windows/Ubuntu executa `FoundationPhysicalDeliveryAllocationAuditTests` explicitamente e mantém todos os gates Godot 4.7.2, kernel, hash, benchmark de frete e históricos.

## Limites e decisão de governança

Este PR é o **Gate 01B.2A — contrato de alocação verificada**, NÃO o Gate 01B completo. A prova de que uma *proposta* aloca 100% do produto não constitui demonstração de que a venda, o preço, a receita e o pagamento de tarifa foram **liquidados financeiramente**. Não existe seleção ótima de destino, water-filling de preços, função de custo logístico, capacidade/saturação das arestas ou matriz de produtos da agricultura canônica nesta entrega. Esses resultados devem ser criados e validados por fases econômicas subsequentes.

**Próximo incremento técnico recomendado:** resolvedor de propostas de mercado a partir das ofertas e demandas reais, respeitando política explícita de custo físico/tarifa e a adequação de commodity; então a liquidação monetária autoritativa e a publicação por ciclo usando os verificadores deste PR e #386/#387.

**Sem alterações de UI, overlay, shortcut, mundo, worker journal ou hash; PR Draft empilhado sobre #387, sem merge.**
