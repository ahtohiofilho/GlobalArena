# GA-VS92 — Representatividade INTERNA da amostra de alimentos localmente pagos

**Issue:** #217. **Baseline:** VS91 PR #216 SHA \`746b532cdc87d4e2b4c4bc86608ae939b590e85f\` (57/57 GitHub Actions PASS). **ADR:** GA-ADR-ECON-007 R2 (#188). **Status:** laboratório read-only.

## Contexto e decisão

As VS87–VS91 já provam compras locais **executadas** de alimento no mercado legacy V12, analisam as tendências de dois ciclos, as direções comparadas com cotações HIPOTÉTICAS VS84 e a estabilidade/robustez ao longo de 3 a 8 ciclos. Resta a pergunta: *o trecho efetivamente observado é estreito ou concentrado demais para uma análise local de alimento?*

A VS92 registra **indicadores exatos de representatividade interna do subconjunto de compras locais de alimento**. Não é um estimador estatístico da economia inteira. Sem universo de mercadorias, consumidores, preços pagos e amostragem probabilística, nunca podemos dizer que os resultados são representativos do mercado geral, dos trabalhadores, de todo alimento consumido, ou de variações de oferta e demanda. Mesmo o diagnóstico favorável **não** autoriza atualizar automaticamente a produção.

## Contrato de proveniência

\`CanonicalExecutedFoodSampleAudit.FromVerifiedRobustness(session, certifiedGateVS91, scenarios)\`:

1. Exige a mesma \`CanonicalPhysicalTacticalSession\` corrente, \`certifiedGateVS91.RequireCurrent(session)\` e \`VerifyIndependent()\`. A cadeia de provas inclui VS90 (3..8 ciclos concluídos), VS89 (última matriz de preços), VS88 (mesmos tiles entre dois ciclos), VS87 (compras pagas V12), VS85 (ticks reais e hashes).
2. De \`State.FoodCash.Receipts\`, para **cada par de ciclos do VS90**, conta os tiles que efetivamente pagaram por alimento na origem/destino (\`PaidMicrocredits > 0\` e preço unitário V12 positivo) e os tiles que pagaram nos dois momentos. Não conta preço ausente ou \`Paid=0\` como observação de preço pago.
3. Quantidade comum \`q_comum = Σ_{tiles pagos em ambos} min(FoodRaw_origem,FoodRaw_destino)\`. Conserva os denominadores \`q_origem\` e \`q_destino\` do VS90: volumes **efetivamente consumidos nas compras locais**, inclusive quantidades de recibos cujo pagamento arredondou para zero; não extrapola para toda demanda ou produção. Os indicadores físicos de cobertura exata são \`q_comum/q_origem\` e \`q_comum/q_destino\`, ambos verificáveis com \`BigInteger\`, sem conversões flutuantes.
4. Concentração: \`q_maior_tile/q_comum\`, onde \`q_maior_tile = max_tile(min(q_origem,tile,q_destino,tile))\`. Confronta o teto declarado em permille, sem suposições sobre tamanho da população. Essa razão mede concentração de **quantidade pareada**, não participação total do mercado nem preços.
5. Continuidade longitudinal: quantidade de IDs de tile presentes no conjunto de compras **pagas e pareadas de TODAS as janelas**. Uma janela sem comparabilidade torna a persistência completa zero. Não preenche lacunas por interpolação.
6. Reconcilia exatamente, **para cada janela**, quantidade total de origem/destino, coorte comum, preços nominais ponderados e quantidade de tiles pareados com o VS90 autenticado. Diferenças bloqueiam a auditoria. \`VerifyIndependent()\` repete todos os cálculos e o replay da cadeia de provas; \`RequireCurrent()\` invalida after tick, rede alterada ou instância restaurada estrangeira.
7. 1 a 8 cenários diagnósticos declarados, ID único ASCII, com \`MinimumMatchedTilesPerWindow\` 1..512, \`MinimumPairedCoveragePermille\` 0..1000 em **ambos** os ciclos, \`MaximumLargestTileSharePermille\` 0..1000, \`MinimumEligibleWindows\` dentro de 1..7 e \`MinimumPersistentMatchedTiles\` 0..512.
8. Classificações ordenadas: \`NoPairedPaidFoodPrices\`, \`InsufficientMatchedTileDiversity\`, \`InsufficientPairedPhysicalCoverage\`, \`ExcessiveSingleTileConcentration\`, \`InsufficientEligibleWindows\`, \`InsufficientPersistentTiles\`, \`WithinDeclaredFoodSampleCriteria\`. A última **não** significa representatividade macroeconômica, qualidade estatística da amostra, previsão validada ou permissão de \`SETTLE\`.

O helper estático \`EvaluateHypotheticalCriteria\` aceita janelas **sintéticas** exclusivamente para explorar limites e testar a aritmética; não comprova proveniência de preços. Somente \`FromVerifiedRobustness\` vincula observações aos recibos da sessão real.

## Diretrizes econômicas preservadas

- \`WorldState\`, \`AdvanceMovementTurn\`, contas, saldos, estoques, trabalhadores residentes e produtores, infraestrutura, frete, turnos econômicos, contratos e save **inalterados**.
- Não há preços reais para madeira, pedra, metais, transportes ou poder de compra abstrato; índices VS78/VS84 são **hipotéticos**, nunca usados como evidência de compras.
- Nenhum ganho escolhido automaticamente, mecanismo de crédito/banco, estoque físico invisível, extrapolação de demanda, transbordo fictício ou SETTLE novo.
- Diferenças na quantidade vendida **não** provam por que mudaram a oferta/demanda; não se inferem causalidade, confiança estatística, previsão ou desempenho de política.
- Todos os thresholds são explícitos; nenhuma “política vencedora” pode ser promovida com base no relatório.

## Homologação

Testes incluem recibos V12 reais com 3 ciclos, reconciliação de coortes por tile e concentração, mínimo de tiles, volume de cobertura exato, maior tile dominante, amostra vazia, persistência longitudinal entre todas as janelas, parâmetros inválidos, duplicidade, mundo restaurado, tick novo, rede alterada, determinismo e ausência de mutações.

Benchmark 8/32/128 com três cenários fixos: baseline mínimo, dois tiles por janela e teto de concentração de 50%; Windows suíte completa, Linux regressões VS40–VS92, Godot Mono Debug/Release. **58 workflows esperados**. PR Draft empilhada VS91, sem merge para main. Homologação somente no mesmo HEAD com 58/58 \`success\`.

## Fase seguinte

O gate adequado antes de qualquer integração macro não é mais um critério sobre o mesmo alimento legado, mas prova de vendas reais e seus preços em **outros bens**, além do desenho executável e conservativo do poder de compra abstrato. A VS92 mede precisamente as **limitações do recorte atual** e não resolve esse escopo de mercado incompleto.
