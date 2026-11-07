# GA-FOUNDATION-000 — Revisão fundamental do Global Arena

**Data de abertura:** 2026-11-07  
**Versão conceitual:** 0.1  
**Situação:** deliberação em andamento; núcleo explicitamente ratificado nesta conversa  
**Escopo:** desenho do jogo desde os primeiros princípios, sem refatoração de código nesta entrega

> **Regra editorial:** este texto distingue rigorosamente o que foi decidido daquilo que ainda está sendo discutido. Nenhuma hipótese, exemplo ou equação ilustrativa pode ser implementada como regra canônica sem ratificação explícita.

## 1. Objetivo

Reconstruir e avaliar a lógica do Global Arena a partir de uma base mínima, identificando premissas que geram explosão de complexidade nas camadas posteriores. Buscar a menor quantidade de regras fundamentais capazes de sustentar todas as eras do jogo.

**Critérios de avaliação:** simplicidade conceitual, coerência física, extensibilidade entre eras, baixo acoplamento, custo computacional proporcional ao benefício e possibilidade de testar as regras isoladamente.

Este documento abre uma **frente conceitual paralela**. Não implica migração, revogação global dos ADRs anteriores, alteração de saves, mudanças de interface, nem autorização para implementar ou promover código. Quando houver conflito com um ADR existente, registrar o conflito e submeter a decisão de migração.

## 2. Léxico de decisões

- **[RATIFICADO]** — afirmado pelo projetista; integra a base da revisão conceitual.
- **[PROPOSTO]** — hipótese a testar; não é contrato de implementação.
- **[ABERTO]** — decisão ainda não tomada.
- **[SUPERADO]** — só após indicação explícita do registro que o substitui.

Uma decisão nova exige: identificação, enunciado, motivação, consequências e indicação de quais hipóteses/ADRs modifica. Ausência de decisão **não** equivale a permissão para adotar valores padrão ou herdar automaticamente um modelo histórico.

## 3. Princípios fundacionais ratificados

### F-001 — Mundo geométrico e lógico [RATIFICADO]

O mundo tem uma superfície baseada em um poliedro de Goldberg de grande dimensão e uma estrutura lógica representável por **grafo bidirecional**, com nós e arestas. A geometria e a topologia lógica são conceitos distinguíveis.

**Não decidido:** correspondência exata entre face/vértice/célula Goldberg e entidade lógica do grafo; custo direcional idêntico nos dois sentidos; unidades métricas.

### F-002 — Nó físico-produtivo [RATIFICADO]

Cada nó possui parâmetros físicos que determinam o **potencial produtivo** de diferentes recursos. O nó é o local da atividade produtiva. O potencial pode diferir por recurso e por nó; não se presume produtividade positiva universal.

### F-003 — Recursos iniciais [RATIFICADO]

O escopo inicial da análise contém **madeira, pedra e alimentos**. A escolha limita o experimento, não o catálogo definitivo de eras futuras.

### F-004 — Trabalhador como unidade básica [RATIFICADO]

O **trabalhador** é a unidade básica de capacidade de trabalho, destinada a manter uma lógica consistente ao longo de todas as eras. Este termo designa uma unidade de simulação; a correspondência com pessoas, população, famílias ou grupos permanece **aberta**.

### F-005 — Trabalhador físico — Modelo A [RATIFICADO]

Cada trabalhador tem **localização física própria** no grafo: encontra-se em um nó ou em deslocamento por uma aresta. Sua capacidade acompanha a sua posição; trabalhar em outro nó requer deslocamento físico até esse nó.

**Invariantes mínimos decorrentes:**
1. Em um mesmo instante lógico, uma unidade de trabalhador não pode ocupar simultaneamente dois lugares.
2. A mesma unidade de capacidade de trabalho não pode ser usada duas vezes na mesma janela de execução.
3. Não é permitido atribuir trabalho localizado em um nó a uma unidade que fisicamente não está nele, sem regra explícita posterior que justifique um mecanismo distinto.
4. A existência de uma conexão bidirecional não implica teletransporte nem deslocamento instantâneo.

**Ainda não decidido:** duração do trânsito, progresso em arestas, divisão de atividade no turno, consumo durante viagem, agrupamento de trabalhadores, perdas e reprodução.

### F-006 — Infraestrutura em nós e arestas [RATIFICADO]

Há duas categorias fundamentais de infraestrutura:
- **infraestrutura de nó:** relacionada à atividade produtiva;
- **infraestrutura de aresta:** relacionada à circulação e ao transporte.

Não se presumem edifícios específicos, níveis, custos, receitas ou regras de manutenção.

### F-007 — Capacidade natural pré-infraestrutura [RATIFICADO]

Nós, arestas e trabalhadores possuem **capacidades naturais de base**, sem exigir infraestrutura construída para entrar na dinâmica. A infraestrutura é um modificador de capacidades, não uma licença para que a mecânica exista.

**Interpretação restritiva:** capacidade natural não significa que todo nó produza todo recurso; o potencial de um recurso em certo nó pode ser zero. Não se fixam valores positivos universais, velocidade ou produtividade.

### F-008 — Neutralidade de era [RATIFICADO como objetivo de design]

Priorizar regras fundamentais que possam valer do Neolítico às eras futuras. Transformações tecnológicas devem, sempre que viável, modificar parâmetros/capacidades em vez de criar subsistemas substitutos.

## 4. Contratos mínimos — sem fechar cálculos

### Nó

Identidade lógica, conectividade e características físicas; capacidades naturais e infraestrutura de nó. Pode apresentar diferentes potenciais para madeira, pedra e alimentos. **[ABERTO]**: estoques naturais finitos, taxa de regeneração, limites por ocupação, degradação, produção autônoma.

### Aresta

Conecta dois nós com possibilidade lógica de deslocamento nos dois sentidos; possui capacidade natural e eventual infraestrutura de transporte. **[ABERTO]**: tempo/custo de travessia, capacidade de fluxo, largura, sentidos com custos assimétricos, congestionamento, transbordo.

### Trabalhador

Unidade identificável, com capacidade de trabalho e posição física singular. **[ABERTO]**: se capacidade é uma grandeza universal consumida em tarefas diferentes; escala temporal; especialização; composição da unidade; execução parcial de ações.

### Recurso

Tipo distinguível de resultado produtivo; inicialmente madeira, pedra e alimentos. **[ABERTO]**: lote, unidade, estoque, persistência, conservação, transporte, perdas e transformação.

### Infraestrutura

Modifica capacidades de nós ou arestas. **[ABERTO]**: construção, materiais, trabalho, manutenção, níveis, obsolescência e efeitos exatos.

## 5. Disciplina contra complexidade acidental

1. **Não canonizar exemplos numéricos.** Nenhuma constante, multiplicador ou curva de exemplo constitui regra ratificada.
2. **Não criar estado redundante.** Cada variável permanente precisa representar uma propriedade independente e demonstravelmente necessária.
3. **Não inferir subsistemas por antecipação.** Mercado, moeda, poder de compra, impostos, civilizações, governo, população, logística industrial, tecnologia e guerra não são parte do núcleo ratificado.
4. **Não confundir potencial e produção efetiva**, nem produção e estoque; definir relações apenas quando necessário.
5. **Não herdar automaticamente ADRs legados.** Avaliar compatibilidade; divergências precisam de decisão explícita antes de implementação.
6. **Testar incrementos de complexidade** em cenários mínimos antes de ampliar o grafo.

## 6. Sequência de validação conceitual

1. Um nó e um trabalhador: como surge a produção de um único recurso?
2. Um nó, vários trabalhadores: como cresce a produção e quando há saturação?
3. Dois nós e uma aresta: como trabalhadores se deslocam e recursos circulam?
4. Dois nós com infraestruturas distintas: como mudanças de capacidade afetam resultados?
5. Rede de nós: quais leis locais permanecem válidas sem exceções globais?

## 7. Próxima decisão em pauta: sem ratificação

**F-009 — Modelo de capacidade de trabalho [ABERTO]**

Alternativas a confrontar:
- **A. Orçamento universal de trabalho por unidade e período:** produção, construção e deslocamento concorrem pela mesma capacidade, com custos específicos por atividade.
- **B. Uma atividade exclusiva por período:** simplifica a resolução inicial, mas limita a representação de ações parciais e de deslocamentos incompletos.
- **C. Capacidades independentes por atividade:** facilita bônus especializados, porém multiplica estados e risco de dupla contagem.

**Hipótese recomendada para investigação (não ratificada):** começar testando o orçamento universal e verificar se seu custo de execução/calibração compensa a coerência entre eras. Nenhuma unidade de medida, valor padrão, duração do período ou regra de fracionamento foi aprovada.

## 8. Relação com o código e documentos anteriores

Este é um documento **de revisão fundacional**, não uma declaração de que o kernel atual já satisfaz F-001 a F-008. As implementações existentes e seus ADRs continuam descrevendo o comportamento hoje codificado, até migração explicitamente aprovada e homologada. Novas propostas devem referenciar este documento e indicar conformidade, conflito ou ponto ainda aberto.

**Gate documental mínimo para futuras implementações deste novo desenho:**
- citar os IDs F-* efetivamente aplicáveis;
- listar todas as novas premissas propostas;
- demonstrar por testes os invariantes afetados;
- não transformar hipóteses [PROPOSTO] ou [ABERTO] em contratos silenciosamente;
- registrar quais ADRs antigos permanecem compatíveis e quais exigiriam substituição.

## 9. Histórico

- **2026-11-07 — abertura:** base geométrica/topológica, nó físico-produtivo, três recursos, capacidade natural e duas infraestruturas; ratificação explícita do **Modelo A — Trabalhador físico**. F-009 permanece aberta.
