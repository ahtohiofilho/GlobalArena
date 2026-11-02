# GA-VS101 — reserva inativa da tesouraria no WorldState e persistência de segmento

**Issue:** #237. **Baseline:** VS100 PR #236, SHA `9bb18bbbf5829b403b9a2dfbf0626d7341fff3c0`, 50/50 GitHub Actions homologados. **Estratégia:** PR Draft empilhada, `main` intacta.

## Decisão de escopo — migração em duas fases

O objetivo é introduzir um **domínio de tesouraria no estado canônico**, sem transformar uma projeção VS99 em moeda gastável nem quebrar históricos.

A VS101 implementa concretamente:

1. **WorldState opt-in** — `WorldState.TreasuryReservation` aceita apenas `CanonicalTreasuryReservationRuntimeState` versão 1, inativa, associada à assinatura SHA256 de um mundo físico, saldo = zero e recibos = zero. `WorldState.WithTreasuryReservation` só funciona para mundo físico com binding; todas as operações imutáveis `With*` e `AdvanceRevision` preservam a reserva.
2. **Hash de estado opt-in v19** — `CanonicalWorldStateHasher` mantém todos os bytes/formatos anteriores quando a propriedade está ausente. Quando há reserva, faz um SHA256 separado por domínio, incluindo a hash do snapshot físico anterior, assinatura do mundo, versão do domínio, saldo zero e zero recibos. Uma mudança de revisão, geografia ou economia física muda o hash v19; um mundo com reserva e um sem reserva não colidem por significado.
3. **Persistência versionada de segmento** — `CanonicalDormantTreasuryPersistenceCodec` exporta um envelope JSON separado e limitado em tamanho, com payload de versão 1, revisão física, assinatura de mundo, hash do estado **sem** reserva, hash do estado **com** reserva, saldo e número de recibos. Um SHA256 do payload protege corrupção acidental. `Restore` exige a mesmíssima base de estado original, o mesmo mundo/revisão/hash, checksum correto, serialização canônica e zero créditos/recibos.
4. **Imutabilidade da economia** — somente há reserva nula/zero. Não há `Post`, `Credit`, `Debit`, `SETTLE`, rendimento, juros, transferência ou campo de arrecadação pública. Nenhuma receita VS98/VS99/VS100 é convertida em crédito, e a população não ganha nem perde dinheiro.
5. **Compatibilidade** — `WorldState` sem reserva continua produzindo hash legado byte a byte; o novo segmento não entra automaticamente no fluxo V9–V18.

## Fronteira essencial de honestidade

**A VS101 ainda NÃO modifica `CanonicalPhysicalTacticalSession.ExportSaveJson` / `RestoreSaveJson` para incluir a tesouraria.** Isso é intencional, pois essas rotinas serializam e reproduzem o diário completo das ações e têm formatos legados V9–V18. Mudar a semântica do save completo sem uma regra de upgrade/replay e sem autoridade monetária seria regressivo. **O que efetivamente se torna persistível na VS101 é o segmento do WorldState**, restaurável sobre um snapshot físico idêntico, e o que se torna parte do hash canônico quando explicitamente anexado é a reserva inativa. A ativação automática e o save unificado serão um estágio posterior; não chamar este recurso de arrecadação estatal operacional.

O fato de o segmento usar checksum SHA256 **não** o torna assinatura digital de instituição/autorização externa. É verificação de integridade e de vinculação a estado, não autoridade de emitir moeda.

## Premissa monetária confirmada

**Populações não têm caixa de compra.** O dinheiro que futuramente entrará no Estado tem fonte abstrata **regulada pelas funções econômicas de produção, absorção, demanda e preço**, não sai de uma carteira de consumidores. A reserva V1 não muda essa diretriz. O módulo legado `PhysicalFoodCashMarketState` (contas Household/Producer) permanece intacto apenas por compatibilidade histórica; ele não informa o modelo definitivo de moeda.

As curvas VS78/VS96 e os fatores VS99 continuam de laboratório, não ratificados. Os institucionais VS98 e os identificadores VS100 não são prova de propriedade legal ou de tributos; a VS101 não mistura esses conceitos com uma tesouraria de uso real.

## Integridade

- `Reserve(WorldState)` aceita só snapshot físico com binding e ainda sem reserva.
- `WithTreasuryReservation` exige associação ao mundo físico real; se anexada, a reserva não pode ser substituída silenciosamente por outra.
- Versão wire, SHA256 do payload, serialização canônica, assinatura do mundo, revisão original e os dois hashes do estado são checados antes de restaurar.
- Corrupção com crédito positivo/recibo positivo, mundo diferente, versão não reconhecida, archive não canônico, envelope desconhecido, hash divergente ou revisão alterada é rejeitada.
- O estado vivo do jogo não é mutado pelo codec, incluindo contas monetárias de alimentos legadas.

## QA / aceite

- `GaVs101DormantTreasuryPersistenceTests`: domínio opt-in e hash v19, estado anterior intacto, roundtrip byte-estável, rejeição de adulteração (inclusive com checksum recalculado), conservação de zero, tags desconhecidas, mundos diferentes, revisão stale, preservação pelas operações imutáveis, ausência de lançamentos reais.
- `GaVs101DormantTreasuryPersistenceBenchmarkTests`: 8/32/128 repetições de reserva+hash+codec sobre sessão física real com mercado alimentar legado presente, sem mutação de save; 1 aquecimento e 3 medições com mediana, sem threshold de runner.
- `ga-vs101-treasury-persistence.yml`: checkout HEAD exato; Linux regressões econômicas dirigidas VS40–101, benchmark e Godot Mono Release; Windows suíte integral, benchmark e Godot Mono Debug; gates comuns continuam testando macOS/kernel/hash.

**Homologar somente após todos os workflows realmente disparados no último SHA chegarem a `completed/success`**, com passos confirmados. PR permanece Draft sem merge em `main`.

## Próximo marco planejado

VS102 poderá projetar a **migração de save unificado e replay** para a reserva inativa, incluindo testes de upgrade V9–V18 e proteção de diffs de hash, ainda sem `SETTLE`. A **ativação monetária** deverá ser separada, após ratificação de unidade/fatores e da autoridade para arrecadação real; não ativar emissão apenas porque o schema ou a CI passaram.
