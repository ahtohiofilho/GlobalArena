# GA-ECON-ABSTRACT-SUPPLY-01 — Origem fisica real, cotacao abstrata experimental

**Data:** 2026-11-06. **Base:** GA-CI-FAST-02 / PR #268. **Status:** read-only; sem promocao ao motor vivo.

## Decisoes prevalentes

Este incremento preserva as diretrizes registradas em `GA-CANON-DIRECTIVES-01-authoritative-product-policy.md`, acima de qualquer implementacao historica:

- `P` representa pessoas reais, residentes e viajantes da sessao.
- `N` representa poder de compra abstrato; ainda nao possui derivacao canonica integral da economia real. Deve ser passado EXPLICITAMENTE pelo chamador como experimento, sem inferir saldos de microcreditos.
- `D=P*N`, `x=S/D`, `p=f(x)`, `R_ref=p*D`. D e sinal economico; nao e quantidade maxima vendavel, nem quota de consumo.
- A funcao `f` ainda nao foi escolhida para o runtime. Nenhum coeficiente novo e aprovado por testes numericos.
- Regras `LocalCashSettlement`, `FoodDemandRawPerWorker` e `AdaptiveThirty` continuam exclusivamente historicas no Godot; nao sao a diretriz final.
- A cadencia futura permanece vinculada a mobilidade/tecnologia, sem ativacao ou constante inventada neste incremento.

## Implementado

`CanonicalAbstractExecutedSupplyShadow.FromLatestRealCycle` exige o fechamento fisico canonico autenticado em `CanonicalExecutedPhysicalCycleProvenance`, com hash atual, ultimo tick executado, fontes identificadas por tile, mercadoria, atividade e canal de producao. O consumo de viagem nao e falsamente convertido em oferta local.

A API recebe os parametros experimentais `commodity`, `explicitlyRetainedOfProductionRaw`, `rawUnitsPerEconomicUnit`, `candidatePurchasingPower` e `experimentalPriceCurve`. Nao existem defaults arbitrarios.

Ela reconstitui a PRODUCAO BRUTA executada da mercadoria, sem inventar mercadorias inexistentes, permite apenas retencao explicita menor ou igual a essa producao, converte unidades com escala declarada e usa o `PopulationPriceKernel` para produzir uma cotacao hipotetica. Operacoes raw acumulam em BigInteger; excesso no dominio decimal e erro explicito, nao truncamento. O mesmo valor de `D` nunca funciona como teto fisico.

O resultado expoe `ExecutedProductionSources`, `ActuallyProducedRaw`, `CandidateRetentionRaw`, `CandidateOfferedRaw`, `CandidateQuote`, `RealPopulation` e o hash/ciclo da evidencia. `RequireCurrent` e `VerifyIndependent` rejeitam observacao stale, sessao estrangeira ou incongruencia dos totais.

## Limite epistemico

Uma producao **executada** nao comprova que a mercadoria foi efetivamente ofertada ao mercado: alimento pode ter sido consumido no mesmo ciclo; pedra e madeira podem estar retidas em estoque ou reservadas para infraestrutura. A `CandidateOfferedRaw` e uma **hipotese**, jamais uma retirada do estoque ou recibo de venda. O total economico nao e necessariamente o saldo fisico disponivel apos consumo.

A `PopulationPriceQuote` nao cria receita realizavel e nunca movimenta caixa, estoque, salario, frete, impostos, ou compras.

A derivacao concreta de N precisa de evidencias reais do ciclo anterior, titularidade/distribuicao, pesos-base auditados e tratamento de mercadorias novas. `RealPurchasingPowerDerivation` segue candidato de indice, nao a promocao final.

**Flags explicitamente negativas:**
- `AuthenticatesActualMarketOffer = false`;
- `AuthenticatesFinalUseOrCrossCycleStockVintage = false`;
- `AuthenticatesPurchasingPowerN = false`;
- `HasApprovedPriceCurve = false`;
- `CanAuthorizeExecutedSettlement = false`.

Nao ha novos campos de save, hash, evento, UI, saldo monetario ou regra de cadencia. Saves V9–V30 continuam com seus comportamentos historicos.

## Testes e QA

`GaEconAbstractSupply01Tests` cobre:
1. Bloqueio sem ciclo efetivamente encerrado.
2. Fontes executadas, mercadoria e populacao reconciliadas ao recibo.
3. N mudando precos/demanda sem alterar pessoas ou oferta; S pode ser maior que D.
4. Retencao hipotetica declarada; retencao integral gera cotacao com zero oferta.
5. Uso do modo cash legado sem utilizar seus saldos e sem mutar o estado.
6. Replay com producao e cotacao identicas, hash e bytes de save preservados.
7. Invalidez do recibo apos turno e realocacao.
8. Falha segura para mercadoria ausente, unidade invalida, retencao em excesso, N invalido e curva ausente.
9. Avaliador de preco chamado uma unica vez, sem reexecutar durante auditoria.

No modelo GA-CI-FAST-02, **FAST aprova apenas escopo local**. A cada grupo de 3–5 incrementos executar `ga-ci-checkpoint`, com regresao completa, hash e benchmark multiplataforma; usar `ga-ci-full` antes de merge. Nunca promover um gate skipped a sucesso.

## Caminho de promocao

Proximo incremento para motor economico:
1. Criar comando/politica versionados para declarar **oferta efetiva** e retencao real, incluindo exclusao de consumo, estoque reservado e carga de expedicao.
2. Registrar destino fisico de cada unidade ofertada; sem `min(S,D)` e sem estoques invisiveis.
3. Homologar curva f e derivacao de N baseada em evidencias do ciclo anterior, sem dualidade carteira + indice.
4. Substituir a regra ativa do Godot apenas por nova versao explicitamente escolhida, preservando saves e replays antigos.

O contrato de cadencia por mobilidade permanece em sequencia propria, nao autoriza reativar faixas de 30 turnos no novo motor.
