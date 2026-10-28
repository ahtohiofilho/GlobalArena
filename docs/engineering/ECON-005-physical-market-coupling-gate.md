# GA ECON-005 — Gate E3: mercado multivariedade e custos físicos compartilhados

**Estado:** cenário experimental em branch empilhada no PR #44. Sem mutação do turno, catálogo de itens, liquidação, instituições bancárias ou crédito.

## Por que este incremento existe

O PR #44 soluciona preços de origem e quantidade consumida para um *suporte interior* de múltiplas variedades com **tarifas comerciais OD congeladas**. O PR #40 calcula o custo físico agregado e não linear de recursos logísticos. E o #42 pode mapear serviços geográficos verificados a recursos físicos. Falta verificar se uma escolha de tarifa e seu correspondente mercado sustentam **as cargas físicas que geraram aquela tarifa**.

`EconomicInteriorPhysicalMarketProbe.Evaluate` executa um ciclo determinístico, somente hipotético:

```text
tarifa_OD(t)
    -> EconomicMultivarietyInteriorClearing (preços de origem + q + H)
    -> cargas microgramas em EconomicPhysicalResourceId
    -> C_r(L) real com intervalo racional do Gate C1
    -> tarifa média hipotética C_r(L)/L por recurso
    -> tarifa_OD(t+1) amortecida
```

Para cada par origem/destino há uma incidência declarada `EconomicReclearingQuotedRoute`, que pode ser produzida a partir do adaptador físico #42; há ainda uma massa explícita por unidade inteira de mercadoria. Os trechos compartilhados acumulam o fluxo de todos os pares e são **avaliados uma vez por recurso**, independentemente da origem da carga. Repassa-se o custo médio, não o custo integral novamente, a cada mercadoria incidente; assim se evita o erro de cobrar `C_r` duas vezes.

A política **de recuperar o custo real por tarifa média** foi escolhida SOMENTE como política experimental explícita e autoconsistente, não como previsão histórica, norma universal, tarifa da produção ou receita institucional do GA. Os `RawUnitCostPerGram` do legado não são convertidos implicitamente: o chamador fornece uma tarifa inicial em unidades de numeraire da preferência, e o resultado continua nesta unidade de referência.

## Condições e limites

- São necessárias todas as incidências OD, **n>=2**, `q[i,j]>0`, `H_i>0`, `p_j>0`; não há ativação endógena de caminhos, remoção de suporte, cantos de mercado ou mercadoria não vendida.
- A malha e os ativos **não são certificados** por esta classe; o chamador deve providenciar o caminho por um serviço verificado no #42. A existência de identificadores sintéticos em um teste não autoriza construções no mundo.
- Cada carga de mercadoria é convertida para microgramas com arredondamento determinístico ao inteiro mais próximo. Como os valores `q` do clearing são `double`, os custos físicos são limitados racionalmente no valor **arredondado**; isto **não** é certificado de precisão de liquidação, nem pode virar saldo financeiro sem contrato de unidade e arredondamento.
- Tarifas são atualizadas só entre resoluções hipotéticas, nunca durante um clearing. `EconomicTurnFreightQuote` continua congelado; nenhuma mutação silenciosa de cotação autoritativa.
- A malha sintética do teste compartilha recurso físico entre distintos pares OD. Isso comprova reconstrução de cargas, **não** uma infraestrutura construída de fato.
- O ciclo aplica amortecimento 0,35, no máximo 160 iterações externas, com tolerância de tarifa. Não convergir, perder um suporte interior, ultrapassar a precisão da massa ou violar orçamento/oferta implica **rejeição**, nunca preço fictício nem crédito.
- Condições marginais de primeira ordem, uma raiz estacionária e a reconciliação de receitas/custos **não provam ótimo global, unicidade, viabilidade social, estabilidade, capacidade física, acessibilidade institucional ou elegibilidade para execução**.
- O erro de recuperação do custo real e o desvio da tarifa são expostos ao auditor; valores financeiros futuros deverão usar unidades e contas autoritativas próprias. Nada é lançado no `WorldState`.

## Fronteira de itens e experiência do jogador

A **massa por unidade inteira** já é parâmetro da incidência; os componentes de mercado e transporte não exigem construir tipos de ferramenta, peça, uniforme ou armadura para existir. Insumos e equipamentos que não tiverem importância estratégica própria podem permanecer **implícitos**, inclusive no recrutamento/equipamento de soldados. A eventual relação entre mercadorias primárias/secundárias e objetos do jogo é uma decisão de catálogo **futura**, não feita aqui. Nenhum exemplo anteriormente citado deve ser tratado como aprovação de item canônico.

## Testes

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicInteriorPhysicalMarketProbeTests`

Verificar kernel, hashes de estado e benchmark de frete no SHA do PR em Windows, Ubuntu e macOS. Manter fora de `main` enquanto houver revisão pendente dos PRs #38–#44.

## Próximo passo, quando homologado

Avaliar suportes diferentes (incluindo caminhos frios e regimes de tarifa alternativos) de maneira finita, verificar condições de participação/utilidade e um auditor econômico independente antes de selecionar uma solução sob política institucional explícita. Somente depois pode ser preparado um commit atômico **à vista**. Nenhum banco ou crédito.
