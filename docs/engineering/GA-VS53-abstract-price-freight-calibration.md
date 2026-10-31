# GA-VS53 — Calibração explícita entre poder de compra abstrato, preços e fretes

**Base empilhada:** GA-VS52 PR #158, commit `f4bbfe3e0da085ea1702e9dcf316c1fd0ccf8f6d`, 18/18 workflows aprovados. **Estado:** laboratório, PR draft sobre VS52; `main`, mundo ativo, UI e saves preservados.

## Objetivo

Integrar em **comparação de cenários somente leitura** três grandezas que antes podiam ser numericamente incoerentes:

1. capacidade agregada abstrata por **trabalhador residente produtor/consumidor** (VS52);
2. preço postado de mercadoria por **unidade inteira de fluxo**;
3. previsão de custo logístico entregue por **unidade inteira de fluxo**.

A calibração **não é uma moeda de posse da população**. Não há carteira, salário, depósito, balanço privado, crédito ou renda advinda de venda futura. A reserva `H` é consumo invisível protegido, não uma conta bancária.

## Contrato

`ExperimentalAbstractPriceFreightCalibrationPolicy` contém identidade e versão próprias, **ID e versão da política VS52** à qual está vinculada, ID do numéraire nominal e **um único multiplicador positivo racional** `abstractUnitsPerNominalQuoteUnit` para bens e fretes. A conversão é `preçoAbstrato = preçoNominal × multiplicador`. Mudança de multiplicador é ensaio comparativo de precificação relativa ao poder aquisitivo, não emissão ou transferência de dinheiro.

`ExperimentalAbstractNominalDeliveredQuote` exige `EconomicPointId`, `CommodityId`, preço nominal, previsão nominal de frete e ID de numéraire. O calibrador rejeita citações de unidade incompatível, linhas ausentes, duplicadas ou inesperadas. Não existem taxas distintas para mercadorias e frete: ambos compartilham a mesma escala explícita. Nem o preço nem o frete são automaticamente derivados dos créditos do V14.

`ExperimentalAbstractPriceFreightCalibrator.Project`:

- valida o vínculo exato com a política VS52 e a cobertura dos compradores físicos projetados;
- normaliza os preços e as estimativas de frete **com frações BigInteger exatas**;
- reconcilia orçamento bruto = `H` + orçamento disponível e custo entregue = mercadoria + frete;
- invoca `ExperimentalCashDemandAlphaPlanner` da VS45 **apenas para projetar quantidades**, verificando a reconciliação entre custo agregado e baseline calculada pelo planejador;
- expõe `CanAuthorizeRealSettlement = false` **incondicionalmente**, incluindo cenários sem serviço logístico declarado; conta quantos serviços transportadores ainda não têm titularidade financeira (VS52).

### Exemplo comparativo, não balanceamento definitivo

Um único povoamento físico com oito trabalhadores: 8 × 5 = **40** unidades de compra abstrata, com `H = 5` e disponível = **35**. Quatro unidades solicitadas de bens com preço nominal de 4 por unidade + previsão de frete nominal de 1 por unidade. Multiplicador explícito de **2 unidades abstratas por unidade nominal**:

| Grandeza por ciclo | Unidades abstratas |
| --- | ---: |
| Mercadorias (4 × 4 × 2) | 32 |
| Frete previsto (4 × 1 × 2) | 8 |
| Custo entregue estimado | 40 |
| Disponível após H | 35 |
| Alfa | 7/8 |

Com escala **1**, baseline = 20 e alfa = 1. Com escala **4**, baseline = 80 e alfa = 7/16. A capacidade dos mesmos oito trabalhadores segue em 40, com H = 5: **não foram criados ou destruídos saldos dos trabalhadores**.

Os fatores por trabalhador da VS52 continuam hipóteses de produtividade distintas por tile. A curva definitiva de produtividade, a composição de necessidades, a emissão de preços de equilíbrio e a escala econômica canônica **não** são escolhidas pela VS53.

## Limites de segurança econômica

- `ExperimentalAbstractNominalDeliveredQuote` é **estimativa ex ante**; não prova que exista rota construída, contrato, titularidade ou capacidade de frete. O custo real com saturação compartilhada VS42 pode divergir; todo lote eventualmente admitido precisará ser recalculado e auditado em unidade consistente.
- `ExperimentalBuyerCashSnapshot` da VS44 avalia custos efetivos de VS42 na escala **nominal legada**. A VS53 **não** o executa com um orçamento abstrato convertido de modo parcial. Exigir no futuro uma integração simultânea do custo logístico real, do preço e da contabilidade antes de permitir o gate financeiro VS46/VS47.
- A lista de operadores não resolvidos permanece da VS52. **Nenhum serviço é atribuído automaticamente ao comprador, ao vendedor ou a uma entidade de transporte**; `SETTLE` real fica bloqueado mesmo se nenhum operador estiver listado.
- `alpha` é projeção proporcional, não reserva de mercadoria, execução comercial, tributação, frete pago nem patrimônio monetário.
- Nenhuma alteração em `CanonicalPhysicalTacticalSession`, `FoodCash` V14, eventos, save, hash, Godot ou `main`.

## QA e benchmark

Testes VS53: reconciliação exata goods/freight/H, alfa de 7/8; escalas comparadas com orçamento físico constante; frações e frete zero; falhas de versão/unidade/âncoras/cobertura; trabalhadores viajando, com redução de demanda residente sem duplicar consumidores; serviços existentes ainda sem proprietário econômico; imutabilidade de save/hash/caixa V14.

Benchmark `--vs53-calibration` mede 8/32/128 **repetições da calibração de um único comprador com oito trabalhadores** (sem custo de worldgen nem fabricação de 128 compradores). Publica tempos e bytes alocados, sem inventar limiar de capacidade ou extrapolar para desempenho nacional.

CI: testes dirigidos VS40–VS53 em Linux, suite integral Windows e compilação Godot Mono Release/Debug. **Aprovação de testes ainda depende da execução da CI do novo commit**.

## Roadmap

A próxima etapa é ligar essa **unidade calibrada** à estimativa de frete físico real (VS40–VS42), sem custo duplicado de saturação, e acrescentar a política institucional auditável de titularidade e distribuição de receitas de transporte. Só depois reexaminar autorização e liquidação, mantendo o modelo abstrato do poder de compra como fonte canônica.
