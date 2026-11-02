# GA-VS96 — Absorção simultânea experimental em mercados locais e remotos

**Issue:** #227. **Baseline homologada:** VS95 PR #226, SHA `aba17ddfb83b267fff20f086cf9b9e7f40462a26` (45/45 `completed/success`). **Estratégia de promoção:** PR Draft empilhada na VS95, `main` preservada.

## Visão de arquitetura

O Global Arena tem hoje três domínios de proveniência distintos:

1. VS93: recibos **realmente executados** apenas de alimento no solver V12; para madeira e pedra, execução comercial é desconhecida.
2. VS94: capacidade produtiva **fisicamente realizável** a partir dos trabalhadores residentes, sem extração.
3. VS95: proposta **fisicamente conservativa** de uso local e envio remoto, com serviços autênticos e sem `SETTLE`.

A VS96 integra, para cada **mercado habitado** e **mercadoria**, as quantidades propostas localmente e as chegadas remotas da VS95. A soma é tratada como **absorção hipotética integral da oferta proposta**. Não existe compra, consumo, pagamento, frete liquidado ou estoque real decorrente da prévia. Capacidade não selecionada permanece não extraída, conforme VS95.

## Contrato de quantidades

`CanonicalPrimitiveSimultaneousAbsorptionTrial.FromCertifiedDisposition(session, currentSites, certifiedDispositionVS95)` exige a mesma instância de descoberta de povoamentos certificada pela VS95, mesmo mundo, rede, trabalhadores e ciclo econômico. A classe não recebe uma lista artificial de compradores, carteira financeira ou parâmetros individuais.

Para cada par `(market,commodity)` da demanda basal canônica do povoamento:

```text
PlannedLocalAbsorption = Σ ProposedLocalUseRaw de fontes no próprio market
PlannedRemoteAbsorption = Σ lot.RawQuantity com lot.Offer.Buyer == market
HypotheticallyAbsorbed = PlannedLocalAbsorption + PlannedRemoteAbsorption
```

A agregação é `BigInteger`, determinística e inclui inclusive mercados com quantidade hipotética zero. O remoto tem rastreabilidade até serviços reais autenticados, conferindo que origens, commodities, destinos e pressão de demanda coincidem com o snapshot da VS95. A validação volta à fonte para impedir sumiço/duplicação de lotes.

**Conservação por mercadoria:**

```text
Σ mercado.LocalAbsorption == VS95.Commodity.ProposedLocalUseRaw
Σ mercado.RemoteAbsorption == VS95.Commodity.ProposedRemoteDeliveryRaw
Σ mercado.TotalAbsorbed + VS95.TotalNotExtractedRaw == VS95.TotalPotentialRaw
```

Não há teto de `DemandRaw`: a quantidade absorvida pode ultrapassar a pressão basal. Isso preserva a diretriz econômica de que **toda oferta planejada encontra absorção** e o ajuste de preço deve refletir utilidade marginal decrescente, não desaparecimento de mercadorias. As frações de utilização e de destinos de produção são escolhas já representadas nas etapas anteriores, não novas regras de estoque.

## Curvas macroeconômicas apenas relativas

Em cada `market/commodity` calcula-se `CanonicalEndogenousLocalPriceComparison.Calculate(offered, demandPressure, sensitivity)` com sensibilidades **1 e 2**, ainda não ratificadas.

Cada cálculo produz:
- índice relativo do preço médio;
- índice relativo da última unidade marginal;
- índice relativo da receita bruta, integral das bandas da curva VS78.

A curva integra **oferta local + importada no mesmo mercado**: preços relativos e receita não podem ser calculados isoladamente para cada origem quando concorrem na mesma localidade. Demanda basal é intensidade de consumo, não carteira monetária, nem quantidade física máxima. A curva de receita é um índice adimensional de comparação: **não é dinheiro**, custo real de frete, cobrança a consumidores ou salário. Não agrega receita nominal entre categorias heterogêneas.

Os recibos legados de alimento executado permanecem nos campos `IndependentExecutedFoodPurchaseRaw` e `IndependentExecutedFoodPaymentMicrocredits`, separados dos valores modelados. Para madeira/pedra continuam **null** (não executado/observado) e nenhum índice hipotético é convertido em microcréditos.

## Segurança e determinismo

- `RequireCurrent` exige mesma sessão, world hash, turno, rede e prova VS95.
- `RequireSameSites` impede usar uma segunda descoberta com a mesma aparência ou política diferente.
- `VerifyIndependent` reconstrói demanda, lotes remotos, somatórios locais, mercados e curvas integralmente e compara os resultados.
- Lotes duplicados, falsos ou sem direito não são admitidos: a prévia se ancora na verificação institucional da VS95.
- Não altera o save, workers, mundo, ciclos ou reservas materiais. Não há `SETTLE`.

## Testes e homologação

- `GaVs96SimultaneousMarketAbsorptionTests`: conservação por origem/mercado/mercadoria; combinação de oferta local e importada; pressão de demanda inferior à oferta sem racionamento; ausência de oferta sem inventário; receitas marginais relativas; recibos FOOD independentes; madeira/pedra desconhecidas; prova de identidade de sites, turno e revisão de rede; determinismo do save.
- `GaVs96SimultaneousMarketAbsorptionBenchmarkTests`: 8/32/128 repetições em ciclo real fechado, com destinos/autorização de frete da VS95, 1 aquecimento e 3 medidas sem threshold artificial de CPU.
- Novo gate Linux: regressões VS40..96, benchmark e Godot Mono Release.
- Novo gate Windows: suíte integral, benchmark e Godot Mono Debug.
- Os workflows já existentes de kernel e hash multiplataforma continuam testando Linux, Windows e macOS.

A quantidade de workflows será observada **a partir dos que de fato forem disparados pelos filtros `paths`**, sem presumir 62/62; o aceite exige todas as execuções acionadas no mesmo HEAD em `completed/success`, além das etapas específicas de checkout real, testes, benchmark e builds Godot. PR segue Draft; nenhuma integração na `main`.

## Próxima decisão de arquitetura

VS97 poderá comparar **distribuição regional** de ganhos relativos, concentração por mercados e sensibilidades, como diagnóstico de acessibilidade, ainda sem converter índices em moeda. A implementação definitiva do poder de compra abstrato, da simultaneidade de decisões institucionais e do fechamento financeiro de um único ciclo permanece deliberadamente separada, conforme a arquitetura aprovada.
