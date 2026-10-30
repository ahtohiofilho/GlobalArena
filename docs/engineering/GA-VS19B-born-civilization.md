# GA-VS19B — A civilização nasce com sua população

Decisão de produto 30/10/2026. **Substitui conceitualmente a PR #116 / VS19** de fundação por botão. Base técnica: VS18/PR #115. PR empilhada, draft, sem merge.

## Regras mínimas
- Uma população neolítica já é uma comunidade civilizacional desde o nascimento do mundo. Não existe botão, custo ou evento de **fundar civilização**, nem comando para fundar cidade.
- O mundo atual tem **somente uma comunidade inicial com oito trabalhadores**. Criar automaticamente `CivilizationRuntimeRecord(CivilizationId(1), StrategicCellId(origin))` **na mesma revisão inicial** que publica os trabalhadores, o estoque vazio e as contas econômicas iniciais.
- A identidade da civilização identifica os membros, os trabalhadores e o contexto econômico. Os **titulares do dinheiro continuam sendo famílias e produtores**, organizados em contas privadas locais; não inventar tesouro estatal e não confundir associação civilizacional com estatização.
- `CivilizationalEconomicAttributionProjection` associa, para este mundo inicial de **uma única civilização**, cada trabalhador ativo ou em trânsito, cada conta família/produtor e cada registro de estoque à identidade já materializada. Conserva total de trabalhadores e dinheiro. A projeção apenas lê, não cria saldos nem bens invisíveis.
- Organização política inicial **implícita, sem regime ou instituição formal**. `Territory.Controls` começa vazio; presença demográfica ponderada do VS17 permanece visível. Nenhuma célula inteira é conquistada porque alguém está presente em um tile, e atravessar fronteira não é anexar.
- A migração futura para várias civilizações exigirá pertencimento canônico explícito por grupo/conta e política territorial endógena. **Este incremento não supõe várias civilizações** nem cria sistema militar, fronteiras políticas automáticas ou instituições.

## Inicialização Godot
- Assim que `CanonicalGameSession` nasce, criar a população física e sua civilização, antes de qualquer botão. O texto do laboratório passa a **INSPECIONAR POVOAMENTO INICIAL (JÁ EXISTENTE)**.
- O grupo inicial tem 4 agricultores, 2 coletores de madeira e 2 coletores de pedra, com 8 trabalhadores e **320 créditos privados** iniciais, como antes. Todos pertencem à civilização 1 desde o turno 0.
- Os controladores `WorldState.Territory` permanecem vazios: o nível de atividade e a presença são observáveis, mas não há decreto político, chefia, reino ou anexação artificial.

## Persistência / compatibilidade
- Novo **save e hash V14** apenas quando o mundo começa em modo `BornWithPopulation`. Não aparece evento `found`: a civilização é parte do snapshot inicial (revisão 1), não uma decisão posterior.
- Novo arquivo `user://ga-vs19b-born-civilization.json` + o sidecar de câmera VS18. As partidas V13 anteriores mantêm seu arquivo `ga-vs15-worker-transit.json` e reabrem como **V13 legado sem identidade retroativa**. Não falsificar passado nem converter V13 automaticamente; caso se queira migrar mundo V13, criar rotina de migração explícita e validada futuramente.
- O payload V14 informa identidade e território de nascimento, verificado pelo replay determinístico junto do snapshot inicial e hash. Não guarda governos nem controles inventados.
- Manter o nome interno do projeto Godot VS17 para preservar diretório `user://`, o título do laboratório identifica **VS19B**.

## Gate Windows
1. Abrir VS19B no Godot. Antes de clicar em qualquer comando, selecionar território estratégico: o mundo já tem trabalhadores e civilização 1 no estado canônico.
2. Abrir tático. O botão de laboratório é apenas de inspeção e **não cria civilização, trabalhador nem revisão**.
3. Verificar titulares econômicos: famílias e produtores pertencentes à comunidade; dinheiro privado total de 320 créditos e nenhum tesouro estatal.
4. Passar um ciclo econômico; receita dos produtores aparece e dinheiro total permanece inalterado.
5. Enviar trabalhadores ao vizinho e avançar até a chegada; população mantém identidade civilizacional, controle político formal não aparece espontaneamente.
6. Salvar durante viagem em **V14**, mudar câmera, avançar e reabrir. Reproduzir população, dinheiro, ordem, identidade e visão.
7. Reabrir um V13 existente: mundo legado continua legível e compatível, **sem civilização inventada retroativamente**. Para jogar no novo modelo, iniciar partida nova.
8. CI: genesis do estado, titularidade derivada, conservação, replay V14, sidecar câmera, compatibilidade V13 e compilação Windows/Linux.

Fora de escopo: múltiplas civilizações, impostos, controle territorial formal, anexação, guerra, cidades e instituições políticas. Propostas de demanda basal #103 e poder de compra #111 permanecem congeladas.
