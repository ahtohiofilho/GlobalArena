# GA-VS102 — Save físico canônico V20 com transição histórica de reserva de tesouraria

**Issue:** #239. **Baseline homologada:** VS101 PR #238, SHA `9c4cf9f632a6f90c311fb832ad8efdd6e51d5021`, 60/60 workflows. **Entrega:** PR Draft empilhada em VS101; `main` intocada.

## Premissa institucional e econômica

**Populações não possuem caixa monetário que pague suas compras.** A receita monetária estatal será gerada abstratamente, porém regulada pelas funções econômicas de oferta física, demanda e preço, e destinada conforme direitos institucionais a ratificar. O módulo legado `PhysicalFoodCashMarketState` preserva compatibilidade histórica, sem ser promovido ao desenho final. A reserva estatal implementada na VS101/VS102 continua **inativa, sem moeda gastável, sem recibos e sem SETTLE**.

## Por que V20, sem mudar payload V9–V17

A VS101 já introduziu `WorldState.TreasuryReservation`, hash v19 opt-in e codec separado. A VS102 integra explicitamente a reserva ao **mesmo envelope do save da sessão física**, com marcador de ação que preserva o histórico:

- O `CanonicalPhysicalSavePayload.Version` continua representando o **modo físico** original, V9 a V17. Cada modo continua a usar suas mesmas regras de geração, movimentação, acampamento, transporte e consumo.
- O `CanonicalPhysicalSaveEnvelope.Version` passa a valer **20 apenas quando a reserva está ativa**. Uma sessão sem reserva mantém envelope V9–V17, ou V18 caso o diário institucional de transporte esteja presente, com JSON e hashes anteriores intocados.
- A propriedade opcional `TreasuryReservation`, omitida quando nula por `JsonIgnoreCondition.WhenWritingNull`, traz `SchemaVersion=1`, assinatura do mundo, saldo 0, recibos 0 e `ActivatedAfterActionCount`. O envelope V20 pode incorporar o diário de transporte da V18, sem descartar os comandos.
- A assinatura de mundo permanece **hexadecimal minúscula**, conforme o contrato `RuntimeWorldBinding`. Os hashes do mundo/estado usam o padrão herdado e foram preservados sem conversões implícitas.

## Transição histórica exata

`CanonicalPhysicalTacticalSession.EnableDormantTreasuryReservation()` é a única entrada explícita. É idempotente, só cria a reserva em um mundo físico legítimo e registra a quantidade `N` de ações já concluídas. A operação **avança uma revisão do WorldState**, como exigido por `CanonicalGameSession.AcceptSnapshotIfCurrent`, mas **não cria uma ação econômica**: é uma transição administrativa de domínio de estado.

```text
Ações anteriores a N:  hashes/revisões legados, imutáveis.
No marcador N:          anexar reserva inativa e avançar uma revisão.
Ações posteriores a N: hashes do estado com domínio v19.
Save V20:               dados físicos antigos + marcador N + reserva zero.
Restore V20:            iniciar no mundo histórico, replay de N ações,
                        ativar reserva no mesmo marcador e replay posterior.
```

A restauração valida checksum, formato, presença exclusiva da reserva no envelope V20, índice `0 <= N <= Actions.Length`, versão da reserva, mundo e ausência de moeda/recibos. Depois aplica os comandos de transporte em seus respectivos marcos, verifica as revisões e hashes de todos os eventos e, ao final, compara o payload serializado ao **reconstituído integralmente pelo motor**. Saves adulterados, com reserva extra escondida em V18, `N` deslocado ou valores financeiros inventados, devem falhar.

Este desenho permite **migrar explicitamente um save legado já aberto**: restaurar o save antigo, ativar a reserva, exportar V20, sem editar retroativamente nenhum evento histórico. Não é uma migração automática de arquivos V9–V18; o modo antigo permanece válido e produz os mesmos dados quando a opção não é ativada.

## Por que a ativação não é monetização

A reserva V1 continua:
- saldo gastável = 0, recibos = 0 e nenhuma operação `Post/Issue/Debit/Credit/SETTLE`;
- sem cartão, carteira, dinheiro ou saldo populacional;
- sem autoridade fiscal presumida, titularidade estatal automática ou conversão de preço de laboratório VS99;
- `Game.Snapshot.TreasuryReservation` é domínio canônico de estado; a transferência de dados via V20 **não** significa que a receita projetada VS99/VS100 foi liquidada.

A propriedade `WorldState.TreasuryReservation` v19 e o codec de segmento VS101 continuam disponíveis para validação independente; agora V20 permite restore de sessão física com replay de ações antes/depois do marcador. Não exclui formatos históricos e não transforma V18 em formato obsoleto.

## Plano de QA

**Testes** `GaVs102IntegratedTreasurySaveTests`: (i) save legado inalterado; (ii) ativação após ações históricas; (iii) ativação antes da primeira ação; (iv) novos turnos pós-ativação e pós-restore; (v) migração explícita de save antigo; (vi) corrupção com checksum refeito (créditos, recibos, schema, mundo, N deslocado, ausência de reserva, hash final falso), (vii) tentativa de contrabando de reserva em versão antiga, (viii) transporte institucional preservado, (ix) consistência das contas legadas de alimentos no mesmo snapshot V20 entre save/restore. O benchmark captura essas contas **depois** do turno que altera legitimamente o mercado legado; não presume que o módulo histórico seja imutável entre turnos.

**Benchmark** `GaVs102IntegratedTreasurySaveBenchmarkTests`: 8/32/128 restaurações do save V20 **inteiro**, incluindo replay de ações reais, uma amostra aquecida e três medidas, sem limiar artificial de CPU. Pode ser mais lento que o segmento da VS101, pois é um replay de sessão completa.

**CI** `.github/workflows/ga-vs102-integrated-treasury-save.yml`: checkout SHA exato no Linux/Windows, regressões dirigidas VS40–102 no Linux, suíte integral Windows, benchmark V20 e builds Godot Mono Release/Debug; gates kernel e hash multiplataforma incluem macOS. Aceitar apenas quando **todos** workflows efetivamente acionados no SHA final terminarem `completed/success`, e a PR permanecer Draft, sem merge na `main`.

## Próximo marco

A VS103 poderá integrar diagnóstico visual de tesouraria inativa no HUD, auditoria de save V20 e validação de migrações entre versões por matriz mais ampla, antes de permitir qualquer operação monetária. A futura arrecadação estatal executável dependerá de ratificar **unidade monetária, função de calibragem, direitos institucionais e semântica de lançamento atômico e idempotente**, sem caixa de consumidores.