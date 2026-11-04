# GA-VS109 — Separar referência de preço, consumo histórico e nutrição

**Natureza:** refatoração sem mudança de regras de jogo. Base: VS108 PR #247.
**Diretriz:** a demanda é uma referência econômica de **preço ótimo quando oferta/referência = 1**, não uma quantidade exata que o agente tenha de consumir. Consumo efetivo pode ultrapassar a referência. Nutrição e seu efeito na eficiência são outro contrato, separado, com parâmetros próprios.

## Problema rastreado

`CanonicalPhysicalTacticalSession.AdvanceMovementTurn` V9–V20 usava
uma mesma variável `demand` para:

1. pressionar a cotação do preço local;
2. limitar fisicamente o consumo por `Math.Min`;
3. salvar a diferença em `FoodUnmetRaw`, facilmente confundida com fome.

Isso **não** implementa a economia escolhida. Entretanto, apagar o limite
sem regra substituta alteraria o mercado, a liquidação, contas, hashes,
fixtures e a semântica de saves anteriores.

## Fronteiras introduzidas

| Contrato | Significado | Limita consumo? |
|---|---|---|
| `PhysicalFoodPricePressureReference` | Referência positiva de pressão para preço. `RawPressure` pode variar independentemente do consumo, como sinal econômico | **Não** |
| `LegacyPhysicalFoodIntakeTarget` | Quota **apenas** dos rulesets históricos V9–V20; tem métodos explicitamente nomeados `DeliverDirect`, `LimitLegacyQuotedPurchase`, `LegacyResidual` | **Sim, exclusivamente para replay legado** |
| `FoodConsumedRaw` / receipts físicos | Alimento efetivamente consumido/registrado | Não estabelece índice nutricional |
| **Futuro: nutrição/eficiência** | Per capita, quantidade consumida, diversidade e fatores de atividade, com avaliação no ciclo posterior | Não inferível de `FoodUnmetRaw` |

O valor histórico de ambos os primeiros contratos era `500_000 raw`
por residente por ciclo, **por coincidência do modelo anterior**. Eles
possuem constantes e tipos distintos; identidade numérica nos saves antigos
não é identidade conceitual. O método público `FoodDemandAt` e a
constante `FoodDemandRawPerWorker` são **aliases de compatibilidade da
quota**, proibidos para novas regras de preços e nutrição.

O motor de cotação agora chama `FoodPriceReferenceAt`; o consumo
histórico chama `LegacyPhysicalFoodIntakeTarget`; o `AdvanceMovementTurn`
não possui mais uma variável genérica `demand` usada para ambos.

## O que não foi alterado

- Algoritmos, quantidades consumidas e aquisições de V9–V20, inclusive
  compras de suprimentos de viagem, livros de caixa, save/hash, mercado
  de origem, ciclos e trabalhadores.
- Curvas VS78 de preço marginal com retornos decrescentes; elas já
  tratam pressão como sinal, não teto de absorção.
- O coeficiente `500_000` no replay antigo; não foi validado como
  ótimo econômico, valor nutricional ou regra futura.
- Nenhuma função de nutrição foi ativada, porque o estado não
  possui ingestão por variedade por indivíduo, parâmetros validados
  e política autorizativa de eficiência do próximo ciclo.

## Próximos critérios antes de substituir o legado

1. Definir o contrato **de consumo endógeno não limitado por pressão
   de preço**, com compras voluntárias, oferta e meios econômicos
   sem caixa populacional fictício e sem estoque invisível.
2. Separar a escala do **ótimo de preço** da dieta desejada por
   pessoa. Pressão de demanda é uma dimensão de mercado, não
   déficit ou meta de ingestão.
3. Declarar função nutricional por consumo per capita, diversidade,
   retornos decrescentes e impacto na produção **no ciclo seguinte**,
   com parâmetros explícitos e testes de monotonicidade/limites.
4. Migrar o ruleset/save de forma versionada, mantendo replay
   imutável dos V9–V20, hashes canônicos e impossibilidade de
   inventar nutrição com base em `FoodUnmetRaw`.

## Gate de QA

`GaVs109FoodPriceNutritionBoundaryTests` cobre independência dos tipos,
influência da pressão de preço sem cortar oferta, operações legadas,
overflows e replay idêntico antes/depois do ciclo, incluindo a cotação.
FAST cobre build .NET/Godot e regressões. Checkpoint completo permanece
disponível separadamente (`ga-ci-full`).

Nenhum GPP e nenhum avanço silencioso de funcionalidade nutricional.
