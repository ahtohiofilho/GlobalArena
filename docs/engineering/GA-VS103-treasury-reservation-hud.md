# GA-VS103 — reserva dormente no HUD e save V20 do cliente Godot

**Issue:** #241. **Baseline:** VS102 PR #240, SHA `7f0d718bb815895a197f954927ba8b15f555d26b`, 67/67 workflows homologados. **Entrega:** PR Draft empilhada sobre VS102; `main` intocada.

## Objetivo

Expor a reserva futura da tesouraria como um domínio canônico **vazio e dormente**, por ação explicitamente solicitada no HUD real do Godot, e integrar o save V20 ao fluxo de salvar/reabrir do cliente.

## Diagnóstico e ação no HUD

A seção fica dentro de **Detalhes e ferramentas de laboratório**, recolhida por padrão para não ocupar o espaço das ações de jogo.

- Antes do registro: `RESERVA NÃO REGISTRADA`; nenhuma reserva é criada no startup nem no load.
- A ação `REGISTRAR RESERVA INSTITUCIONAL VAZIA` chama somente `EnableDormantTreasuryReservation()`.
- A operação administrativa avança uma revisão canônica e preserva o diário de ações sem acrescentar evento econômico.
- Depois do registro, o HUD informa `RESERVA DORMENTE V1`, saldo gastável zero e recibos zero; o botão fica desabilitado.
- O diagnóstico declara explicitamente que não há emissão, arrecadação, tributo, transferência, débito ou `SETTLE`.

A operação não é uma ativação monetária. Não cria receita pública, não muda a demanda, não debita consumidores e não cria caixa populacional. O antigo `PhysicalFoodCashMarketState` permanece apenas como subsistema legado de compatibilidade; não define a origem monetária estatal do desenho futuro.

## Save e restore do cliente

- Quando `WorldState.TreasuryReservation` está presente, o botão de salvar grava `user://ga-vs20-dormant-treasury.json`.
- O load prioriza esse arquivo sobre saves anteriores e o reconstrói por `CanonicalPhysicalTacticalSession.RestoreSaveJson`.
- Um save V9–V18 continua sendo aberto pelas regras históricas, sem ativação automática ou conversão silenciosa.
- Após abrir um save legado, o jogador pode solicitar explicitamente o registro vazio e então salvar uma nova cópia V20. A trilha histórica de ações é preservada pelo marcador de ativação já validado na VS102.
- O sidecar da câmera continua vinculado ao conteúdo canônico do save por SHA-256 e não entra no hash do mundo.

## QA

O E2E `tools/GlobalArena.Godot/Tests/UiE2E.gd` usa os sinais reais dos controles instanciados em `Main.tscn` e verifica:

1. reserva ausente no estado inicial e botão disponível;
2. registro explícito, saldo zero, recibos zero e botão desabilitado;
3. save V20, avanço posterior do turno e restore V20;
4. prioridade do V20 mesmo quando o arquivo V14 legado ainda existe;
5. persistência do estado dormente e retorno ao turno salvo.

O workflow de E2E executa Godot Mono 4.7.2 real em diretório de usuário isolado. O gate VS103 também executa as regressões de persistência VS101/VS102 e compila o cliente Godot em Linux/Windows Debug/Release.

## Fora de escopo

Sem emissão monetária, receita pública, tributação, carteira ou caixa de consumidores, transferências, débito/crédito, bancos, juros, empréstimos, dívida, garantias ou liquidação. A reserva segue vazia e não gastável.

**Critério de aceite:** todos os workflows acionados no HEAD final em `completed/success`, com checkout do SHA exato e E2E real aprovado. PR permanece Draft, sem merge na `main`.
