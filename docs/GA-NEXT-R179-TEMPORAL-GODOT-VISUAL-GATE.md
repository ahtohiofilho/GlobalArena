# GA-Next R179 — Gate visual econômico temporal autoritativo

O teste R179 executa a cena real Godot 4.7.2 .NET no mundo fine-first assinado G18, no runner Ubuntu com OpenGL de software e Xvfb. Não fabrica turnos, mercadorias, trabalhadores, preços, créditos ou overlays. A tecla E é enviada como evento Godot, usando o mesmo fluxo de fechamento do usuário.

## Habilitação isolada
- Apenas a variável explícita `GA_NEXT_R179_TEMPORAL_ECONOMY_SMOKE=1` habilita a variante QA no mesmo `GaExplicitEconomicQaConfiguration.PrepareFineFirst`.
- Usa política econômica da fixture visual histórica, sem promovê-la a regra de balanceamento do jogo. Acrescenta ledger R167 vazio e `FoundationGroundEdgeTravelTimePolicy(1_400_000UL)` declarada para habilitar a fase temporal R170.
- O jogo normal, a QA R87 e outros smoke tests conservam suas configurações anteriores.

## Aceitação
1. Antes de E, mundo e trabalhadores físicos reais estão vinculados ao host, caixa sem ciclo fechado, recibo temporal ausente e label invisível/vazio.
2. A primeira tecla E gera um fechamento temporal real com replay, hash e caixa autoritativos, exibindo apenas os créditos da civilização humana através de `GetLastClosedTemporalEconomics` (R171).
3. Cada campo da UI é comparado com o respectivo valor do recibo: ciclo, crédito ao caixa, líquido de vendas, impostos, pedágios e quantidade global física claramente identificada.
4. Após a segunda tecla E, ciclo e hash mudam, o painel é atualizado e o mapa não sofre deslocamento por iniciativa da apresentação.
5. O teste salva duas capturas PNG reais, após os fechamentos, na evidência do workflow Godot (14 dias). Captura headless de um framebuffer renderizado é prova de execução visual automática, mas a apreciação humana da disposição e legibilidade ainda depende de abrir os PNGs ou o cliente interativo.

## Limites
Sem alterar fórmula, congestionamento, solver marginal, ledger ou redistribuição. Regressões obrigatórias Windows/Linux; macOS facultativo. PR Draft e sem merge.
