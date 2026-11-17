# GA-Next — Demanda por mercadoria a partir da atividade de cada trabalhador

**17/11/2026 · hipótese econômica independente · PR empilhado sobre #383 · Draft, sem merge**

## Finalidade

A aprovação do PR #383 estabeleceu a base: **cada trabalhador físico é uma fonte de demanda localizada** e cada produtor ativo tem capacidade positiva e uma ocupação individual. O mercado experimental antigo não é o `WorldState` autoritativo. Neste incremento, convertemos as intensidades individuais em **linhas de demanda por mercadoria e por tile** por uma política EXPLICITAMENTE entregue pelo chamador, não por pesos escolhidos silenciosamente pelo jogo.

As mercadorias conhecidas do cenário atual continuam IDs numéricos experimentais. **Não foi publicado um catálogo definitivo**, nem se presume que commodity `1` é milho ou `2` é madeira; a profissão continua sendo um atributo diferente da mercadoria. O catálogo futuro terá os seis cultivos alimentares acordados, madeira, pedra e demais minerais por época.

## Política de demanda (injetada, jamais padrão automático)

`FoundationActivityCommodityDemandPolicy` precisa receber:
- **Catálogo explícito** de `CommodityId` distintos, positivos e existentes na política, sem inferência de tipo, sem itens novos e sem substituição de etiquetas.
- **Matriz completa e estritamente positiva** de intensidades `(perfil de atividade, commodity) → IntensityMicroPerWorker`. A matriz tem seis perfis: produtor agrícola, produtor florestal, produtor mineral, viajante, militar e outros.
- Nenhum peso, proporção, demanda fixa 40/70, fator default `1` por mercadoria, cesta especial, origem de preferência ou comprador sintético. Uma política faltante, duplicada ou com peso zero é rejeitada **antes de projetar qualquer mercado**.

O perfil do trabalhador é o exercício da sua atividade no turno:
- `Producing + Agriculture/Forestry/Mining` → perfil produtivo correspondente.
- `Traveling`, `Military`, `Other` → perfil próprio independentemente da profissão latente. **Um trabalhador participa de exatamente um perfil**, mas de todos os produtos definidos pelo catálogo.
- Coeficientes são absolutos, não cotas normalizadas. Assim, **atividades diferentes podem gerar volumes totais de demanda diferentes**, além de diferentes composições, conforme o coeficiente que o gameplay adotar depois.

Para cada trabalhador `w` e mercadoria `c`:

```text
D_micro(w,c) = floor( DemandMultiplierMicro(w) *
                       IntensityMicroPerWorker(profile(w),c) / 1_000_000 )
```

O cálculo usa `UInt128` intermediário, somas `ulong checked` e ordenação canônica por tile/civilização/commodity. Se a intensidade positiva for tão pequena que se arredonde a zero na escala micro, a configuração é rejeitada explicitamente; não se injeta um piso artificial de demanda. Unidades são demanda relativa em microunidades, **não moeda**, `D` monetário ou carteira do comprador.

## Contrato de saída

`FoundationPhysicalCommodityDemandProjector.Project(PhysicalWorkerRuntimeState census, policy)` primeiro invoca o PR #383, preservando os `FoundationPhysicalSupplyLot` originais do último censo confirmado. Em seguida devolve:

1. `DemandByCivilization`: `(tile físico, civ, item) → demanda em micros, número de pessoas reais daquela civ`. Não fornece novos trabalhadores.
2. `MarketDemand`: `(tile físico, item) → soma de demanda das civilizações no MESMO tile`. É um ponto de consumo físico, ainda não é preço nem transação.
3. `SupplyLots`: os lotes **de capacidade** (item + ocupação + origem + civ) do PR #383, sem alterar quantidade nem associar produto à profissão por nome.
4. Totais reconciliados de demanda por produto e capacidade ativa, distintos do `TotalDemandMultiplierMicro` do PR anterior, pois atividades podem multiplicar ou diminuir demanda geral.

`FoundationTacticalHumanAiTurnHost.GetCommodityMarketInputs(policy)` lê apenas `Snapshot.Workers`, sem mutação ou prematura execução. Ordens pendentes (inclusive realocação em lote) **não alteram o mercado** até o encerramento do turno `E`; em seguida a projeção recalcula a posição física, perfil e demanda de cada indivíduo.

## Provas e limites

- Fixture com 12 pessoas, 2 produtos provisórios e seis perfis diferentes: demanda específica para ambos os produtos, sem compradores artificiais e sem oferta do trabalhador viajante; preços e receitas ausentes.
- Lote de ocupação + movimento no mesmo turno: antes de `E`, hash e projeção idênticos; depois `E`, novo ponto de consumo na chegada, mudança da demanda por ocupação, IA retomando atividade, sem produção no tile de passagem. Replay v11 intacto.
- Militar, viajante, pessoa sem produção e `Other` demandam todos os produtos; nenhuma ocupação latente do militar gera demanda dupla. Sem produtos fictícios.
- Validação de políticas incompletas, desconhecidas, zeradas ou duplicadas; demanda menor que 1 micro rejeitada sem piso implícito; overflow rejeitado.
- Projeção estável de **10 mil trabalhadores**, dois proprietários no mesmo tile, duas mercadorias e linhas de mercado agregadas, sem Nodes adicionais ou alterações no Godot.
- Censo ausente ou v10 rejeitado. Censo v11 vazio e catálogo explícito resultam em mercados vazios.

**Não implementado neste corte:** catálogo definitivo, sazonalidade/aptidão de culturas, matrizes reais de preferência por época, limpeza de conflito entre commodity e ocupação na fixture, determinação de quantidade efetivamente vendida, water-filling, preço `sqrt(D/Q)`, custo logístico, tarifa, tesouros, impostos ou demanda dos edifícios. Nenhuma bandeira de 'mercado funcionando' deve ser anunciada.

**Governança:** PR Draft empilhado sobre #383; nenhuma modificação na interface ou no core do turno; nenhuma publicação na `main` sem homologação do projeto.
