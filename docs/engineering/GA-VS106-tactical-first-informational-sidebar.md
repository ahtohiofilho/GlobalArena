# GA-VS106 — Tático primeiro e coluna lateral informacional R1

**Base:** PR #244, checkpoint transversal UX-VIS-01 (somente documentação), sobre VS105 PR #243 homologada com 79/79; PR VS106 empilhada em Draft. Sem merge em `main`; GPP e milestones do roadmap permanecem intactos.

## Hipótese de produto

O jogo é um Grand Strategy em **superfície planetária esférica**: as decisões e os acontecimentos operacionais partem dos tiles táticos reais; o globo estratégico é um recurso auxiliar crucial para navegação, escala e identidade do mundo, não uma tela principal concorrente. A interface deve expor **mais informações canônicas em menos espaço**, sem o caráter visual de um laboratório arcade.

## Entrega delimitada

1. **Entrada tática real:** o Godot já cria a sessão física canônica, então a primeira tela agora abre o bucket estratégico associado ao **tile fino do povoamento inicial**. Utiliza `CanonicalPhysicalTacticalSession.DisplayBucket(InitialTile)` e o `OpenTacticalContext` de produção. Não inventa local, unidade ou controle político.
2. **Navegação preservada:** botão visível `VISÃO GLOBAL · GLOBO ESFÉRICO` alterna para o mesmo `GlobePreview` canônico. O seletor real via mouse escolhe território e `VOLTAR AO MAPA TÁTICO` abre a respectiva região tática. O globo nunca é planificado ou descartado; estado da câmera e mundo permanecem preservados conforme o fluxo existente.
3. **Resumo tático de alta densidade:** `GAQASessionPulse` mostra turno, ciclos, próximo ciclo e ritmos reais; `GAQALocalEconomyPulse` mostra pessoas por atividade no tile, estoques físicos de alimento/madeira/pedra e prévia de demanda alimentar; `GAQAWorkerTransitPulse` mostra viajantes e viagens ativas. São *read models* sobre a mesma sessão, atualizados ao avançar ciclos/emitir ordens/carregar partida.
4. **Hierarquia e tipografia:** títulos de até 12pt de tema, dados em 11 e comandos em 12; menores espaços laterais e entre linhas; aviso didático e relatório técnico detalhado de ciclos deslocados para `MOSTRAR DETALHES E FERRAMENTAS DE LABORATÓRIO`, recolhido por padrão. Comandos e resultados principais permanecem operacionais.
5. **Limites:** nenhum saldo familiar V12 legado torna-se caixa canônico; nenhum dado estatístico é inventado para decorar HUD. Não modifica produção, movimentação, território, trade, semântica de turnos, replay, WorldState, hash ou save V20. Não fecha o layout final, não exige inspeção manual por VS.

## Evidência UX progressiva

- `UiE2E.gd`: verifica ao abrir que o tático é visível e o globo não, que os oito residentes/produtores originais aparecem por atividade 4/2/2 e que o ciclo +5 atualiza o resumo; prossegue com a suíte real existente de movimento, salva/reabre, V14/V20 e reserva vazia.
- `UiVisualQA.gd`: executa no Godot Mono 4.7.2 real com Xvfb/Mesa, verifica HUD e geometria do mapa quadrado, captura `02-tactical.png` no início, `01-strategic.png` após entrar no globo, e `03-tactical-return.png` após selecionar território com mouse real e reabrir o tático. São capturas comparáveis, **não** aprovação estética humana nem teste golden pixel.
- `ga-qa-ui-e2e-001.yml` e `ga-qa-ui-visual-002.yml` existentes são obrigatórios; o segundo publica artefato de screenshots. Gate novo VS106 agrega builds e verifica o fluxo real.
- Uma avaliação humana futura poderá usar estes arquivos de evidência quando houver mudança perceptível suficiente ou decisão de UX a resolver; não é checklist por commit.

## Aceite

Conservar 1:1 do viewport, integridade da seleção/continuidade do globo e das ações econômicas existentes; nenhum desvio em saves; 100% dos gates do HEAD concluídos em success. Se problemas de legibilidade, densidade ou usabilidade surgirem numa avaliação pontual, registrar feedback para o próximo incremento sem confundir CI verde com validação final de UX.
