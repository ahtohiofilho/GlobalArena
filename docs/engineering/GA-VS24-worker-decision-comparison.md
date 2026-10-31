# GA-VS24 — comparar consequências de realocação produtiva sem alterar a partida

**Data:** 31/10/2026. **Base:** GA-VS23/PR #123, com aceite Windows e CI homologados.

## Valor de produto
A VS23 já permite realocar trabalhadores (VS21), produzir/consumir ao longo de cinco ciclos (VS23) e observar falta alimentar. A VS24 conecta decisões e consequências antes do compromisso: com um tile ocupado selecionado, o usuário escolhe atividade de origem, destino e quantidade, e clica **COMPARAR REALOCAÇÃO EM 5 CICLOS (SEM APLICAR)**.

O HUD exibe duas linhas comparáveis — **Manter** (alocação vigente) e **Mudar** (redistribuição sugerida) — após os mesmos cinco ciclos econômicos, com alimento consumido, falta alimentar e proporção atendida. Exibe também estoque local de alimento, madeira e pedra resultante em cada alternativa. **A prévia é condicional**: mantém todas as demais decisões futuras constantes, não prevê a reação de outros agentes ainda não implementados. Para efetivar a troca, permanece o botão canônico **MUDAR ATIVIDADE NO TILE**.

## Implementação segura
- `PhysicalWorkerDecisionProjection.Compare` exporta o save canônico V14/V13/V9 da sessão atual **sem mutação**; restaura duas sessões temporárias por `CanonicalPhysicalTacticalSession.RestoreSaveJson`.
- Alternativa A avança cinco ciclos sem mudança; alternativa B aplica `ReassignWorkers(tile, fromJob, toJob, workers)` à cópia e avança os mesmos cinco ciclos com `PhysicalEconomicCycleProgression.AdvanceBatch`.
- Cada alternativa executa os **mesmos mercados, produção, consumo, possíveis viagens e cadência adaptativa** do runtime atual; não há fórmula de previsão econômica independente, preço alternativo ou bônus de profissão inventado.
- Comparação inclui o intervalo de turnos idêntico, agregados alimentares dos últimos cinco ciclos e **estoques do tile escolhido** no último turno da projeção. Não confundir estoque com produção bruta ou PIB.
- Se a proposta não é válida (mesma atividade, zero ou mais residentes do que disponíveis), a prévia é rejeitada; a partida real, hashes e journal permanecem intactos.
- A linha de prévia só aparece sob demanda; é descartada visualmente ao atualizar o HUD ou alterar a quantidade de trabalhadores; nunca persistida no save.
- Performance: são duas restaurações de save e dez ciclos econômicos em sessões temporárias. A ação ocorre somente quando o jogador requisita, não em cada frame, não em atualização de HUD.

## Preservação de escopo
- Continuam congeladas: issue #121 (cadência física absoluta), issues #110/#111 (macroeconomia antes de contabilidade e poder de compra endógeno), #103 (mercado multicommodity). Não alterar créditos experimentais (320), salários, contas privadas, construção, propriedade ou infraestrutura.
- UI preserva o viewport quadrado, as barras anteriores removidas e os detalhes técnicos recolhíveis. Não adiciona painel externo, cidade declarada nem nova aba.
- Saves V13/V14 permanecem legíveis/reproduzíveis, e nenhum formato ou evento novo é gerado apenas pela prévia.

## Gate Windows / QA
1. Executar o script `tools/launch-vs24.ps1` em uma nova worktree VS24; verificar título **Global Arena — VS24 | Comparador produtivo** e log `GA_VS24_RUNTIME_ACTIVE`.
2. Inspecionar povoamento inicial 4 agricultura/2 madeira/2 pedra. Selecionar origem **Agricultura**, destino **Madeira**, quantidade **2**. Clicar **COMPARAR REALOCAÇÃO EM 5 CICLOS (SEM APLICAR)**.
3. Confirmar que há alternativas **Manter** e **Mudar**, indicadores alimentares e estoques finais do tile. Turno continua **0**, ciclos continuam **0**, trabalhadores continuam **4/2/2**.
4. Aplicar a mudança pelo botão preexistente **MUDAR ATIVIDADE NO TILE (SEM VIAGEM)**. Agora a distribuição deve virar **2/4/2**, ainda no turno 0. Avançar cinco ciclos reais e confrontar com os valores da linha **Mudar** da prévia (desde que nenhum outro comando intervenha).
5. Salvar, avançar ou realocar, reabrir e verificar volta do save; a projeção hipotética não deve ser regravada como ação ou dado permanente.
6. CI Windows/Linux: testes VS24, VS23, VS22, VS21, VS20; equivalência das duas cópias com avanço manual, cenário pós-trânsito cruzando turno 30, save V13, conservação de moeda/população, hash da sessão real invariável e Godot Debug/Release.

**Governança:** PR draft empilhada na VS23/PR #123, sem merge na `main` até fechar a integração da cadeia.
