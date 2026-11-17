# GA-Next — Gate 01B.2D-2: amortecimento verificável e escala espacial distribuída

**17/11/2026 · incremento C# empilhado no PR #392 · Draft, sem merge · não liquida um mercado nem desenha rotas no Godot**

## Situação de partida

A auditoria de carga por `PhysicalTacticalEdgeId` (#386), as alocações físicas com trabalhadores reais (#388), o roteamento por custo/titular (#389), a distribuição por múltiplos mercados (#390), a coordenação entre produtores (#391) e o custo físico endógeno `freeRate × [1+(load/referenceScale)^4]` (#392) já existem em código.

O Gate 01B.2D-1 exige consistência **exata** entre o custo efetivamente usado na distribuição e o custo recalculado do **volume final**. Em casos de concorrência por duas rotas substitutas, o algoritmo pode alternar entre elas, embora cada lote seja vendido integralmente. Publicar a média de dois estados ou um limite fictício de capacidade produziria um resultado econômico falso.

Também não era correto usar **10 mil trabalhadores na mesma origem** como substituto de teste de escala global: o custo de Dijkstra e do `water-filling` depende fundamentalmente da quantidade de **origens e mercados diferentes**.

## Implementação deste gate

A mesma API `FoundationPhysicalEndogenousCongestionFeedback.Coordinate` recebe agora um parâmetro opcional `FoundationPhysicalCongestionRelaxationPolicy(InitialDenominator, MaximumDenominator)`. Sem o parâmetro, o comportamento permanece **exatamente igual ao gate anterior**, com denominador 1 e sem alteração da matemática de preço ou custos.

O numerador do custo-alvo continua sendo a **curva de quarta potência auditada**, sem teto de fluxo. Apenas a atualização iterativa da estimativa física é amortecida:

```text
costTarget_e = freeRate_e × [1 + (auditedLoad_e / scale_e)^4]
costNext_e = costCurrent_e
           + sign(costTarget_e - costCurrent_e)
             × ceil(abs(costTarget_e - costCurrent_e) / denominator)
```

Todos os termos são inteiros; uma diferença não nula avança **pelo menos uma microunidade**, para não declarar estabilidade artificial causada por arredondamento.

- Configuração válida: `1 <= InitialDenominator <= MaximumDenominator`; os orçamentos de passes de produtores e de feedback continuam explícitos e estritamente positivos.
- Ao detectar a repetição exata de uma assinatura do **vetor canônico de taxas por aresta**, se existir margem no orçamento de amortecimento, o denominador é dobrado (limitado ao máximo autorizado), e o sistema reinicia a busca determinística com **custos livres originais**. A assinatura antiga é descartada para a nova estratégia numérica.
- Quando o denominador máximo já está em uso e o vetor de custos volta a aparecer, ou quando o orçamento de iterações acaba, a função lança exceção. Isso **não equivale** a concluir mercado liquidado.
- O sistema retorna **apenas** quando `costTarget == costCurrent` em **todas as arestas físicas** após uma distribuição integral auditada. Os diagnósticos registram `FinalRelaxationDenominator` e `AdaptiveRestarts`; `ConsistentWithFinalEdgeLoads=true` tem o mesmo significado forte de antes.
- Tarifas e titularidade permanecem inalteradas pelo amortecimento: somente a aproximação numérica do **custo físico** é atenuada. Não há tarifa cobrada, imposto, banco, receita, inventário ou tesouro criado.

### Limite de possibilidade do roteamento atual

Apenas reduzir o passo de ajuste do custo **não garante** a existência de um ponto fixo exato. Exemplo: um único produtor e um único comprador ligados por dois caminhos paralelos idênticos; o roteador do gate anterior seleciona **um caminho inteiro por entrega**. Quando esse caminho fica congestionado, toda a carga pode migrar para o outro, que por sua vez congestiona. Sem subdivisão de uma mesma entrega entre duas rotas, um equilíbrio de fluxos pode ser **irrepresentável** neste modelo discreto. Neste caso, o comportamento seguro é falhar com orçamento de simulação, não produzir uma camada visual fictícia.

A futura correção estrutural poderá exigir divisão de uma venda entre múltiplos caminhos, uma otimização de fluxo ou custo incremental. Este gate **não** disfarça essa ausência com uma média de rotas não executadas.

## Testes de regressão e escala

`FoundationPhysicalCongestionStabilityScaleTests` é executado em runners **Windows e Ubuntu**, com as suítes anteriores intactas.

1. Aritmética exata do amortecimento: 7 microunidades de diferença e denominador 4 avançam 2 microunidades, em ambas as direções; diferença de 1 nunca vira 0 ao usar denominador elevado.
2. Um cenário real de transporte entre dois tiles demonstra feedback amortecido (`denominator=4`) que só termina quando carga e custo congestionado efetivamente coincidem, sem alterar o cenário v11 ou publicar ciclo.
3. Um **diamante de quatro tiles** selecionado por adjacência real Goldberg, com duas rotas explícitas de mesmo comprimento, verifica que alternância persistente é rejeitada mesmo com estratégia adaptativa `1→2→4→8`. O teste não confunde ciclo de planejamento com solução comercial.
4. Política de denominadores zero ou inconsistentes é rejeitada antecipadamente.
5. **512 produtores em tiles distintos** e **1.024 mercados de demanda em tiles distintos** (um trabalhador produzindo e outro não produzindo em cada par, 1.024 trabalhadores), conectados por **512 arestas reais disjuntas**, com preços de planejamento explícitos e capacidades compartilhadas por aresta: o teste executa a pipeline completa de demanda/produtor, roteamento, alocação, auditoria de volume, atualização do custo e convergência. Registra `Stopwatch.ElapsedMilliseconds` para observabilidade, **sem limiar absoluto de wallclock**, que seria instável em runners hospedados compartilhados.
6. A carga física por cada uma das 512 arestas corresponde à carga do auditor; nenhuma é criada na costa sem identidade real; produção de todos os 512 lotes é integralmente alocada.
7. Como todo teste de escala, esse cenário é **esparso e disjunto**. Ele exercita muitas origens e mercados reais, mas **não** comprova desempenho em um único grafo continental densamente interconectado, com milhares de rotas alternativas ou compradores concorrendo pelas mesmas arestas. Esse benchmark deve ser incrementado antes da implantação em produção.

## Próximos gates e riscos

**Gate 01B.2D-3:** multi-path congestion-aware allocation: uma mesma remessa pode exigir várias rotas realmente utilizadas, cada qual auditada e conservativa. Medir custos por origem/destino, um grafo interconectado de alta escala e convergência com saturação intensa. Não extrapolar a partir do benchmark disjunto.

**Gate 01B.3:** liquidação econômica monetária real, arrecadação tarifária/tributária, treasury, journal e hash. Somente após fechar o ciclo autoritativo expor a visualização do último ciclo: largura pela carga real e cor pela titularidade histórica da infraestrutura.

**Invariantes de governança:** nenhum algoritmo deste PR é executado como fase de fechamento econômico em `FoundationTacticalHumanAiTurnHost`; o host usa somente `Snapshot.Workers` e `GetLastClosedEconomicTradeView` continua `NoClosedCycle`. O PR permanece Draft empilhado no #392; nenhuma cena, mapa, controle ou tecla aprovada no Godot é alterada e não há merge em `main`.
