# GA-FOUNDATION-F084F — Liquidação transitória por preço de destino (F-071)

**Data:** 2026-11-08.  
**Estado:** implementação laboratorial de **projeção financeira pura**, sem lançamento em tesouro, save, journal ou execução de mercado.  
**Base de implementação:** PR #306 (F-084E), derivado de #305/F-084D, #304/F-084C, #303/F-084B, #302/F-084A e #298/GA-FOUNDATION-000.  
**Agricultura:** PRs #299–#301 independentes e preservados.  
**Decisões-base:** F-041, F-043, F-046, F-048, F-055, F-059, F-060, F-062, F-066, F-071, F-072, F-073 (ainda PROPOSTA) e F-084(4).

## 1. Problema F-071 que este gate responde

Uma aresta pode conduzir uma unidade de mercadoria de uma civilização que vende no mercado X a preço 2, ou uma unidade idêntica destinada ao mercado Y a preço 1. O registro de **fluxo agregado `f[e,c,i]=1`**, isoladamente, não indica qual valor monetário cruzou a aresta. Portanto, **não há liquidação tarifária correta sem origem/civilização, destino/mercado/produto e decomposição transitória do fluxo para fins de rateio**.

F-055 mantém os **agregados físicos abstratos** como representação principal do fluxo e dos mercados; o contrato matemático de liquidação apenas usa uma decomposição efêmera de vendas já resolvidas por outro algoritmo. **Não** surgem mercadorias físicas, estoque, caminho como entidade persistente, propriedade de itinerário ou carteira de consumidores.

## 2. Entradas do laboratório

- `DestinationMarketInput(DestinationNodeId, CommodityId, DemandRaw, TotalSoldQuantityRaw)`: **Q agregado de todas as origens** vendido num mercado de destino/produto, e sua demanda D. O futuro clearing resolverá Q e fornece o snapshot; este módulo não o determina.
- `TransientDestinationSale(FlowId, OriginNodeId, DestinationNodeId, CommodityId, SellerCivilizationId, SoldQuantityRaw, UsedEdges)`: uma parcela **já atribuída** à origem e ao destino, com seu caminho lógico **somente como decomposição transitória da solução**. Identificadores estáveis garantem desempate de quantização, não autorizam pagamento duplicado.
- `TransientEdgeUse(StableEdgeId, Tariff, Holdings)`: peso local da aresta derivado do contrato **experimental** F-073 e quotas `H[e,c]` de capital efetivamente incorporado. Devem ser consistentes para a mesma aresta em todas as parcelas da mesma avaliação.

O módulo valida chaves de mercado `destino×produto`, lotes únicos, Q de cada mercado reconciliado exatamente com a soma das vendas, caminhos com zero aresta somente para venda local, ausência de aresta duplicada dentro de uma parcela, proprietário sem ID duplicado, capital não negativo e soma de quotas igual ao capital físico da aresta. Identidades/rateios conflitantes em uma mesma aresta do mesmo snapshot são recusados.

**Limite de autoridade:** um `UsedEdges` fornecido sem os extremos topológicos das arestas não prova por si só continuidade, elegibilidade, incidência física da rota, conservação de carga no grafo ou ausência de fraude. O solver/journal autoritativo terá de demonstrar essas condições antes de aceitar sua projeção.

## 3. Preço por destino, e quantização monetária

Para `Q>0,D>0`, F-043 determina o mesmo preço para **todos os produtores do mesmo item e mercado**:
```text
P_dest,i = sqrt(D_dest,i / Q_dest,i)
ReceitaBruta_dest,i = Q_dest,i * P_dest,i
```

O protótipo de ponto fixo (`Scale=1.000.000`) calcula `PriceRaw = floor(sqrt(D_raw × Scale² / Q_raw))` usando **raiz inteira sobre BigInteger**. O valor de mercado em unidades mínimas é `GrossRaw=floor(Q_raw × PriceRaw / Scale)`; **não** recota um preço para cada origem. Para Q=0, sem cotação: `PriceRaw=0, GrossRaw=0`. Para Q>0,D=0, rejeita sem inventar preço. Se a escala fixed-point não representar preço positivo, falha explicitamente: **a precisão econômica final ainda depende de ratificação**.

O montante inteiro **do mercado como um todo** é distribuído proporcionalmente às quantidades `q_l/Q`, atribuindo qualquer resíduo pelos **maiores restos** com desempate por `FlowId` estável. Assim `Σ_l gross_l=GrossMarket` **exatamente**, sem criação/perda de dinheiro por arredondar cada venda isoladamente. Exemplo com três lotes de uma microunidade, demanda 10 microunidades, Q=3: receita arredondada de 5 unidades monetárias mínimas -> distribuição 2, 2, 1 para IDs 3, 5, 9. Esta é uma **política de quantização proposta**, não uma nova regra de preço ou preferência econômica.

## 4. Repartição F-060 + F-062, conservando 100% da receita

A civilização titular da produção é a **recebedora da parte líquida**; trabalhadores e locais não recebem carteiras automaticamente (F-048). O setor comprador **implícito** gera a receita bruta sem saldo consumidor representado (F-046).

Para a parcela vendida de valor bruto `V_l`, cada aresta tem intensidade de referência `w_e` e coproprietários `H[e,j]`. A civilização vendedora c fica isenta da própria quota:
```text
s[e,j] = H[e,j] / Σ_k H[e,k]
W_l = Σ_e Σ_(j≠c) w_e*s[e,j]
V_líquida_c = V_l / (1+W_l)
V_titular_e,j = V_l*w_e*s[e,j] / (1+W_l), somente j≠c
Σ créditos da parcela = V_l
```

O laboratório calcula o peso externo de cada aresta com razão de capital. O arredondamento das parcelas de peso dos diferentes cotitulares é reconciliado por **maiores restos e IDs estáveis** para que `Σ_j w_e,j=W_e` exatamente na escala inteira. Em seguida, para **cada venda**, define destinatários: **produtor** (peso-base 1) e todos os **cotitulares externos** das arestas efetivamente utilizadas (pesos reconciliados). Distribui o valor bruto dessa venda por maior resto, com desempate fixo por `EdgeId` e `CivilizationId`. O vendedor tem chave de desempate `EdgeId=-1`. A soma de todos os créditos é exatamente a receita bruta da venda em unidades mínimas.

Uma civilização proprietária de mais de uma aresta pode receber mais de um crédito por venda; o agregado de tesouros por civilização é derivado da soma. Vendas locais e arestas naturais sem capital não geram tarifa. Uma venda nunca credita `V` bruto ao vendedor **e** novamente a repartição: credita-se apenas sua fração `V_líquida` e as partes dos outros titulares.

**Importante:** o peso de tarifa `w_e` F-073 **não é ratificado**. O contrato F-060 de como repartir um valor dada uma intensidade é aprovado; sua escala e o comportamento do mercado diante da tarifa ainda exigem nova validação. A regra de rateio monetário não inclui a impedância física `L` no denominador financeiro; F-072 só a utiliza para comparar alternativas de acesso.

## 5. Exemplo mínimo que demonstra por que o fluxo agregado não basta

Duas vendas da civilização A, ambas de quantidade 1 do mesmo produto:
- Mercado X: Q=1 e D=4 -> P_X=2, bruto=2.
- Mercado Y: Q=1 e D=1 -> P_Y=1, bruto=1.
- Uma aresta e melhorada pertence integralmente a B, com intensidade de laboratório `w≈0,846846`.

Se a venda para X usa e, a receita bruta atribuída ao fluxo tarifável é **2**; se, no mesmo agregado de carga de uma unidade na aresta, a venda para Y usa e, a receita bruta atribuída é **1**. Os créditos a B em ponto fixo de laboratório são aproximadamente **0,917073** versus **0,458536**. Os dois cenários têm vendas totais brutas idênticas de **3** e a mesma carga agregada sobre e, mas **contabilidade de titulares distinta**. É exatamente a lacuna da F-071, não uma discrepância de preço.

Esses são dois **snapshots alternativos dados**; sem solver global e sem tarifa endógena não se afirma que ambos são equilíbrios comerciais válidos (F-059/F-072).

## 6. QA, determinismo e alcance

A regressão F-084F valida:
1. mesmo preço por mercado/produto para todos os vendedores e receitas brutas agregadas pelo destino;
2. resposta distinta de uma aresta a vendas X/Y com mesma carga mas preços diferentes;
3. venda integral atribuída a mercados, sem estoque e sem carteira compradora;
4. venda local sem tarifa, isenção do próprio capital, receita positiva de titulares externos;
5. duas ou mais arestas e cotitulares, sem taxa cobrada duas vezes e sem crédito ao titular que é o próprio vendedor;
6. soma exata dos recebimentos de cada parcela/mercado/batch, sem saldos negativos nem perda de unidades por arredondamento;
7. mesma saída para enumerações permutadas de lotes, mercados, arestas ou coproprietários;
8. duplicidades e snapshots divergentes rejeitados;
9. Q=0 não produz preço transacional, D=0 com Q>0 recusado, subprecisão ou overflow explícitos;
10. checksum independente para 4.096 preços F-043 e benchmark de lotes de venda por destino.

**Não implementado:** escolha de rota e destino, equilíbrio F-059/F-072, livro de tesouros autoritativo, deduplicação de comandos/journal, escrituração de imposto, pagamento real, modelo de emprego, nova geração Goldberg, UI, consumo e demanda endógena. O `DestinationRevenueProjection` é **um cálculo de créditos a aplicar uma única vez** após confirmar causalidade e idempotência no motor; repetir `Settle` devolve os mesmos valores, mas não realiza duas transações.

## 7. Homologação e próximo gate

```powershell
dotnet test .\GlobalArena.Foundation.Circulation.Tests\GlobalArena.Foundation.Circulation.Tests.csproj -c Release --nologo
dotnet run --project .\GlobalArena.Foundation.Circulation.Benchmark\GlobalArena.Foundation.Circulation.Benchmark.csproj -c Release
```

A CI `GA Foundation F084A-F Circulation Lab` deve demonstrar regressões F-084A–F em Linux/Windows/macOS, mais benchmark de 10 mil/100 mil **lotes transitórios** numa topologia sintética. Cargas e preços não são calculados por um solver de fluxo real; o resultado não autoriza alegar convergência nem gameplay completo.

**Próximo gate:** F-084G, um laboratório pequeno de **equilíbrio simultâneo** com ofertas, destinos, carga B, preços, custos C e pesos tarifários W, verificando gaps, escolha econômica e desempates F-059; apenas após essa etapa considerar contratos de integração ao runtime e testes Goldbergs procedurais. A prova de conservação monetária de F-084F é condição necessária, mas não suficiente, para um clearing economicamente correto.

**Histórico preservado:** #298 nova fundação; #299–#301 agricultura; #302–#306 circulação F-084A–E; este incremento F-084F dependente de #306, sem merge.
