# GA-Next — Visualização de trabalhadores físicos no painel Godot (15/11/2026)

**Status:** incremento real de apresentação no cliente Godot .NET 4.7.2, empilhado sobre PR #354, ainda sem merge. **Não** altera a economia, preços, demanda, infraestrutura, guerra, tesouros ou a autoridade dos trabalhadores.

## Objetivo de jogabilidade

Uma seleção de célula **estratégica canônica Goldberg** no globo agora alimenta uma área de inspeção física na coluna direita, sob o contexto estratégico. O jogador pode verificar quais trabalhadores físicos estão no tile selecionado, a que civilização pertencem, seu modo de atividade, produto, capacidade de produção e multiplicador de demanda.

O viewport continua reservado ao mapa. A inspeção fica no scroll da coluna direita, não cria etiquetas sobre o globo, não altera cores nem marca vizinhos. **Todos os tiles mantêm exatamente as mesmas regras.**

## Contrato de dados

A nova classe runtime **StrategicPhysicalWorkerInspector.Read(generatedWorld, selectedStrategicCellId, worldState)** recebe somente o worldgen verdadeiro, a seleção de uma célula estratégica real e um snapshot do **WorldState v9**. Não lê os agregados legados EconomicPoint.Workforce como se fossem trabalhadores físicos e não consulta a heurística F072 provisória.

O retorno inclui:
- Identidade canônica da célula, revisão autoritativa e status de disponibilidade;
- lista ordenada por ID dos trabalhadores **fisicamente localizados naquele tile exato**;
- ID do trabalhador, proprietário, atividade (Producing, Traveling, Military, Other), CommodityId, OutputMicroPerTurn e DemandMultiplierMicro;
- total de trabalhadores demandantes e subtotais por atividade. A capacidade nominal NÃO é apresentada como produção efetiva quando a atividade não está produzindo.

Os estados são explícitos:
1. Sem snapshot autoritativo — **NoRuntimeState**.
2. Snapshot sem vínculo com o mundo — **UnboundWorld**.
3. Snapshot de outro mundo — **WorldMismatch**, sem vazamento de dados.
4. Censo individual ainda não inicializado — **CensusUninitialized**, não confundir com zero trabalhadores.
5. Censo inicializado, tile com zero trabalhadores — **Bound**, lista vazia legítima.

Não existe categoria privilegiada de tile, demanda sintética, filtro geográfico arbitrário nem identificação de trabalhador inventada.

## Integração do cliente

O arquivo real **tools/GlobalArena.Godot/Scripts/Main.cs** adiciona uma seção **PHYSICAL WORKERS | STRATEGIC TILE** ao sidebar estratégico existente, conectada:
- ao evento real de escolha de território do **GlobePreview**, sem alterar o renderer;
- ao fluxo de snapshot via **ReceiveAuthoritativeSnapshot** e **AttachRuntimeState** no thread Godot;
- à troca de contextos: na visão tática, o painel estratégico continua oculto.

A lista mostra no máximo **8 trabalhadores visíveis** e a contagem do restante, por proteção de viewport (não há limite de trabalhadores por tile no domínio). Dados de produção/multiplicador são formatados no cliente, não recalculados. O inspector permanece **somente leitura**; nenhuma ação de jogo ou solução econômica é disparada ao selecionar território.

**GlobePreview.cs** limpa corretamente a seleção estratégica ao alternar para um layer experimental HD que não possui identidade estratégica selecionável, evitando que a UI continue exibindo trabalhadores de uma seleção falsa ou desatualizada.

## Verdade sobre a partida inicial e a execução

Por padrão, **Main** inicializa uma sessão Goldberg G(3,0) vinculada, mas ainda com **censo de trabalhadores físicos não inicializado**. Nesse caso, selecionar um tile apresenta **Physical worker census not initialized in this game**, e NÃO inventa trabalhadores apenas para preencher o painel.

Quando um componente de gameplay externo publica um **WorldState v9 com trabalhadores físicos reais** no mesmo mundo através da interface pública **ReceiveAuthoritativeSnapshot**, a UI mostra os dados imediatamente e os atualiza a cada novo snapshot. Um controle autoritativo que gere trabalhadores iniciais no carregamento da partida continua sendo desenvolvimento futuro; não criar duplicação na UI.

### QA manual esperada

Abrir tools/GlobalArena.Godot/project.godot no Godot .NET 4.7.2, executar cena principal e selecionar um território no globo:
- Sem censo, painel sinaliza explicitamente ausência de inicialização.
- Com snapshot autoritativo v9 injetado no fluxo real de gameplay, a seleção mostra trabalhadores apenas da célula selecionada, com revisão e atividades; ao mudar a seleção os fatos mudam.
- Depois de publicar movimento por evento canônico, novo snapshot atualiza a localização exibida.
- Retorno à visão tática oculta o inspector estratégico, sem mudar o estado.
- Selecionar modo visual HD experimental limpa seleção estratégica; nenhuma identidade fina é usada como tile econômico.

A compilação CI do projeto Godot verifica código .NET e referências, **não substitui um E2E visual com runtime Godot**. Se o SDK indisponível em runners, o gate deve declarar explicitamente essa limitação, sem prometer homologação visual.

## Automação de regressão

Os testes **StrategicPhysicalWorkerInspectorTests** exercitam worldgen real, censo v9, cenários sem snapshot/censo, mundo divergente, tile vazio, vizinho ocupado, IDs válidos, dados em microinteiros, imutabilidade e refresh após nova revisão. A CI Windows e Ubuntu compila o cliente Godot e reexecuta a suite integral do kernel.

## Limites de escopo

A economia experimental tem gaps de precisão relevantes e continua desabilitada como economia autoritativa/financeira. Nenhuma cotação ou receita é apresentada como fato homologado; o painel mostra **somente trabalhadores e suas propriedades canônicas**. O catálogo agrícola, comandos visuais de deslocamento, combate e livro-caixa ainda não foram implementados na interface.

**Próximo investimento de produto:** conectar o materializador de trabalhadores do começo de partida e o envio de ordens do jogador ao commit gate, mantendo a UI apenas como emissora de intenção e consumidora de estado.
