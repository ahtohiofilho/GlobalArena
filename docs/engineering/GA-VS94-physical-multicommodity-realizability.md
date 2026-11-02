# GA-VS94 — Realizabilidade física experimental multibem

**Issue:** #223. **Baseline:** GA-VS93 PR #220, SHA 267345edc4a1c6cbf5ccbf35edffb03fedbd9911 (59/59 homologados). **Integração:** Draft PR empilhada sobre VS93, main preservada.

## Tese de arquitetura

A VS93 autentica compras e pagamentos **executados somente de alimento** no solver V12. Madeira e pedra têm oferta potencial e demanda projetada, não vendas executadas. A VS94 acrescenta um **lote de produção fisicamente realizável**, ancorado nas mesmas evidências da VS93: sem extração efetiva, estoque, consumo, frete ou liquidação.

A VS82/VS83 já permitiam realização hipotética com declarações limitadas a 12 fontes, para estudos de preço e frete. A VS94 não as substitui: processa **todos os povoamentos vivos** e reconcilia trabalhadores, capacidade por atividade, quantidade viável e evidências de execução separadas.

## Contrato

A API CanonicalPrimitivePhysicalMulticommodityBatch.FromCertifiedEvidence(session, currentSites, certifiedVs93, utilization) exige três políticas globais com taxa física de utilização inteira 0..1000 permille: agricultura, silvicultura e mineração. Não há microcomando individual.

Para cada origem e produto primitivo, inclusive sem capacidade, computa:

    ExperimentallyFeasibleRaw = floor(PotentialRaw * UtilizationPermille / 1000)
    CapacityNotExtractedRaw = PotentialRaw - ExperimentallyFeasibleRaw
    PotentialRaw = ExperimentallyFeasibleRaw + CapacityNotExtractedRaw

Quantidade viável é um CENÁRIO FÍSICO, não inventário, produção já realizada ou venda. Capacidade remanescente é **não extraída**, e não estocada. Demanda basal é sinal e não teto de consumo, quantitativo vendido ou preço pago. Totais por mercadoria em BigInteger preservam conservação e são reconciliados com testemunho VS93.

Compras executadas de alimento permanecem em campos IndependentlyExecuted* separados das quantidades experimentais. Madeira e pedra têm compras, pagamentos e preço real **null (desconhecidos)**; índices relativos VS78 não são moeda.

RequireCurrent e VerifyIndependent exigem sessão/ciclo/hash/rede/proveniência atuais e reconstrução independente. Não há método de commit e não há limite de 12 origens.

## Exclusões

Sem SETTLE, estoques invisíveis/involuntários, preços nominais novos, lucro monetário, carteira B=40, crédito, bancos, fundação de cidades, ou modificações de worker/motor/turno/save. Não prova qual parte da produção viável será fisicamente executada ou vendida. O próximo motor deverá fechar escolhas de produção, consumo, frete e liquidação simultaneamente sob poder de compra abstrato.

## QA

- GaVs94PhysicalMulticommodityBatchTests: conservação por fonte/mercadoria; taxas 0 e 1000, arredondamento; rejeição de políticas inválidas; determinismo de ordenação; invalidação após turno/rede/sessão estrangeira; imutabilidade de save; preservação do recibo de alimento real.
- Benchmark --vs94-physical-multicommodity-batch em 8/32/128 sobre fixture de sessão multicíclo e evidência real VS93.
- Gate Linux regressões VS40–94, benchmark e build Godot Mono Release; Windows suíte integral, benchmark e Godot Mono Debug.
- **60 workflows esperados**; homologar somente com checks completed/success no mesmo SHA da PR Draft e evidências de execução real, nunca só status de monitor. main intacta.

## VS95 em perspectiva

Definir o mecanismo de seleção da quantidade **efetivamente extraída** e disponibilizada por fonte, arbitragem física entre uso local e frete, direitos institucionais e fechamento simultâneo. Não promover projeções a SETTLE antes da validação do poder de compra abstrato.
