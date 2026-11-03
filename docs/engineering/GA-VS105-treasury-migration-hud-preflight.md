# GA-VS105 — Diagnóstico real de migração V20 no HUD Godot

**Baseline:** VS104 PR #242, HEAD `7828e60621bcfc1756fef6c337a015e7e1c444f2`, 54/54 workflows `completed/success`, incluindo kernel e hash em macOS 15. **Entrega:** PR Draft empilhada diretamente sobre VS104; sem merge em `main`.

## Objetivo

Expor o preflight read-only `CanonicalDormantTreasuryMigrationPreflight.Inspect` da VS104 na interface real do Godot, sem promover nenhum registro financeiro. O usuário pode clicar em **INSPECIONAR SAVE PARA V20 (SOMENTE LEITURA)**, na seção `Detalhes e ferramentas de laboratório`, que permanece recolhida por padrão.

## Contrato e fluxo

1. O botão consulta o mesmo arquivo que **REABRIR SAVE** abriria, de acordo com a prioridade já existente: V20, viagem remota, civilização V14, movimento V13, mercado V12, geografia V11, V10, V9 e, por último, VS07.
2. **Não há fallback** para um save anterior quando um V20 prioritário está corrompido; a consulta falha sem alterar o jogo. Para VS07, exibe orientação para a migração histórica separada (não propõe V20).
3. A inspeção lê somente os bytes do arquivo e chama o replayer VS104 em sessões isoladas. Valida assinatura do mundo em relação ao jogo ativo.
4. Mostra versão original, destino V20, marco da reserva, contagem de ações, revisões, e prefixos dos SHA-256 dos archives. O log real imprime um marcador `GA_VS105_SAVE_MIGRATION_READ_ONLY_OK`.
5. Fonte V9–V18: avisa que **o registro continua exigindo ação explícita** do jogador. Fonte V20: registra diagnóstico de reserva já presente, sem segunda ativação.
6. A operação verifica hash, revisão, ações e exportação da sessão viva, além do conteúdo do arquivo original após a consulta. **Não grava nada**, não toca os sidecars de câmera e nunca fornece o candidato V20 a um caminho de escrita.
7. Ao salvar, reabrir ou alterar o estado canônico, os diagnósticos anteriores são invalidados. Uma nova inspeção é necessária para atualizar a evidência.

## Experiência e limites

O painel avançado é opcional e escondido por padrão para preservar o viewport tático. Uma falha aparece como `PRÉ-VALIDAÇÃO INDISPONÍVEL (PARTIDA E ARQUIVO PRESERVADOS)`, sem ativar a reserva nem carregar outro arquivo automaticamente.

**A tesouraria segue dormente:** saldo gastável zero, recibos zero, nenhuma emissão, tributação, liquidação, receita real, débito de dinheiro populacional, carteira, banco ou crédito. A economia populacional futura usa poder de compra abstrato. `PhysicalFoodCashMarketState` continua legado de compatibilidade, não fundamento do modelo final.

## QA

- `tools/GlobalArena.Godot/Tests/UiE2E.gd` usa os **controles Godot instanciados e seus sinais reais**, no diretório de dados isolado.
- O cenário V14 verifica que o arquivo original permanece byte-idêntico, a reserva continua não registrada e o turno permanece igual.
- O cenário V20 verifica que a consulta não duplica a reserva, não altera saldo/recibos, não grava o arquivo e preserva o turno.
- Gate `.github/workflows/ga-vs105-treasury-migration-hud.yml`: checkout no SHA exato; testes VS102/VS104 e builds Godot .NET Linux/Windows; Godot Mono 4.7.2 real com hash do binário verificado, importação e E2E SceneTree na Linux.
- Demais workflows da PR, incluindo `ga-qa-ui-e2e-001` e regressões, deverão terminar em `completed/success`. O macOS 15 segue fixado nos gates kernel/hash da VS104.

## Fora do escopo

Sem seletor de arquivos universal, upgrade automático, sobrescrita de saves, confirmação de migração, realocação econômica, emissão, `SETTLE`, arrecadação pública, receitas efetivas ou alteração dos dados físicos. O botão de registrar reserva continua sendo o único comando institucional explícito.

**Aceite:** todos os workflows acionados no HEAD final aprovados; duas testemunhas de E2E V14/V20; PR Draft; `main` intocada.
