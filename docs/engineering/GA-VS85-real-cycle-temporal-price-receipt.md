# GA-VS85 — Recibo temporal de preços hipotéticos entre ciclos econômicos reais

**Issue:** #203. **Base:** VS84 PR #202 SHA \`f5c69e776e853ed2ffe427425621fc82ca548bd9\` (50/50 PASS, Draft). **ADR canônico:** #188 GA-ADR-ECON-007 R2.

## Problema

A VS84 produziu feedback endógeno de preços em **duas projeções do mesmo instante físico**, portanto não demonstrava que o jogo realmente tinha passado por um ciclo econômico. Também não provava preços **transacionados**: VS78/VS82 são índices de mercado hipotéticos, sem consumo executado ou SETTLE.

A VS85 distingue rigorosamente essas duas coisas: **tempo realmente avançou no motor existente**, enquanto a cotação/produção retroalimentada continua sendo uma simulação read-only. Não se deve chamar o índice VS78 de pagamento realizado.

## Protocolo (sem mudanças no formato dos saves)

1. Na sessão atual, **imediatamente após o tick que efetivamente concluiu um ciclo econômico**, sem reatribuição física posterior no mesmo turno (último \`PhysicalEconomicCycle.MovementTurn == State.MovementTurn\`), construir um \`CanonicalLaggedProductionResponse\` auditado (VS84) a partir de trabalhadores, preços locais hipotéticos e serviços autênticos.
2. \`CanonicalCycleStampedPriceReceipt.Capture(session,feedback)\` recusa turnos sem fechamento econômico e captura em memória o hash do estado, o turno do ciclo, o índice do ciclo, as revisões de trabalhadores/rede, o número de eventos físicos, os sinais de preço e as **taxas subsequentes** por atividade já calculadas.
3. **O chamador**, não o recibo, executa \`session.AdvanceMovementTurn()\` quantas vezes forem necessárias para o próximo ciclo. Isso é o movimento/ciclo efetivo do motor, com sua contabilidade legada própria.
4. \`ApplyAfterNextSettledCycle\` exige que a **mesma instância de sessão** tenha agora o **próximo** ciclo econômico completo e seja o turno corrente, com \`Cycles.Count = initialCount+1\`. Requer hash diferente, exatamente a quantidade de \`tick\` no diário correspondente à diferença de turnos e cadeia de \`BeforeHashHex/AfterHashHex\` a partir do hash congelado. Não atravessar mutações de trabalhadores, atos não-tick, alterações de rede ou mudança de sessão/saves.
5. Os preços anteriores não são recalculados nem substituídos por preços do ciclo atual. Aplicam-se as taxas congeladas ao **novo** \`CanonicalPrimitiveSiteDiscovery\` (trabalhadores e potenciais físicos reais do período atual) através de VS83→VS82→VS81. O ciclo real e a produção hipotética têm **provas separadas**, com replay independente e invalidação após outro turno.
6. Não se adiciona memória persistente de preços ao formato de save; esse recibo é objeto restrito a uma sessão viva. O save gerado depois do tick é idêntico antes/depois de avaliar o relatório read-only.

## Regras de segurança

- Recibo sem construtor público para impedir produção de carimbo temporal arbitrário.
- Inviável atravessar mais de um ciclo; invalida-se após o próximo fechamento (evita reutilização ilimitada de preço antigo).
- Reatribuição de trabalhadores ou intervenção no network desde a captura rejeita o feedback. Mesmo que o mundo coincida após restore, a identidade da sessão deve ser idêntica.
- Não criar ciclos novos dentro de \`Apply\`; a única transição de tempo acontece externamente via \`AdvanceMovementTurn()\`.
- Prova de tempo **não significa** preço, consumo ou pagamento reais. O motor físico legado pode executar movimentos, produção e food cash conforme seu próprio contrato, mas este relatório NÃO afirma ter substituído a sua economia.
- Índice de preço relativo não se mistura com custo nominal de transporte para declarar lucro. Demanda não limita volume físico. Não existem orçamentos \`B=40\`, bancos/crédito, inventário involuntário, SETTLE nem hard freight throughput.
- O ritmo econômico adaptativo (1/1 no começo, depois variações) é respeitado porque a captura/validação verifica \`State.Cycles\`, não assume um ciclo por movimento.

## QA

Sete testes: ciclo anterior autêntico vs mesmo turno; rejeição do recibo pós-reatribuição dentro do mesmo turno; avanço real e novo ciclo com hash/journal encadeados; invalidação após ciclo extra; proibição de restauração/save; intervenção de trabalhadores; limite de carga remota; determinismo/replay e ausência de mutação na avaliação. Benchmark 8/32/128 usa **um avanço de ciclo econômico real durante a preparação**, depois mede repetidamente a aplicação read-only na mesma sessão já avançada. Gate 51 workflows, Linux VS40–85 e Windows full suite; benchmark Godot Mono Release/Debug. Homologação só após todos \`success\` no HEAD atual.

## Próxima fase

Para dar ao jogo um mercado **real** com preços observados e utilizados em ciclos posteriores, será necessário decidir separadamente qual preço é de fato confirmado pelo motor, como persistir snapshots com validação histórica em saves e como promover a decisão econômica sem confronto com o sistema legado de food cash/estoques. Nenhum desses passos deve ser inferido como concluído na VS85.
