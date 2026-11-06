# GA-UI-TECHNICAL-01 — Área técnica sob demanda, mapa e decisões em primeiro plano

**Data:** 06/11/2026  
**Base:** GA-UI-DIRECTION-01 (PR #276) e GA-UI-LAYERS-01 (PR #277, tecla **M** e menu de camadas homologados visualmente).  
**Estado:** implementação de UX para homologação no Windows; ainda não é aprovação de todo o painel direito.  
**Escopo:** reorganizar exclusivamente controles de interface já existentes; sem alteração em mundo, economia, movimento, save, journal ou hash.

## Por que fazer

O mapa G(18,0) e a alternância Biomas/Relevo já foram aceitos. A próxima dívida é a exposição permanente de **WORLD STATS**, **HD CALIBRATION**, **LAND +/-**, **NEXT SEED**, atalhos históricos da visão global, diagnósticos de território, `TACTICAL TILE EDGES`, controles experimentais de projeção/enquadramento e laboratórios de QA no painel direito.

As opções são úteis para engenharia, mas não têm retorno suficiente para ocupar a área do jogador continuamente. O objetivo segue **MAPA PARA INTERAGIR · TECLADO PARA ACELERAR · MENU PARA DESCOBRIR · PAINEL PARA DECIDIR**.

## Implementação

A arquitetura cria **uma seção técnica contextual por escala**, recolhida por padrão:

- **Global:** duas descrições curtas de seleção/foco ficam visíveis. Todo o restante da antiga `GlobePreview.TransferHud` (estatísticas, detalhes experimentais, seletor de cores obsoleto, atalhos e zoom antigo, calibração HD) é **movido como o mesmo nó Godot**, sem recriar nem desconectar eventos, para `GAQAStrategicTechnicalPanel`. O texto longo de presença territorial também vai para essa seção. `LAND +/-` e `NEXT SEED` deixam de ocupar o rodapé fixo do sidebar.
- **Tático:** permanecem visíveis panorama do povoamento, ciclo, estoque físico, reserva/retensão e uso material, comandos de trabalho/movimentação recolhíveis, avançar turno/ciclo e salvar/reabrir. `TACTICAL TILE EDGES`, inspeção técnica e suas opções, controles antigos de projeção/arestas/enquadramento e laboratório avançado são reparentados para `GAQATacticalTechnicalPanel`.
- Os dois botões de acesso usam rótulo **FERRAMENTAS TÉCNICAS (QA)**, não aparecem como barra fixa no viewport, e não ativam nenhum diagnóstico ao iniciar.
- **Atalhos globais S (estatísticas) e L (legenda)** devem expandir o painel técnico quando chamados. Teclas de câmera, atalhos de Biomas/Relevo **M**, seleção e mouse continuam com seus eventos preexistentes.
- A seleção do tile, deslocamento, limites estratégicos desligados, hover branco e malha física mantêm o comportamento homologado.
- Diagnósticos experimentais não passam a constituir modos de jogo, produção real ou economia abstrata por terem sido preservados no QA.

## Invariantes e segurança

1. **Nenhuma mutação canônica:** mover um Control dentro da árvore da UI não altera (P), (N), estoque, ciclos, posição, hash, save ou journal.
2. **Nenhum segundo controller:** preservar os controles já construídos, seus handlers e seus tipos; reparentar o nó original para a seção contextual. Não duplicar botões nem callbacks.
3. **Sem perda de diagnósticos:** os controles de calibração HD continuam acessíveis à engenharia sob demanda, sem aparecer no HUD principal e sem substituir o mapa físico G18.
4. **Foco no mapa:** o viewport mantém suas dimensões; nenhuma barra de ferramenta permanente é acrescentada.
5. **Ordem útil:** comandos de jogador e indicadores físicos continuam acima da área técnica.
6. **Mensagens essenciais** (falha de ação, indicadores econômicos reais) não são ocultadas em QA; avisos técnicos irrelevantes, sim.
7. **Contexto de escala:** o painel técnico global fica oculto ao entrar no tático; o técnico tático fica oculto na visão global. O estado de expansão pode permanecer enquanto não houver decisão de persistência específica — nunca será gravado no save econômico.

## Critérios automáticos

O teste Godot E2E `UiE2E.gd` valida:
- início no tático com ambos os painéis técnicos recolhidos;
- controles de borda tática e laboratório avançado dentro do painel tático;
- presença territorial, `WORLD STATS`, `NEXT SEED` e `HD CALIBRATION` no espaço técnico global, sem exclusão;
- expansão e recolhimento do painel tático; possibilidade de acionar `WORLD STATS` pelo mesmo botão antigo quando o painel global está aberto;
- alternância tático → global → tático, com turno 0 e cenas/controles preservados;
- regressão anterior de camadas Biomas/Relevo e dos comandos canônicos.

FAST inclui Linux Release, Windows Debug, testes direcionados e o E2E Godot real. Jobs `skipped` não contam como aprovação.

## Aceite visual obrigatório

Na versão Debug Windows, verificar as duas escalas, em especial:

1. **Visão tática:** mapa largo; `CAMADAS` acessível; informação econômica e comandos de ciclo/save visíveis; `TACTICAL TILE EDGES`, controles `FRAME`, `PROJECTION` e inspeção técnica ausentes da vista inicial.
2. **Expandir FERRAMENTAS TÉCNICAS (QA) táticas:** aparecem os controles de projeção, inspeção e laboratório avançado originais; ações e zoom continuam funcionando.
3. **Visão global:** globo biomas/relevo preservado, textos de foco compactos; `WORLD STATS`, `HD CALIBRATION`, `LAND +/-` e `NEXT SEED` não ocupam a tela inicial.
4. **Abrir ferramentas globais:** instrumentos antigos acessíveis e funcionais; não surgem novos territórios nem vendas fictícias.
5. **Atalhos:** `M` alterna Biomas/Relevo nas duas escalas; `S` e `L` devem revelar o painel quando necessário, sem ações ocultas.
6. **Redimensionar e rolar:** nenhuma sobreposição ou corte que prejudique a jogabilidade em tela menor.

Se a área técnica continuar visualmente ruidosa ou as informações importantes desaparecerem, **corrigir neste checkpoint de UI** antes de retomar economia/frete ou adicionar outros ícones de viewport.

## Situação da diretriz

Este incremento testa a hipótese de **painel direito focado em decisões e ferramentas técnicas sob demanda**. A quantidade e posição futura de ícones, a personalização de atalhos e a disposição final do painel continuam abertas à avaliação de ROI. Não presumir aprovação completa da UI com o resultado dos testes automatizados.

## Registro de validação visual — 06/11/2026

**Evidência:** duas capturas de execução Windows Godot Debug compartilhadas pelo usuário na revisão do PR #278, uma na visão tática e outra na visão global.

**Aceite visual das telas principais:**
- **Tático: aprovado.** Viewport amplo, globo/tile físicos, hover branco, `CAMADAS · BIOMAS`, resumo econômico, ação de ciclo e save no painel principal. Inspeção de tile, projeção e calibração experimental deixaram de ficar permanentemente visíveis; o novo botão de ferramentas técnicas está recolhido.
- **Global: aprovado.** O poliedro físico G(18,0) com biomas permanece íntegro; `WORLD STATS`, `HD CALIBRATION`, comandos de alteração experimental e outros diagnósticos deixaram de ocupar a vista inicial. O acesso `FERRAMENTAS TÉCNICAS (QA)` está visível e recolhido.
- **Preservação do fluxo:** o modo Biomas/Relevo e o hover branco já haviam sido aceitos nos checkpoints visuais anteriores. O E2E deste PR passou nos testes de abrir/recolher ferramentas e de conservar os handlers, mas o **uso manual dos controles técnicos expandidos** ainda não foi conferido em captura.

**Débito visual não bloqueante:** há repetição entre `Região estratégica 49 · foco tático` e `Foco estratégico 49 | terreno físico G(18,0)`. Reavaliar na próxima rodada de hierarquia do painel direito, sem tocar no mapa aprovado. O botão `CAMADAS` sobre terreno claro pode ganhar uma superfície de contraste compacta, condicionado a teste visual.

**Novo gate prioritário:** retornar à visão tática e executar `AVANÇAR 1 CICLO`, comprovando na UI mudança real de turno/ciclo, produção física, consumo alimentar e estoque livre por tile. Não avançar para frete comercial antes de avaliar esse percurso. Evitar interpretar a telemetria global como oferta local ou inferir venda a partir de uso material. Caso a tela mostre valores incoerentes, corrigir interface/projeção no mesmo checkpoint.

**Estado do PR:** visual principal aprovado nas duas escalas; validação manual detalhada das ferramentas técnicas e do ciclo econômico permanece aberta. PR segue Draft; FAST/E2E já aprovados no HEAD anterior. Este registro é documentação, não alteração do executável.
