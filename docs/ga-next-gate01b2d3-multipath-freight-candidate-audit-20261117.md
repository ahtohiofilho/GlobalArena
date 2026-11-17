# GA-Next — Gate 01B.2D-3A: várias rotas físicas para uma entrega econômica candidata

**17/11/2026 · C# sobre PR #393 · Draft, sem merge · sem alteração no Godot**

## Justificativa

O Gate 01B.2D-2 testou dois caminhos físicos entre um produtor e um comprador, identificando uma limitação material: o resolvedor de frete original (#389 e #391) escolhe **um único caminho inteiro por entrega**. Se toda a carga alternar entre duas rotas, pode não existir um ponto fixo exato de custo e volume; o amortecimento (#393) evita algumas oscilações, mas não cria a variável de repartição de fluxo que falta.

Este gate introduz a **representação executável de múltiplos caminhos efetivamente utilizados para a MESMA atribuição de mercadoria e comprador**, conservando rigorosamente unidades físicas e agregando-as nas arestas existentes da malha Goldberg.

## Contrato e pipeline

`FoundationPhysicalMultiPathFreightPlanner.Plan(census, incidence, demandPolicy, conversionPolicy, quotes, freeFlowEdgeCosts, carryingScales, maxProducerPasses, maxBatchesPerDelivery)`:

1. Parte de um **censo v11 autoritativo**, da demanda de seus trabalhadores e de políticas monetárias/transportes/carga físicas **explicitamente informadas**. Executa a coordenação de **quantidades por mercado** do PR #391 com os custos livres informados. Este mercado inicial é um **candidato auditado**, não um preço/receita liquidado.
2. Mantém **inalterados** os seguintes dados de cada entrega do mercado inicial: `seller, sourceTile, occupation, buyer, buyerTile, commodity, GoodsMicro`. Não transfere compradores ou produção de um lote a outro.
3. A partir da equivalência física `CargoMicroPerWholeCommodityUnit`, calcula o **quantum exato de GoodsMicro** que se converte sem arredondamento. Divide cada entrega em até `maxBatchesPerDelivery` parcelas quase iguais; esse parâmetro é um **orçamento computacional de discretização**, não limite artificial de produção, capacidade de estrada, demanda ou preço.
4. A cada parcela, consulta exclusivamente as **arestas explicitamente declaradas transitáveis** com identidade real `PhysicalTacticalEdgeId`, usando Dijkstra e custo prospectivo da nova carga na aresta: `freePhysicalCost × (1 + ((alreadyRoutedCargo + thisBatchCargo)/referenceScale)^4) + externalTariffIfNonOwner`. A curva física utiliza o cálculo inteiro exato do PR #392; a titularidade, tarifa e referência de capacidade são por **aresta real** e são comuns a ambos os sentidos e todas as mercadorias.
5. Cada parcela adiciona seu `CargoMicro` a **cada aresta efetivamente atravessada**. Rotas idênticas da mesma entrega são agregadas; rotas distintas permanecem em **`FoundationPhysicalMultiPathDeliveryPart(ParentDeliveryOrdinal, ChildDelivery, CargoMicro)`**. A relação pai-filho permite auditar que a produção não foi criada por multiplicação de trajetos.
6. A lista FINAL de todas as entregas parciais é submetida **obrigatoriamente** ao `FoundationPhysicalDeliveryAllocationAuditor` (#388), que valida 100% da oferta real, compradores reais, itinerários físicos adjacentes, equivalência física exata e titularidade de infraestrutura, e ao `FoundationPhysicalTradeEdgeLoadAuditor` (#386). A soma das parcelas de cada entrega deve ser exatamente a quantidade original; os volumes acumulados por todas as arestas devem coincidir com a auditoria independente.
7. O retorno inclui `MarketAllocation` (original de #391), `Parts`, `RoutingBatches`, `AllocationAudit` e `ProjectedEdgeCosts`. As taxas congestionadas finais são **calculadas sobre o volume de todos os caminhos realmente propostos**; **não** afirmamos que os próprios caminhos foram escolhidos sob essas taxas finais ou que o ponto fixo esteja certificado.
8. Integração `FoundationTacticalHumanAiTurnHost.PlanMultiPathTradeCandidates(...)` é **interna ao servidor e somente leitura do snapshot confirmado**. Não modifica `WorldState`, journal, hash, movimento, tesouro, tarifas efetivamente cobradas ou visualização.

## Limites técnicos importantes

A escolha de caminhos é **heurística incremental com lotes físicos exatos**: compara o custo prospectivo de cada parcela e registra a carga imediatamente. Ela representa dois caminhos que o motor anterior não podia representar, mas **não prova** ótimo de transporte, equilíbrio Wardrop/KKT, water-filling conjunto de preços+destinos+rotas, nem convergência endógena global de frete. Para obter esse certificado será necessário um mecanismo de fluxos alternativos com otimização de custo marginal integral e realimentação entre destino econômico e vários caminhos.

A curva de congestionamento possui capacidade de referência, **não teto rígido**; o cálculo não impõe falta de alimentação quando excede a infraestrutura. Aresta não declarada não é gratuita. A tarifa continua separada do custo físico, e o titular é isento da própria tarifa. Um fluxo pode ter duas rotas, mas um trabalhador continua sendo contado uma única vez como produtor e demandante no seu tile.

Um comprador pode existir no mesmo tile do produtor, caso em que a venda local não consome nenhuma aresta.

## Gate de qualidade

Testes `FoundationPhysicalMultiPathFreightPlannerTests` em Windows e Ubuntu:

- **Diamante Goldberg** com duas rotas de duas arestas: uma mesma entrega remota é separada em caminhos distintos; a soma dos filhos reproduz exatamente a venda original, a carga por aresta é a quantidade física efetivamente atravessada e o modelo não anuncia um ciclo fechado.
- **Sem estrada declarada**: somente vendas locais, nenhum tráfego imaginário e hash/snapshot preservados.
- **Duas mercadorias sobre a mesma aresta**: uma única capacidade e um único titular histórico, com volumes por mercadoria cuja soma bate com o volume total.
- **Falha segura** para capacidade omitida, duplicada, nula ou de outra topologia, e orçamento de parcelas inválido.
- **Determinismo** com ordens permutadas de trabalhadores, mercados, capacidades e custos.
- **Benchmark de integração espacial** com 128 produtores e 256 mercados distintos em corredores físicos isolados; mede duração no runner, sem limiar de milissegundos arbitrário. Exercita cálculo dinâmico parcelado, mas **não equivale a um continente com milhares de produtores competindo por várias rotas**.
- Regressões C# históricas, Godot Mono 4.7.2, Kernel/StateHash multiplataforma e benchmark de frete existente.

## Governança e próximo gate

Os resultados ainda são **candidatos de comércio**, não o **último ciclo econômico efetivamente liquidado**. A fronteira `GetLastClosedEconomicTradeView` do PR #387 continua retornando `NoClosedCycle`: nenhuma cor/linha nova deve ser exibida no mapa antes do evento econômico autoritativo.

**Próxima prioridade:** acoplar otimização **conjunta e iterativa** de destinos e múltiplas rotas para melhorar o custo marginal efetivo, com benchmark de rede **densamente conectada** e estudo de frete sob saturação extrema. Depois: preço/receita/tarifa/imposto/titularidade real do mundo v11, liquidação, journal, publicação de ciclo e overlay Godot.

**PR Draft empilhado sobre #393. Sem merge e sem alteração da UI previamente aprovada.**
