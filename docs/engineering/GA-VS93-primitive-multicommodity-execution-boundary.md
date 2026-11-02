# GA-VS93 — Matriz de fronteira entre mercados reais e projeções primitivas

**Issue:** #219. **Baseline:** VS92 PR #218, SHA \`157f988f685aa98796e0c9b341f3fb103358c91f\` (58/58 checks \`completed/success\`). **Diretriz:** GA-ADR-ECON-007 R2 (#188). PR Draft empilhada, \`main\` preservada.

## Contexto

A VS92 encerrou a sequência de diagnóstico de representatividade dos preços locais **realmente pagos** pelo alimento no solver legado V12. No outro domínio, o GA já dispõe de trabalhadores/residentes reais e de projeções físicas locais para três insumos primitivos, com oferta potencial, pressão basal de demanda e preço relativo hipotético. Esses dois domínios não podem ser falsamente unidos como se toda oferta projetada tivesse sido executada ou paga.

A VS93 estabelece a interface auditável para saber **qual parte dos dados físicos/econômicos existe com autoridade de execução**. O próximo motor real multibem poderá usar a mesma separação de evidências, sem herdar falsas transações de madeira/pedra ou confundir o preço relativo VS78 com dinheiro.

## API e invariantes

\`CanonicalPrimitiveCommodityExecutionBoundary.FromCertifiedEvidence(session,currentPrimitiveSites,certifiedSampleVS92)\` requer:

1. \`CanonicalPhysicalTacticalSession\` viva, ciclo econômico realmente concluído no turno corrente, \`CanonicalPrimitiveSiteDiscovery\` atual com proveniência de mundo e revisão de rede/trabalhadores, e auditoria VS92 atual, que reconstrói VS91/90/89/88/87/85.
2. Mapeamento **explícito da política de descoberta** \`EconomicPrimitiveCommodityMapping\`: alimento ↔ agricultura; madeira ↔ silvicultura; pedra ↔ mineração. Os IDs dos bens são configuráveis por cenário (no benchmark: \`CommodityId(7), CommodityId(8), CommodityId(9)\`). O ID físico do alimento no cash-food legado V12 **não** é automaticamente o mesmo ID dos bens nas prévias experimentais. A ponte compara **a categoria FOOD por decisão de mapeamento**, não identidade monetária implícita de IDs.
3. Para cada bem, soma \`SupplyFlows.RawQuantity\` e \`DemandFlows.RawQuantity\` da política dos trabalhadores reais usando aritmética \`BigInteger\`; informa trabalhadores reais alocados na atividade. A oferta é **potencial não necessariamente extraída**; demanda é **pressão projetada**, mesmo que não tenha havido atendimento. Nenhuma dessas grandezas prova venda, preço monetário ou estoque acumulado.
4. Apenas o alimento pode ter prova de execução local \`CanonicalExecutedLocalFoodPriceWitness.FromLatestRealSettlement\` no solver V12, com \`ExecutedFoodRaw\`, \`ActuallyPaidMicrocredits\`, unidade monetária \`GA-LEGACY-FOOD-CASH-MICROCREDITS\`. Além de compras fisicamente registradas, informa o subconjunto com **pagamento positivo** (o arredondamento inteiro de microcréditos pode dar pagamento zero para alguma quantidade positiva).
5. Para madeira e pedra, os campos \`ActualLocalPurchasedRaw\`, \`ActuallyPaidMicrocredits\`, \`PurchasedFoodRawWithPositivePayment\` e \`ExecutedNominalUnitId\` são explicitamente **\`null\` (desconhecidos/não implementados)**, jamais 0 ou valor extraído de uma projeção. O indicador \`HypotheticalOnlyNoCanonicalExecutedMarket\` identifica essa lacuna. Por definição, dados locais de outras mercadorias e frete monetário não podem ser inferidos do cash-food.
6. Para alimento, a classificação distingue \`ExecutedLocalCashFoodWithPaidPrice\` (compra efetiva com algum pagamento positivo), \`PhysicalFoodReceiptWithoutPositivePayment\` (compra física registrada, mas nenhum pagamento positivo na amostra) e \`NoExecutedLocalFoodReceipt\` (nenhuma compra local executada no ciclo). Ausência de compra real **não** equivale a preço unitário zero de mercado.
7. A verificação independente \`VerifyIndependent()\` reconstrói recibos reais, demanda/oferta potenciais, mapeamento de trabalhadores e provas VS92, com comparação de cada linha. \`RequireCurrent\` invalida após avanço de turno, mudança de rede ou tentativa de transplantar evidência entre partidas/saves. Não existe método de escrever estado nem converter índices de preço em pagamento.

## Fora de escopo / limites

Esta etapa **não** é um solver de mercado de três produtos; serve como contrato de fronteira para construí-lo corretamente. Não há registro de vendas reais de madeira/pedra, comércio monetário intermercado ou frete efetivamente liquidado derivado do relatório. Não implementa poder de compra abstrato, preço nominal multibem, distribuição de renda, lucro monetário líquido ou estoque invisível.

Não altera \`WorldState\`, \`AdvanceMovementTurn\`, \`FoodCash\`, worker, cidades, guarnições, infraestrutura, transporte, ciclo econômico ou saves. Não cria crédito/bancos, nem permite \`SETTLE\`, compromissos financeiros ou compra baseada em \`B=40\`. Nenhuma informação hipotética VS78/VS84 ou sidecar VS86 vira venda real. A produção real de outros bens e a decisão de integração monetária permanecem deliberadamente para uma etapa posterior.

## QA e homologação

- Fixture canônica real (VS92) e descoberta atual de sites \`GA-VS62\`: três bens, mesmo mundo e ciclo.
- Testes afirmam preço cash-food autêntico e recibos inteiros exatos, diferença explícita entre volume físico comprado e volume pago, \`null\` obrigatório de transações de madeira/pedra, potencial e demanda derivados de \`Flows\`, correspondência dos trabalhadores residentes, estado obsoleto após turno, revisão de rede, sessão restaurada estrangeira, determinismo e inalteração do save.
- Benchmark \`--vs93-multicommodity-execution-boundary\` 8/32/128 sobre produção potencial/demanda atuais e evidência de compras locais reais, sem modificar o mundo.
- Gate dedicado Linux regressões VS40–VS93, Windows suíte integral, builds Godot Mono Release/Debug. **59 workflows esperados**.
- A homologação fica limitada a **59/59 \`completed/success\` no mesmo HEAD SHA** da PR Draft. \`main\` não recebe merge automático.

## Direção de VS94

A sequência de relatórios VS87–93 tornou explícita a lacuna: para madeira/pedra, temos o **quanto poderia ser ofertado** e o **quanto é demandado**, mas não comprovamos produção efetiva, entrega, compra e pagamento. Antes de promover um mecanismo multibem, definir um pipeline experimental de quantidades físicas **realizáveis** com conservação, apropriação e critérios institucionais de compra, sem forçar uma carteira financeira artificial e sem tornar a UI microgerenciável. Isso deverá ser separado de qualquer \`SETTLE\` real até que o modelo de poder de compra abstrato e fechamento simultâneo esteja formalmente validado.
