# GA-UI-SETTLEMENT-GLYPH-01 — população contínua, símbolo sedentário e atividade sob demanda

**Data:** 2026-11-05  
**Base empilhada:** GA-UI-TACTICAL-WIDE-01, PR #261, HEAD `35bbed204ba54f0dd1b06804f700b533d9d20c88`.  
**Escopo:** marcador e interação do mapa tático Godot, sem mudança nas regras, saves ou hashes.

## Decisão canônica — uma única lógica

**Assentamento, aldeia, vila, cidade e megalópole NÃO são diferentes tipos de entidade econômica, jurídica, política ou populacional.** Representam níveis e formas emergentes da mesma dinâmica de residentes, trabalhadores, produtores, consumo, estoques, transportes e atividade física no tile. Não existe `FoundCity`, `UpgradeSettlement`, classificação obrigatória nem uma transição oculta de motor ao cruzar limiar de população. **Nomes e patamares são, no máximo, apresentação derivada**, recalculável e descartável; não fazem parte do WorldState ou de saves.

**Acampamento de viagem** não inaugura outro sistema: é o mesmo contingente humano, mantido na mesma contabilidade física, mas com estado de **deslocamento/parada temporária**, suprimentos consumidos, espera e eventual coleta. Pode futuramente ser representado pela mesma família de arte base, com composição temporária e duração; não deve virar um "tipo de cidade" nem criar pessoas, trabalho, estoques ou produção fantasma. O acampamento logístico já documentado continua distinto de uma instalação produtiva permanente construída com materiais. A origem de ambas é sempre o estado real, não um botão "fundar" ou "subir categoria".

## Direção visual

O desenho inicial é um **assentamento sedentário de Neolítico final / transição para a Idade dos Metais**, e não acampamento de viagem, caverna paleolítica, estrutura industrial ou cidade predefinida.

- **Um único símbolo por tile com população residente real**; a fonte é `EmergentSettlementProjection.FromSession(session)`, que soma todas as alocações válidas por tile. População maior não cria outra entidade ou círculo; futuras variações de densidade são totalmente UI e não foram ativadas neste gate.
- Substituir três marcadores de profissão sempre visíveis por **duas moradias fixas de argila/entramado com telhado inclinado, pequena área de trabalho e armazenamento**. Arte processual, sem dependência de textura/imagem externa ou licença.
- Geometria real de pequeno volume construída em C# / `SurfaceTool`, posicionada a partir dos mesmos polígonos/células do tático. Todos os componentes apoiam no plano de terreno `Y≈0` e têm alturas proporcionais ao raio útil do tile, visíveis de câmera ortográfica oblíqua de 45 graus e das rotações suportadas. Evitar billboard de tela ou ícone que "flutue"; nunca interceptar o raio do tile picker, que continua projetado matematicamente no terreno.
- Halo discreto de ocupação e contorno de seleção atuais permanecem. Marcadores âmbar de **pessoas em viagem** também permanecem e NÃO são chamados de assentamento.
- Informações detalhadas são `hover` e clique: o hover sobre tile ocupado mostra o total de pessoas e ocupações agricultura, madeira, pedra; o clique continua selecionando o mesmo tile e exibindo as mesmas quantidades na barra lateral. **A interface nunca converte ocupação em três entidades.**
- As bolinhas originais de grupo são preservadas apenas como **fallback de QA**, ativável na seção expandida de ferramentas de laboratório; desligadas por padrão.

## Contratos de arquitetura

1. Nenhuma nova enum de `SettlementType`, `CitySize`, `CampIsTown` e nenhuma classificação no WorldState.
2. `EmergentSettlementProjection` permanece fonte determinística da presença humana e não sofre mutação. `TacticalWorkerMarkerProjection` continua fonte dos movimentos reais e do modo debug de profissões.
3. `TacticalContextPreview.Settlements.cs` produz a malha apenas de apresentação, substituível por arte final/LOD sem alterar o motor. O número de moradias do ícone **não equivale ao número de trabalhadores nem edifícios canônicos**: é um símbolo, não uma contagem.
4. Hover usa ray-to-plane existente, respeitando pitch, pan, yaw e zoom; não cria hitboxes 3D que concorram com a seleção existente.
5. A ligação com fases de infraestrutura (`em obra`, `concluída`, `operando`) será overlay secundário com dados reais de `PhysicalPrimitiveFacility`, sem redefinir o tipo de assentamento.
6. Preservar a janela tática **horizontal** aprovada no PR #261 e a barra de comandos rolável.

## QA

- `GaVs122UnifiedInhabitedPlacePresentationTests`: valida que três ocupações constituem **um único povoamento**, que reatribuição muda apenas composição produtiva, que viagem transitória não funda assentamento e que chegada real produz presença no destino. Valida conservação de pessoas e replay.
- `UiVisualQA.gd`: exige malha tridimensional de assentamento visível, antigas bolinhas ocultas normalmente e reversíveis via modo debug, navegabilidade tático → globo → tático e screenshots reais em 1600×900.
- `GA-QA-UI-E2E-001`: compatibilidade dos comandos e persistência preexistentes.
- CI FAST, seis testes reais multiplataforma (kernel/hash), benchmark, além de **GA-QA-VISUAL-002** habilitado pelo label `ga-ci-full` e revisão da captura do usuário no Windows.

## Aceite visual do usuário

A implementação só será considerada visualmente aprovada quando o mapa demonstrar claramente uma moradia estável com telhados inclinados, sem distorção na câmera atual, com hover informativo, tile picking intacto e nenhum encobrimento excessivo da malha. Teste automatizado não substitui a verificação em Windows.

## Fora do escopo

Variantes por era/tamanho; escolha definitiva da arquitetura da cidade; modelo 3D detalhado; sombras avançadas; animações; campo de produção fictício; alteração do modelo de acampamento; fundação administrativa ou sistema de propriedades; mudanças de economia, tecnologias, população, stocks, hash, save e poder de compra `N`.
