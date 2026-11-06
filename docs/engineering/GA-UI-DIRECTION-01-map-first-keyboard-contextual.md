# GA-UI-DIRECTION-01 — Mapa protagonista, atalhos de alto ROI e interface contextual

**Registro:** 06/11/2026  
**Estado:** PROPOSTA ORIENTADORA EM AVALIAÇÃO; não é ADR aprovado nem autorização para redesenhar a UI sem homologação.  
**Base:** PRs #274–#275, avaliação visual da malha física G(18,0) e discussão de prioridades de UI.  
**Natureza:** política de experiência do jogador (UX); sem alterações no motor, save, hash, economia ou geometria.

> **Tese:** o mapa é a superfície principal de exploração e interação. O teclado acelera o que é frequente e simples. O painel direito permite decidir com contexto. Menus discretos possibilitam descobrir e configurar o que não compensa memorizar. A interface deve ser progressivamente revelada, nunca ostensivamente ocupada só porque há espaço.

## 1. Status das ideias: separar acordo de hipótese

### Decisões de produto/UX já estabelecidas e observadas

- **Tático como interface principal:** visão esférica, mapa amplo, viewport sem barras superiores/inferiores inúteis. O global é lente de navegação e leitura do mesmo mundo físico, não uma segunda obrigação de gerenciamento estratégico.
- **Fonte física compartilhada:** o globo global e a visão local mostram a malha G(18,0) e as cores canônicas de terreno da sessão, em vez de inventar outra geografia. A seleção de foco G3 invisível no globo permanece mecanismo de navegação legado, não uma alegação de seleção exata de tile físico.
- **Baixo ruído visual por padrão:** vizinhos não precisam de anéis de destaque; fronteiras estratégicas começam ocultas e podem ser habilitadas; **hover branco do tile fica preservado** por aceite visual; origem e destino explicitamente escolhidos permanecem distinguíveis.
- **Observabilidade é gate:** ao completar um fluxo relevante do jogo, interromper a expansão de funcionalidades para verificar e melhorar a experiência visual; testes automatizados não substituem aceite visual do usuário.
- **Sem confusão semântica:** estoque, oferta, uso físico, consumo, venda, preço e receita só podem ser apresentados como executados se houver evidência canônica; não rotular money/cash legado de N abstrato.

### Hipóteses de UX propostas — a validar, NÃO congeladas

1. **Map-First, Keyboard-First, Context-Aware:** ações espaciais rápidas no mapa, teclas para repetição e painel lateral para decisões com parâmetros.
2. **Descoberta por menu, eficiência por atalho:** atalhos diretos para poucas ações frequentes; comandos eventuais e combinações complexas ficam em menus legíveis.
3. **Um único acesso discreto às ferramentas visuais como primeira experiência**, possivelmente no canto superior direito do viewport; outros cantos não devem ser ocupados automaticamente.
4. **Biomas e Relevo como visualizações acessíveis tanto no global quanto no tático**. Biomas é o padrão atual; relevo deve vir dos campos físicos 01C. Modo misto é candidato sujeito a teste de contraste/legibilidade.
5. **Painel direito contextual e progressivo**, não painel permanente de todos os sistemas. Itens de diagnóstico e laboratório devem ficar escondidos até solicitação explícita.

Não deduzir desta proposta atalhos definitivos, números fixos de ícones, disposição rígida, taxa de uso, fórmula de ROI ou comportamento implementado. A homologação posterior pode corrigir essas hipóteses.

## 2. Responsabilidades de interação

| Região ou modalidade | Responsabilidade primária | Exemplos | O que evitar |
| --- | --- | --- | --- |
| **Viewport do mapa** | Inspeção e comandos espaciais diretos | selecionar tile, hover, escolher origem/destino, traçar rota, navegar, alternar foco | painéis grandes cobrindo o terreno, ícones fixos nos quatro cantos sem necessidade |
| **Teclado** | Acelerar ações frequentes, simples e reversíveis | alternar Biomas/Relevo, centralizar câmera, abrir visão global, alternar overlay | sequências difíceis de memorizar, conflitos de foco, ações destrutivas em tecla isolada |
| **Painel direito** | Tomada de decisão com profundidade | produzir/realocar com quantidade, revisar suprimentos, confirmar rota, interpretar consequências | log técnico permanente, repetir informações óbvias sobre o mapa |
| **Menu contextual do objeto/tile** | Ações possíveis somente naquela seleção | ver atividade, mover, alocar, planejar, inspecionar disponibilidade | mostrar ações inválidas como se executáveis; criar lógica paralela à ação canônica |
| **Acesso discreto de ferramentas do mapa** | Descobrir e configurar visualizações | camadas, overlays, legenda, ajuda de atalhos | virar barra constante de ícones |
| **Notificações essenciais** | Alertar sem perder contexto | ação recusada, estoque insuficiente, chegada/risco real, ciclo concluído | esconder eventos importantes em menu colapsado ou exibir alertas não comprovados |

**Regra de projeto:** o mapa recebe a **intenção espacial**; o painel direito recebe os **parâmetros e efeitos que precisam de leitura/decisão**. Não multiplicar formas de modificar o mesmo estado sem passar pelo mesmo comando/validação canônicos.

### Exemplo de fluxo

1. O jogador seleciona no mapa um tile de madeira (clique; o hover apenas antecipa o alvo).
2. A seleção revela resumo mínimo do tile e ações possíveis. Um atalho direto pode abrir o comando frequente; o menu oferece a opção visível a quem não sabe o atalho.
3. Se o comando demanda quantidade, grupo beneficiário, rota ou riscos, o painel direito abre **na seção relevante**, com o mapa ainda visível.
4. O jogador confirma; a simulação gera evidência real. A UI confirma apenas os efeitos executados, sem afirmar venda caso só tenha ocorrido uso físico.
5. O jogador fecha/recolhe o detalhe e retorna ao mapa; o aplicativo não mantém todos os controles expostos.

## 3. Política de ROI para controles

**Ocupação de viewport é um custo.** Um botão fixo só se justifica quando sua utilidade supera o custo visual e a interferência com o mapa.

Usar estes critérios qualitativos antes de adicionar um controle:

- **Frequência:** repetido muitas vezes por sessão favorece atalho; eventual favorece menu/descoberta.
- **Complexidade:** ação binária favorece atalho; muitas opções/parâmetros favorecem painel/menu.
- **Descoberta:** opções importantes precisam ter ao menos uma rota visível, sem exigir manual externo.
- **Impacto/risco:** ação destrutiva, irreversível ou de alto custo exige estado visível, revisão/confirmar ou desfazer, não tecla isolada acidental.
- **Contexto espacial:** executar sobre um tile sem interromper seleção e câmera favorece interação direta.
- **Acessibilidade:** todas as ações essenciais devem ser operáveis também sem decorar atalhos e com foco navegável; não depender exclusivamente de cor, hover ou combinação de teclado.
- **Tela/performance:** botão não pode ocultar informação crítica, reduzir viewport útil ou gerar repaints custosos sem benefício.

**Heurística:** atalho para o que o jogador repete; menu para o que ele procura; painel para o que ele precisa **decidir**. Essa é uma regra de priorização, não prova estatística de ROI.

## 4. Hierarquia visual e progressão

- **Padrão:** mapa dominante; estado essencial compacto (ex.: turno/ciclo, seleção, alertas com evidência); nenhuma parede de diagnóstico.
- **Seleção:** resumo do tile/objeto e próximas ações relevantes; detalhes no painel direito quando necessário.
- **Operação em andamento:** mapa pode mostrar origem, destino explícito, caminho e custo, sem ressuscitar destaque de todos os vizinhos nem fronteiras estratégicas permanentes.
- **Modo de visualização:** indicador discreto da camada ativa. Biomas e relevo devem poder ser alternados global/taticamente, a partir de **uma única composição 01C**. Alterar a camada não altera terreno, física, custos, estoque, saves ou hash.
- **Modo avançado/laboratório:** permanece recolhido e claramente identificado; jamais disputar o espaço da UI de jogo.
- **Avisos críticos:** não ficam ocultos atrás do recolhimento; ação pendente/erro real deve aparecer perto do contexto do jogador.

### Biomas versus relevo

O código já mantém **elevação, umidade, temperatura e biomas** na mesma composição física. Biomas discretos ajudam na interpretação ecológica e potencial produtivo; campos contínuos de altitude/umidade fundamentam custos e fatores de produtividade (com coeficientes experimentais a calibrar). São complementares.

- **Biomas (atual):** cores ecológicas canônicas compartilhadas global/tático.
- **Relevo (candidato):** visualização hipsométrica e/ou sombreada **derivada do mesmo campo de elevação 01C**, nunca da calibração HD independente. Não modificar geometria/picking só para testar a paleta.
- **Misto (candidato posterior):** combinar legibilidade dos biomas com relevo somente depois de teste visual.
- **Camadas adicionais (futuras):** estradas, rotas, logística, população etc. opt-in; overlays físicos e políticos são conceitos diferentes.

A escolha de **atalhos específicos, memória do modo ao trocar escala e comportamento no save** permanece aberta. Uma interface pode preservar a preferência visual sem incorporá-la ao estado canônico; tratar como decisão separada.

## 5. Arquitetura sugerida para implementação futura

Não alterar tudo de uma vez. Evoluir por componentes observáveis:

1. **Catálogo central de ações da UI**: ID semântico, rótulo, dica/atalho sugerido, condições de disponibilidade, contexto de foco, categoria e executante canônico. As mesmas ações alimentam teclado, menu e painel, sem cópias divergentes de regras.
2. **Gerência de foco/modalidade:** o atalho só dispara no contexto correto; campos de texto, número e menus recebem prioridade. Acessibilidade, remapeamento e localização são requisitos, não soluções improvisadas em múltiplos arquivos.
3. **Controlador unificado de visualização:** camada de Biomas/Relevo e overlays por estado *de apresentação*, compartilhado entre globo/tático, calculado a partir de dados físicos já autenticados.
4. **Detalhes progressivos no painel direito:** seções por seleção/atividade, com estado de expansão previsível; o painel não rouba eventos de câmera/seleção do viewport.
5. **Acesso de descoberta enxuto:** começar com no máximo um ponto discreto de entrada para camadas e ajuda, avaliando utilidade com captura real. Adicionar ícones aos outros cantos **somente** depois de evidência de uso.
6. **Diagnósticos em espaço próprio:** `HD CALIBRATION`, `LAND +/-`, `WORLD STATS`, dumps de IDs, opções históricas e logs são QA/inspeção, não HUD de jogador. Preservá-los em modo técnico recolhido; não apagar funcionalidades úteis de QA antes de substituí-las.

Nenhum item acima autoriza ativar uma regra de economia, movimentação, geografia ou mercado que ainda esteja em preflight/legado.

## 6. Critérios de avaliação — testes visuais antes de canonizar

A discussão deve gerar **experimentos comparáveis**, não decidir só pelo diagrama.

| Cenário a testar em Godot real | Sinal de boa UX | Falha que exige revisão |
| --- | --- | --- |
| Jogador novo descobre Biomas/Relevo sem memorizar teclas | encontra opção visível e entende qual modo está ativo | função inacessível ou escondida sem indicação |
| Jogador experiente alterna mapa e câmera por teclado | evita ir ao painel para comando simples; não perde foco | tecla conflita com entrada de quantidade/campo |
| Seleciona tile e planeja ação econômica | mapa continua legível; painel mostra custo, riscos e consequência reais | painel ocupa o mapa ou apresenta dados inventados |
| Usa visão global e retorna ao tático | coerência da malha física, seleção e cores | globo usa uma física/cor diferente, ou muda os dados canônicos |
| Passa cursor sobre tile | hover branco existente permanece | hover e destino confundidos; anéis de vizinhos ressurgem |
| Habilita fronteiras estratégicas | encontra controle quando deseja, mas elas começam apagadas | aparecem por padrão ou são exigidas para navegar |
| Testa tela menor | mapa mantém prioridade e menus são acessíveis | corte de texto/ações, janela modal bloqueando jogo |
| Executa ciclo econômico significativo | observa produção, consumo e estoques autênticos | precisa abrir vários dumps técnicos ou vê venda fictícia |

Revisar em conjunto **tempo de acesso, número de ações, obstrução visual, descoberta, erro e conforto**. Nenhuma métrica numérica fictícia é fixada antes da primeira avaliação.

### Roadmap experimental sugerido

- **GA-UI-LAYERS-01:** Biomas e Relevo em ambas as escalas, sem alterar física; validar legibilidade.
- **GA-UI-DISCOVERY-01:** um menu discreto de camadas/ajuda e poucos atalhos não conflitantes; comparar contra o painel atual.
- **GA-UI-CONTEXT-01:** seleção de tile → ações relevantes no mapa → detalhes no painel; testar economia, movimento e um comando futuro.
- **GA-UI-TECHNICAL-01:** recolher/calibrar ferramentas antigas no espaço técnico, preservando QA.

**Gate:** não iniciar automaticamente os quatro PRs. Escolher o próximo com base no aceite visual do PR #275 e nos resultados de um protótipo pequeno.

## 7. Registro de decisão e manutenção

- **Autoridade:** esta nota orienta discussões, revisões e aceite. *Não substitui* `GA-CANON-DIRECTIVES-01-authoritative-product-policy.md` nem as decisões homologadas dos PRs #274–#275.
- **Revisões:** atualizar esta nota em cada mudança de orientação com data, motivo, alternativa considerada, impacto e resultado de teste visual; evitar que o que era hipótese vire regra por omissão.
- **Checklist obrigatório em PRs de UI:** (a) qual problema de jogador está sendo resolvido? (b) por que atalho, mapa, menu ou painel? (c) qual custo de espaço e foco? (d) como o jogador descobre sem memorizar? (e) fonte canônica dos dados? (f) QA Godot e captura? (g) decisão aprovada ou ainda experimental?
- **Gates de engenharia:** FAST por alteração, checkpoint quando apropriado; aceite visual no Windows quando houver mudança significativa. Não chamar job skipped de homologação.

**Síntese para futuras implementações: MAPA PARA INTERAGIR; TECLADO PARA ACELERAR; MENU PARA DESCOBRIR; PAINEL PARA DECIDIR.**
