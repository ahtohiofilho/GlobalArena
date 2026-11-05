# GA-UI-PRODUCTION-STATES-01 — produção e construção visíveis no mapa tático

**Data:** 2026-11-05. **Base:** PR #262 (assentamento único visual), HEAD `4652278ca1d0b0c7f054bf6decf548989c11048c`. PR empilhada Draft, sem merge.

## Resultado de produto

Depois de validar a primeira representação tridimensional do assentamento neolítico tardio no Windows, o próximo ganho de ROI é **mostrar a infraestrutura produtiva real evoluindo sobre os mesmos tiles**, sem depender de olhar apenas o laboratório lateral.

O assentamento — seja pequeno ou megalópole — **segue uma única lógica populacional e econômica**, assim como decidido pelo usuário. O assentamento desenhado não é prédio canônico nem "fundação de cidade". O novo indicador sobreposto não transforma povoamentos em classes nem cria agentes novos. O acampamento de viagem permanece estado físico temporário do mesmo contingente humano, sem mudar esse contrato.

## Fonte canônica única

`CanonicalTacticalWorksiteMapProjection.FromSession(session)` usa somente:
1. `session.ExecutedPrimitiveAssemblies`: frames materiais com madeira/pedra **de fato incorporadas** no journal de montagem;
2. `session.State.PrimitiveFacilities`: ativos físicos do `WorldState`, com trabalhadores comprometidos, turnos de construção/conclusão e operadores existentes.

Cada montagem tem ordinal único. Toda instalação deve corresponder a uma montagem real no mesmo tile, sem inventar materiais. O resultado ordenado é **um marcador agregado por tile**, não um novo centro urbano nem um marcador por grupo de trabalhadores. Mesmo se há obra e operação simultâneas, a UI mantém um marcador e apresenta quantidades distintas no hover.

Estados com prioridade de apresentação:
- **MaterialFrame:** madeira/pedra incorporadas, ainda não existe obra;
- **UnderConstruction:** instalação inacabada, com pessoas reservadas de verdade;
- **Operating:** abrigo concluído com operadores reais quando não há outra obra simultânea;
- **Completed:** ativo físico pronto, mas sem operadores, quando não há obra em andamento.

Em um tile com simultaneamente uma obra e um abrigo operando, a prioridade gráfica é **obra** e o hover mostra ambos os números. Não se infere produção a partir da cor do marcador; "operando" significa `OperatingWorkers > 0`, e não um bônus quantitativo.

A projeção recalcula sem armazenar novos estados, serializar dados, alterar hash ou emitir saldo `N`. Confere a conservação do número de frames, instalações e locais e falha se um ativo for representado sem insumo incorporado.

## Visual 3D e câmera

`TacticalContextPreview.WorksiteMarkers.cs` adiciona `GAWorksiteProgressMarkers`, uma única malha procedural para os tiles visíveis. A geometria fica ao lado das duas moradias da UI-SETTLEMENT-GLYPH-01, apoiada no plano tático e visível a 45° de pitch, com yaw previsto na câmera:
- frame: dois pequenos volumes de pedra/madeira;
- obra: quatro postes erguidos e viga aberta;
- concluída: pequeno abrigo com telhado cinza;
- operando: mesmo abrigo com telhado verde e sinal de trabalho.

Os sinais mudam apenas o desenho; não são prédios adicionais nem unidades. O hover reutiliza a inspeção do tile com origem econômica verdadeira e acrescenta contagem de frames prontos, obras, instalações concluídas, operadores e trabalhadores em construção. Um imóvel físico persistente deve continuar desenhado mesmo após saída de residentes — é um ativo, não uma nova cidade.

`Main.NeolithicWorkerLab.UpdateWorkerLabHud()` reconstrói a projeção após cada tick econômico, obra, operação, suspensão e reabertura. `TacticalContextPreview.Open()` reconstrói a malha após zoom de enquadramento ou troca do globo. O resumo lateral inclui legenda textual.

## QA

`GaVs123TacticalProductionMapStateTests` cobre ausência de instalações falsas em assentamento inicial, montagem, construção, conclusão, operação, suspensão, múltiplas instalações/atividades no mesmo tile sem criar novas cidades, persistência durante deslocamento e save/replay idênticos em todas as etapas.

`UiVisualQA.gd` comprova que o nó gráfico de infraestrutura é criado, mas invisível numa partida sem montagens nem edifícios. O CI de renderização real e um smoke visual no Windows são gates separados da homologação de compilação/kernel.

**Não está no escopo:** alterar economia, multiplicar rendimento, inventar estoques/novos itens, duração de obras, construção civil autônoma, escalonamento automático de aparência urbana, representação definitiva por eras, parada de viagem, receitas, dinheiro ou poder de compra abstrato.

## Aceite no Windows

Abrir REBASE de UI e no tile povoado executar a mesma sequência validada pelo kernel: após recurso material, `MONTAR → CONSTRUIR → AVANÇAR 1 CICLO → OPERAR → AVANÇAR 1 CICLO`. O indicador no mapa deve migrar **ocre → âmbar → cinza → verde**, sem mudar o símbolo de assentamento, a seleção do tile ou a contagem populacional. Passar o mouse deve revelar dados de ambas as construções e do povoamento. Confirmar com capturas da cena em pelo menos dois estados.

O código continua em Draft até FAST, 6/6 regressões kernel/hash, benchmark, Godot E2E e GA-QA-VISUAL-002. Captura do Godot no Windows valida apresentação, não substitui testes de lógica.
