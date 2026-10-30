# GA-VS11 — Ritmo de ciclos econômicos adaptativo por faixa temporal

Data: 29/10/2026. Implementação empilhada sobre VS10A/PR #100. Sem merge na main.

## Motivação e decisão

No Neolítico, poucas unidades se movimentam com velocidade reduzida; exigir seis turnos de movimento para liquidar um único turno econômico prolonga artificialmente a fase inicial. Com avanço das unidades, o jogo concentra mais ações físicas no mesmo tempo de turno. A cadência econômica deve crescer progressivamente em relação ao movimento.

Regra inicial canônica: **X = 30 turnos de movimento por faixa**. É uma constante de política, não uma conclusão sobre balanceamento final.

| Turnos de movimento | Intervalo econômico |
| --- | --- |
| 1–30 | 1/1 |
| 31–60 | 1/2 |
| 61–90 | 1/3 |
| 91–120 | 1/4 |
| 121–150 | 1/5 |
| seguintes | 1/[1 + floor((turno-1)/30)] |

Os ciclos são acionados quando o número de turnos transcorridos **desde a última liquidação** alcança o intervalo exigido para o turno atual. A contagem não é zerada na troca de faixa; a regra evita congelamento mesmo quando o intervalo ultrapassa X. Marcos testáveis: 30º = 30 ciclos; 31º = 30; 32º = 31; 60º = 45; 61º = 45; 63º = 46; 90º = 55.

## Modelo de implementação

- `PhysicalEconomicCadence` em Runtime concentra a regra determinística. `AdaptiveWindowTurns=30`.
- `PhysicalTacticalEconomyRuntimeState` guarda a identidade da política imutável e valida se o histórico de ciclos contém todas e somente as liquidações devidas.
- `CanonicalPhysicalTacticalSession` decide liquidar produção/consumo pelo estado canônico e pela política selecionada; sem produção fantasma e sem duplicar o ciclo na fronteira.
- O Godot inicia **novas partidas** com `AdaptiveThirty`, exibe a razão econômica da próxima jogada e salva em `user://ga-vs11-adaptive-physical.json`.
- Eventos, hashes, estoques, trabalhadores e consumo continuam no mesmo WorldState. A cadência não muda a produtividade **por ciclo**; muda quando os ciclos são executados.

## Compatibilidade forte

- Partidas históricas V9 continuam com `LegacyFixedSix` (1/6) e hash formato 9 idêntico. Saves V9 podem ser abertos, auditados e continuar com a política histórica. Não são reinterpretados usando regras novas.
- Novas partidas adaptativas usam **hash formato 10** (tag de regra e X=30) e **save formato 10**, com validação por replay e isolamento em outro arquivo, evitando sobrescrever V9.
- Ao reabrir, prioridade: save adaptativo V10 → save canônico V9 → save VS07 com migração histórica. Saves superiores corrompidos **não** caem silenciosamente para um formato anterior.
- Para usar o ritmo adaptativo sobre uma partida V9 existente será necessária migração explícita de política, com mudança de regras e revisão/histórico próprio. Essa migração **não** está implementada neste incremento.
- O nome do arquivo inclui `vs11` para assegurar isolamento dos saves homologados do VS10A.

## Gates

QA Linux e Windows: faixas exatas, fronteiras 30/31, 60/61, 90/91, persistência V10 e hashes, preservação do V9, recusa de timeline impossível, continuidade depois da faixa 31, regressões VS07–VS10A, compilação do cliente Godot. Gate de hardware Windows/Intel UHD: observar HUD 1/1, 1/2 e 1/3, sem regressão visual; testar V10 save/reload.

Sem mercado, transporte, UI territorial nova, instalação neolítica obrigatória, bancos/crédito, combate ou mudanças de terreno. Divergência WorldGen visual 01C vs economia canônica permanece pendência visual independente. MKT-01 bloqueado.
