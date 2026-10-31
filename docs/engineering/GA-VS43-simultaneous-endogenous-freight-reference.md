# GA-VS43 — Referência de quantidades comerciais simultâneas e frete congestionado

**Data:** 31/10/2026. **Base:** GA-VS42 PR #148, head `31521562`, 8/8 workflows aprovados. **Status:** laboratório opt-in em PR draft empilhado, sem ativação na `main`.

## Por que esta etapa existe

A VS42 coordenou alternativas **com quantidades físicas fixas**. A economia emergente exige decidir também **quanto produzir/comercializar**, considerando o valor marginal da demanda, os custos de reserva da produção e a saturação compartilhada das linhas de transporte. Essa é uma comparação simultânea: redistribuir carga altera o custo de transportar as demais remessas, e desistir de uma venda pode ser economicamente correto.

## Contratos reutilizados

`ExperimentalSimultaneousFreightReference` aceita exclusivamente:
- `EconomicProductionDemandResult` com oferta e demanda por fonte/destino e mercadoria;
- `FoodCommodityCatalog` para converter fluxos em gramas, sem inventar massa;
- `EconomicTurnCommodityValueOffer` para valor **integrado**, e `EconomicTurnSourceReservation` para custo dos produtores;
- `ExperimentalSimultaneousFreightLane` com serviços alternativos completos do contrato VS42 (em cenários reais, construir via `FromBuiltOffer`, após validar a rede física/tecnologia e os endpoints);
- função de saturação compartilhada `ExperimentalSharedFreightCongestionPolicy` que substitui, sem duplicar, a saturação individual hipotética VS40.

**Não** cria frete se a relação econômica não tiver serviço oferecido, não altera a conectividade marítima, não acrescenta novos comandos ao jogador e não cria estoque físico persistido.

## Modelo de referência: quantidade e serviço como variáveis

Para cada linha econômica `i`, as alternativas são: **não comercializar** ou escolher uma combinação de serviço `s` e quantidade física `q_i` numa grade discreta. A quantidade respeita oferta disponível e demanda restante. Compartilhamentos de fontes e destinos são validados **conjuntamente** antes de se avaliar a hipótese:

```
Σ_i originando da fonte f e item c q_i <= oferta(f,c)
Σ_i chegando ao destino d e item c q_i <= demanda(d,c)
```

Cada lote negociado usa carga `q_i * GramsPerFlowUnit` em **microgramas**, pois `q_i` está em 1/1.000.000 de unidade física de fluxo. O mercado considera a derivação de valor integrada por destino e mercadoria, uma única vez, inclusive com múltiplos produtores concorrendo. O objetivo é:

```
retorno_líquido =
   Σ_destino,item valor_integrado(quantidade_entregue)
 - Σ_origem,item (quantidade * reserva_produtor)
 - frete_geográfico_operacional
 - saturação_compartilhada(Σ_carga_em_cada_corredor)
```

Aritmética monetária **exata**, frações racionais assinadas para o retorno, sem arredondar valores econômicos em `double` e sem penalidades inventadas por bioma ou tempo de movimento.

### Três protocolos comparados

- **Independente:** cada linha escolhe seu serviço e volume mais vantajoso isoladamente, reparando limites globais de oferta/demanda em ordem determinística. Não é decisão global ótima.
- **Coordenado:** melhoria local determinística, permitindo **alterar quantidades, modalidade de serviço ou deixar de negociar**. Aceita apenas aumento estrito do retorno líquido; não tem garantia de máximo global.
- **Exato:** enumeração finita de todas as combinações **apenas se o número de configurações não exceder `maxExactConfigurations`**. Essa é a referência auditável de pequeno porte. Retorna nulo quando não pode certificar.

A grade de busca `rawQuantityQuantum` define a resolução econômica da exploração. O limite técnico de **24 quanta por linha** existe somente para proteger a busca finita deste laboratório; **não limita capacidade física da infraestrutura, volume comercial real nem o modelo final**. A avaliação de mercados com carga elevada será feita por políticas escalares futuras, sem explosão combinatória.

## Fronteiras de escopo e decisões adiadas

1. Esta é uma **referência simultânea de fluxos de mercadoria** mas não é solução de equilíbrio geral com orçamento monetário de todos os consumidores. O orçamento privado e o ajuste alfa de demanda permanecem responsabilidades futuras e exigem contrato financeiro explícito.
2. A curva de valor de demanda e os custos dos produtores são fornecidos como snapshots externos; **não** modela retorno financeiro ao Estado, investimentos, impostos, preços intermercados ou obrigação futura. **Sem crédito, empréstimos, juros ou garantias**, conforme GA-ADR-ECON-005.
3. As decisões são somente **hipóteses de clearing** e não liquidam, debitam estoques, alteram preços vigentes, renovam o save nem executam a economia do mundo.
4. Serviços continuam de **uma única tecnologia e itinerário previamente validado**. Não há transbordo gratuito, navios ou automação de ordens militares.
5. O custo de serviço, sua dispersão e a função de saturação ainda são hipóteses de laboratório, sem afirmar que o objetivo equivale ao bem-estar social completo.
6. Não transformar as fichas militares 1–5 em micromanagement, nem usar pontos de movimento no custo monetário de frete.

## Benchmark

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs43-simultaneous-freight
```

Amostras sintéticas reproduzíveis para **4, 8 e 12 linhas econômicas**, com quatro polos de demanda, fontes distintas, dois serviços por linha, componentes geográficos, uma via compartilhada e outras vias de desvio. A solução exata é calculada no caso de **4 linhas** (81 combinações); casos maiores usam heurística e **não são homologados como ótimos**. São medidas observacionais de milissegundos, alocações e número de hipóteses, não thresholds de produção.

## Critérios de QA

- Remessas com mesmo destino não ultrapassam demanda, nem vários destinos podem ultrapassar a oferta do mesmo produtor.
- A resolução pode selecionar quantidades menores que a oferta, inclusive **zero**, quando frete e custo produtivo anulam o retorno.
- A carga fisicamente convertida modifica a saturação; os custos financeiros são avaliados por volume e corredor compartilhado.
- Invariância por ordenação de entrada, avaliação racional determinística e correspondência com auditoria exata onde viável.
- Testes VS40–VS43, regressão xUnit total no Windows, Godot Mono Debug/Release, benchmark Release no Linux, preservação de save/hash do jogo atual.

## Próximos gates de desenho

Antes de integrar à economia em tempo de jogo, precisamos de um benchmark sobre o **WorldGen real**, demanda dependente de renda/orçamento, cotação ex-ante travada por ciclo, validação independente do resultado comercial e seleção institucional explícita do clearing permitido (GA-ADR-ECON-005). Somente depois disso avaliar ativação e versionamento de save.
