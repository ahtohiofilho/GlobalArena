# GA-VS104 — Preflight determinístico e read-only de migração de saves para V20

**Baseline:** VS103, PR #241, HEAD `05c24978468de58ad4629aaa7fa2a68dea63d7e9`, 78/78 workflows `completed/success`. **Entrega:** PR Draft empilhada na VS103, sem merge em `main`.

## Objetivo e fronteira econômica

Reforçar a segurança de migração histórica para o envelope V20 **antes de qualquer operação monetária**. O jogador continua sendo o único agente que pode solicitar explicitamente o registro da reserva vazia. A VS104 apenas prepara um diagnóstico read-only, sem alterar o jogo vivo e sem gravar arquivos.

- Nenhum caixa de compra, carteira ou saldo monetário populacional.
- Nenhuma receita real, monetização de propostas VS99/100, emissão, tributação, crédito, débito, dívida ou `SETTLE`.
- O módulo `PhysicalFoodCashMarketState` continua legado e é comparado byte a byte na projeção do save; não é promovido a modelo financeiro.
- A reserva V1 continua vinculada ao mundo e restrita a saldo gastável 0 e recibos 0.

## Contrato

`CanonicalDormantTreasuryMigrationPreflight.Inspect(archive)`:

1. restaura o save físico original pelo replayer canônico atual, incluindo hash, mundo, checksum, ações e comandos de transporte;
2. reexporta a sessão original sem mudanças e a restaura **outra vez em uma sessão isolada**;
3. se o save for V9–V18, simula `EnableDormantTreasuryReservation()` apenas nessa cópia; se já for V20, não ativa novamente;
4. constrói um candidato V20 e compara o payload completo (ações, trabalhador, estoques, ciclos, caixa alimentar legado, viagens, transporte), retirando da comparação apenas o overlay administrativo esperado: revisão +1, hash v19 e reserva V1;
5. restaura o candidato V20 independentemente, compara eventos e hashes e confirma replay byte-idêntico;
6. retorna somente um relatório de versões, hashes, revisões, marco da ativação e digests SHA256 dos archives. **Não retorna o JSON candidato, não escreve em disco e não modifica a fonte.**

Relatório:
- `SourceEnvelopeVersion`, `PhysicalPayloadVersion`, `CandidateEnvelopeVersion`;
- `HistoricalActionCount`, `ActivationAfterActionCount`;
- `SourceWorldRevision`, `CandidateWorldRevision`;
- assinatura do mundo, hashes antes/depois e SHA256 dos bytes da entrada/do candidato;
- `AlreadyReserved`, `RequiresExplicitPlayerRegistration` e invariantes de ausência de escrita/monetização.

## QA

`GaVs104DormantTreasuryMigrationPreflightTests` cobre V9–V14 com sessão real e turno, histórico físico avançado, reabertura de V20 sem ativação dupla, preservação de `FoodCash` e eventos históricos, repetibilidade determinística e rejeição de corrupção mesmo quando o checksum é recalculado.

`.github/workflows/ga-vs104-dormant-treasury-migration-preflight.yml` exige checkout no HEAD exato da PR em Linux e Windows, regressões VS101/VS102/VS104 e builds Godot Mono. A homologação exige **todos os workflows efetivamente disparados no último HEAD em `completed/success`**, incluindo os legados, não um contador de sucessos de outro commit.

## Não objetivos e próximo passo

A VS104 não conecta automaticamente o relatório a saves locais nem habilita a tesouraria pública. Uma próxima evolução poderá exibir o relatório em UI ou auditar uma migração efetivamente solicitada pelo jogador. Qualquer lançamento econômico real só poderá ser implementado depois de uma ratificação específica da política monetária, autoridade de receita, escala de conversão e semântica idempotente, preservando o poder de compra abstrato.
