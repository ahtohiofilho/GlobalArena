# GA-Next — Gate 01B.2D-1: congestionamento endógeno de transporte por aresta física

**17/11/2026 · incremento de simulação sobre PR #391 · Draft sem merge · sem Godot novo**

## Decisão e objetivo

O Global Arena deve refletir, no custo do comércio, **o volume real agregado de todas as mercadorias e civilizações que atravessam uma mesma aresta do grafo físico**. Esse volume, futuramente, também definirá a largura da rota na camada de comércio, e a **titularidade histórica da infraestrutura** determinará sua cor.

O laboratório antigo `PhysicalWorkerTradeCycle` já investigou a curva contínua `FreeCost × (1+(Load/Scale)^4)`, porém somente com IDs locais e aritmética `double`; o sistema v11 dispõe agora do auditor físico #386, das entregas reconciliadas #388, dos caminhos #389, da distribuição marginal multi-mercado #390 e da coordenação entre produtores #391. Este corte faz o feedback da **carga física consolidada** atuar sobre a seleção de rotas e mercados, sem declarar uma liquidação financeira já inexistente.

## Contrato novo

`FoundationPhysicalEndogenousCongestionFeedback.Coordinate(census, incidence, demandPolicy, conversionPolicy, quotes, freeFlowEdgeCosts, carryingScales, maxProducerPasses, maxCongestionIterations)`:

- `freeFlowEdgeCosts`: uma entrada explícita por aresta realmente transitável, com custo físico livre, titularidade e tarifa externa. Arestas não informadas continuam bloqueadas; a via natural sem infraestrutura precisa ter atributos reais informados, nunca é gratuita por omissão.
- `FoundationPhysicalEdgeCarryingScale(PhysicalTacticalEdgeId, CargoMicroAtReferenceLoad)`: capacidade de **referência física** positiva e explicitamente informada para cada aresta transitável, com **a mesma unidade logística comparável do catálogo de mercadorias**. Não é um limite rígido de transporte.
- `FoundationPhysicalCongestedEdgeReading`: em cada aresta declarada, volume final agregado, capacidade de referência, custo livre, custo físico congestionado e titular/tarifa. Armazena também as arestas declaradas com fluxo zero; uma aresta omitida nunca se transforma em via pública.
- `FoundationPhysicalEndogenousCongestionPlan`: distribuição física final auditada, todas as leituras de aresta, número de iterações de feedback e indicador de consistência entre custo usado e carga final. **Não** é prova de eficiência global, relatório de receita ou ciclo fechado.

## Curva e escala

A regra do laboratório preservada como **hipótese de planejamento**, para cada aresta física `e`, é:

```text
freeRate_e > 0
carryingScale_e > 0
load_e = soma de CargoMicro de TODAS as entregas/mercadorias
         e de AMBAS as direções atravessando a aresta e

congestedPhysicalRate_e = freeRate_e × [1 + (load_e/carryingScale_e)^4]
externalTariff_e        = constante exógena declarada
ownerPaysTariff_e       = false
nonOwnerPaysTariff_e    = true, quando houver titular válido
```

O termo `(load/capacity)^4` é calculado com `BigInteger` exato e arredondado **para cima** à microunidade monetária por unidade de carga. Isso evita `double`, `NaN`, truncamento silencioso de congestionamento pequeno e overflow intermediário, embora a precisão monetária mínima ainda seja discreta. Se o resultado extrapola `ulong`, **falhar fechado**, sem impor teto artificial.

Exemplo matemático (somente fixture, não regra econômica do jogo): com custo físico livre de 100.000 microunidades e capacidade de referência 1.000.000 microunidades de carga, o custo calculado é 100.000 com fluxo zero, 200.000 com fluxo igual à referência e 1.700.000 com duas vezes a referência. **A produção pode exceder a capacidade de referência**; não há bloqueio mecânico de mercado ou déficit alimentar.

### Invariante de costa e mercadoria

Uma aresta `PhysicalTacticalEdgeId` recebe **um único volume total** com as cargas de milho, trigo, madeira, metal, outras mercadorias e civs somadas após conversão para unidade comum. Nem cinco mercadorias, nem cinco estradas adjacentes à costa multiplicam a capacidade de uma mesma aresta. O custo congestionado é aplicado uma vez à travessia de cada carga; uma entrega cruzando três arestas utiliza as três capacidades físicas. Uma entrega local não consome capacidade de aresta.

## Pipeline e condição de estabilidade

1. Materializar/validar políticas de ofertas, demandas, cotações, vias, custos e capacidade **uma única vez**; a capacidade precisa cobrir **exatamente** as arestas transitáveis.
2. Resolver a distribuição marginal entre produtores do PR #391 com os custos físicos atuais. Esse resultado contém somente entregas físicas candidatas, integralmente auditadas por #388 e #386.
3. Obter o volume consolidado final `load_e` diretamente de `AllocationAudit.Freight.UsedEdges`, nunca de pedidos pendentes ou propostas de rota descartadas.
4. Recalcular `congestedPhysicalRate_e` em todas as arestas, mantendo **inalterados** a titularidade e a tarifa externa.
5. Repetir o planejamento completo com os novos custos até que **cada custo físico calculado com as cargas finais seja idêntico ao custo físico realmente usado para selecionar aquelas mesmas rotas e compradores**. Nesse caso, `ConsistentWithFinalEdgeLoads = true`.
6. Se `maxCongestionIterations` (orçamento explícito) terminar sem estabilidade, ou uma assinatura de custos se repetir ciclicamente, lançar exceção. **Não publicar** a solução transitória como mercado liquidado ou último ciclo fechado.

Essa condição é ponto fixo do **mapeamento carga→custo→distribuição candidata**, não afirma ótimo global, convergência universal ou acerto do preço de equilíbrio. Congestionamento intenso pode causar oscilações — o resultado correto nesta versão é falhar com um diagnóstico, não inventar média/resultado estável. Futuros gates poderão incluir amortecimento e procura robusta de equilíbrio com critérios objetivos.

## Performance e observabilidade

O algoritmo opera por **aresta física** e por **lote agregado**, não por trabalhador individual; produtores no mesmo tile/ocupação/mercadoria são consolidados antes de roteamento. Dijkstra por origem/civ e auditorias de #391 são reutilizados internamente a cada nova iteração. O tempo por ciclo ainda depende de `numberOfSourceMarkets × numberOfBuyerMarkets × congestionIterations × marginalIterations`. Nenhum benchmark de muitos milhares de produtores e mercados distintos foi realizado por este PR; **10 mil trabalhadores concentrados em um lote não provam escalabilidade total**.

Os diagnósticos de retorno incluem carga, capacidade e taxa física antes/depois em cada aresta, para estudos futuros de hotspot logístico sem adicionar novas cores à interface. O custo da curva de quarta potência em BigInteger é calculado por aresta, não por transação comercial.

## Segurança / teste e governança

`FoundationPhysicalEndogenousCongestionFeedbackTests` cobre:
- Curva de quarta potência em carga nula, uma e duas vezes a capacidade; monotonicidade, micro-arredondamento e overflow sem teto artificial.
- Dois produtos diferentes consumindo a **mesma capacidade física**, com entrega cruzada verdadeira e titularidade histórica preservada.
- Falhas de escala ausente, duplicada, zero, de mundo/topologia estrangeira e orçamento de iteração insuficiente.
- Resolução determinística sob reordenação dos inputs.
- 10 mil trabalhadores em um mesmo lote, vendas locais e ausência de congestionamento/volume em estrada não utilizada.
- Host v11: ordens de profissão pendentes não mudam censo; após `E` o retorno da IA à produção eleva o cenário de 11 para 12 unidades; consultas não alteram hash/journal.
- CI .NET Windows/Ubuntu + regressões reais do Godot Mono 4.7.2, hash/kernel multiplataforma e benchmark de mercado/frete.

**Limitações remanescentes:** custos e capacidades são fornecidos explicitamente por políticas, não derivados ainda de investimentos reais no mundo v11. O feedback **não** processa preços de mercado como eventos financeiros, tarifas arrecadadas, impostos, tesouros ou settlement; não cria `FoundationSettledEconomicTradeCycleArtifact`. Portanto, `GetLastClosedEconomicTradeView` continua **`NoClosedCycle`** e o overlay de rotas reais **não é ativado**.

**Próximo gate sugerido:** análise de convergência e amortecimento para mercados difíceis + benchmark de muitas origens/arestas; em seguida parametrização física de infraestrutura natural/investimento, receitas/tributos e publicação autoritativa de um ciclo econômico efetivamente fechado, antes do overlay Godot.

**PR Draft empilhado sobre #391. Sem merge em main, sem mudança de controles aprovados.**
