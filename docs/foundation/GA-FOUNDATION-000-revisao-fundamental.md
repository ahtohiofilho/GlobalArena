# GA-FOUNDATION-000 — Revisão fundamental do Global Arena

**Data de abertura:** 2026-11-07  
**Versão conceitual:** 0.7  
**Situação:** deliberação em andamento; núcleo explicitamente ratificado nesta conversa  
**Escopo:** formulação de uma **nova versão conceitual autônoma** do jogo, sem dependência do kernel anterior; sem refatoração de código nesta entrega

> **Regra editorial:** este texto distingue rigorosamente o que foi decidido daquilo que ainda está sendo discutido. Nenhuma hipótese, exemplo ou equação ilustrativa pode ser implementada como regra canônica sem ratificação explícita.

## 1. Objetivo

Reconstruir e avaliar a lógica do Global Arena a partir de uma base mínima, identificando premissas que geram explosão de complexidade nas camadas posteriores. Buscar a menor quantidade de regras fundamentais capazes de sustentar todas as eras do jogo.

**Critérios de avaliação:** simplicidade conceitual, coerência física, extensibilidade entre eras, baixo acoplamento, custo computacional proporcional ao benefício e possibilidade de testar as regras isoladamente.

Esta documentação define uma **nova versão do Global Arena**, desde o marco zero. O desenho é **autônomo**: comportamento existente, convenções, decisões e limitações técnicas do Global Arena anterior **não são insumos obrigatórios, nem precedentes normativos**. A arquitetura nova é o critério de aprovação. O repositório atual é apenas o local de registro nesta fase, não uma dependência de implementação.

Esta entrega apenas documenta decisões. Não modifica, migra, revoga ou homologa os módulos do jogo anterior; os sistemas existentes podem continuar a funcionar na sua própria linha de evolução. Uma decisão futura e explícita definirá como, quando e onde implementar a nova versão.

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

### F-009 — Capacidade universal de trabalho [RATIFICADO]

Cada trabalhador tem **um orçamento único de capacidade de trabalho por período**, que pode ser aplicado a atividades como produção, construção e deslocamento. As diferentes atividades disputam essa **mesma capacidade**, em vez de possuírem orçamentos independentes que permitam executar simultaneamente trabalho duplicado.

**Invariante:** a soma da capacidade efetivamente comprometida ou consumida por um trabalhador em um período não pode exceder o orçamento desse trabalhador nesse período. O tratamento de tarefas contínuas entre períodos deverá preservar essa contabilidade.

**Ainda não decidido:** escala e unidade de capacidade, duração do período, custos específicos por atividade, frações de trabalho, retomada de tarefas, modificadores e duração do movimento. Ratificar F-009 **não** ratifica uma fórmula de produção ou uma economia numérica.

### F-010 — Independência conceitual e código descartável [RATIFICADO]

A nova versão será **projetada como um jogo novo**. Nada no código, modelo de dados, subsistemas, APIs, testes, UI, saves, parâmetros ou ADRs do Global Arena anterior limita o que pode ser decidido na nova arquitetura.

O código anterior só poderá ser reaproveitado **depois** de fechar as regras pertinentes da nova versão, e apenas se uma análise demonstrar que esse código executa exatamente o contrato novo, sem trazer implicitamente premissas legadas. O reaproveitamento deve servir para acelerar a implementação; **não é um requisito de produto**.

Para componentes incompatíveis, a diretriz é **descartar/substituir**, mesmo que isso implique retrabalho. Não é objetivo preservar compatibilidade de saves, protocolos, fórmulas, estruturas de dados ou fluxos internos anteriores, salvo se a nova arquitetura vier a exigir expressamente algum deles por mérito próprio.

**Proibição de contaminação:** não inferir nenhuma regra nova a partir de comportamento existente, nomes de classes, interfaces, testes históricos, economia anterior ou caminhos de implementação. Comparações com o legado são permitidas **na fase de implementação e auditoria de reaproveitamento**, não como condicionantes da formulação conceitual.

## 4. Contratos mínimos — sem fechar cálculos

### Nó

Identidade lógica, conectividade e características físicas; capacidades naturais e infraestrutura de nó. Pode apresentar diferentes potenciais para madeira, pedra e alimentos. **[ABERTO]**: estoques naturais finitos, taxa de regeneração, limites por ocupação, degradação, produção autônoma.

### Aresta

Conecta dois nós com possibilidade lógica de deslocamento nos dois sentidos; possui capacidade natural e eventual infraestrutura de transporte. **[ABERTO]**: tempo/custo de travessia, capacidade de fluxo, largura, sentidos com custos assimétricos, congestionamento, transbordo.

### Trabalhador

Unidade identificável, com capacidade física e posição singular. A capacidade de trabalho **é uma grandeza universal disputada entre atividades [RATIFICADO F-009]**. **[ABERTO]**: escala temporal e quantidade por período; especialização; composição da unidade; execução parcial de ações; contabilidade do trânsito.

### Recurso

Tipo distinguível de resultado produtivo; inicialmente madeira, pedra e alimentos. **[ABERTO]**: lote, unidade, estoque, persistência, conservação, transporte, perdas e transformação.

### Infraestrutura

Modifica capacidades de nós ou arestas. **[ABERTO]**: construção, materiais, trabalho, manutenção, níveis, obsolescência e efeitos exatos.

## 5. Disciplina contra complexidade acidental

1. **Não canonizar exemplos numéricos.** Nenhuma constante, multiplicador ou curva de exemplo constitui regra ratificada.
2. **Não criar estado redundante.** Cada variável permanente precisa representar uma propriedade independente e demonstravelmente necessária.
3. **Não inferir subsistemas por antecipação.** Mercado, moeda, poder de compra, impostos, civilizações, governo, população, logística industrial, tecnologia e guerra não são parte do núcleo ratificado.
4. **Não confundir potencial e produção efetiva**, nem produção e estoque; definir relações apenas quando necessário.
5. **Legado não orienta a formulação.** Não consultar ADRs ou código antigo como fonte de regras para esta versão; reuso posterior depende de auditoria contra o contrato independente.
6. **Testar incrementos de complexidade** em cenários mínimos antes de ampliar o grafo.

## 6. Sequência de validação conceitual

1. Um nó e um trabalhador: como surge a produção de um único recurso?
2. Um nó, vários trabalhadores: como cresce a produção e quando há saturação?
3. Dois nós e uma aresta: como trabalhadores se deslocam e recursos circulam?
4. Dois nós com infraestruturas distintas: como mudanças de capacidade afetam resultados?
5. Rede de nós: quais leis locais permanecem válidas sem exceções globais?

## 7. Próxima decisão em pauta: sem ratificação

**F-011 — Conversão de trabalho em produção [ABERTO; hipótese histórica temporariamente subordinada a F-012/F-013]**

### Problema

Uma função puramente proporcional \`Q = W × R\`, onde \`W\` é trabalho e \`R\` rendimento físico, é adequada **enquanto há oportunidades de produção não disputadas**. Não é justificativa suficiente para extrapolar a produção linearmente quando dezenas de unidades exploram um único nó.

Separar três questões:
1. **Rendimento:** quanto material uma unidade de trabalho consegue gerar nas condições físicas e técnicas locais?
2. **Acesso:** quanto trabalho total pode ser absorvido naquele recurso/nó por período, dado o espaço e as oportunidades físicas de operação?
3. **Disponibilidade material:** quanto material pode efetivamente ser retirado ou renovado? Este conceito não se confunde com acesso e será tratado separadamente, porque madeira, pedra e alimentos não têm obrigatoriamente a mesma dinâmica de reserva/regeneração.

### Candidato preferencial para o primeiro experimento [PROPOSTO]

\`\`\`text
E(v,r) = min(W(v,r), A(v,r))
Q(v,r) = E(v,r) × Y(v,r)
\`\`\`

- \`W(v,r)\`: trabalho total direcionado ao recurso \`r\` no nó \`v\`, proveniente de unidades **fisicamente presentes** e de seus orçamentos universais de trabalho F-009.
- \`A(v,r)\`: quantidade de trabalho que as oportunidades físicas **acessíveis** conseguem absorver naquele período, calculada a partir das propriedades do nó (e dos modificadores aplicáveis). **Não** é uma tabela de limite arbitrário de produção nem um novo estoque autônomo.
- \`Y(v,r)\`: rendimento por unidade de trabalho efetivamente aplicada, calculado a partir das propriedades físicas relevantes e, no futuro, dos efeitos de tecnologia/infraestrutura.
- \`Q(v,r)\`: resultado potencial do processo **antes** de qualquer regra de disponibilidade de material que venha a ser ratificada.

Este candidato não exige uma função de retornos decrescentes arbitrária: o retorno marginal é constante até a ocupação das oportunidades físicas e passa a zero depois dela. Essa mudança abrupta é uma simplificação consciente, a comparar com os benefícios de uma curva suave. Trabalhadores em excesso **não são automaticamente consumidos ou perdidos**; podem ser destinados a outras atividades, caso as regras de alocação permitam.

**Exemplo puramente ilustrativo**, não calibrado: \`Y=3 unidades por trabalho\`, \`A=10 unidades de trabalho por período\`. Para \`W=1, 5, 10, 20, 100\`, a produção potencial seria \`3, 15, 30, 30, 30\`. Os números não se tornam canônicos.

### Alternativas não ratificadas

- **Linear irrestrita:** menos parâmetros, mas permite exploração ilimitada do mesmo espaço se não houver outra restrição física; útil como caso-limite local, não necessariamente como regra geral.
- **Saturação suave racional:** \`Q = Y W / (1+W/A)\` ou forma equivalente. Melhor transição marginal, mas adiciona interpretação/calibração de parâmetros e pode produzir um rendimento inferior a \`Y\` mesmo para um trabalhador isolado. Não adotar só por elegância matemática.
- **Disponibilidade física + produção:** a futura regra de estoque/acesso poderá limitar \`Q\` adicionalmente, mas não substitui a questão de quantos trabalhadores operam com eficiência em um espaço.

### Critérios obrigatórios antes de ratificar

1. \`W=0 → Q=0\`; rendimento físico nulo para o recurso implica \`Q=0\`.
2. Nenhum trabalhador remoto produz sem deslocamento no grafo.
3. Acrescentar trabalho não pode reduzir a produção total por mero efeito de parametrização.
4. Trabalho não aplicado ao processo não pode ser cobrado como consumido inadvertidamente.
5. Não duplicar a mesma superfície/operação quando dois recursos concorrem pelo mesmo espaço físico — concorrência entre recursos **ainda em aberto**.
6. Reserva, regeneração, extração e armazenamento são decisões **posteriores e distintas**; não converter potencial em material físico infinito.
7. Dar preferência a parâmetros **derivados de atributos físicos do nó**, e não a estados novos e numericamente ajustados sem significado.

**Status:** a equação limitada por oportunidades é uma **recomendação para testar**, não uma lei ratificada. A forma exata de derivar \`A\` e \`Y\` ainda precisa ser demonstrada, e a dinâmica dos materiais permanece aberta.


## 7A. Síntese conceitual: agente físico e orçamento de tempo

### F-012 — Trabalhador como agente físico de ação e demanda [RATIFICADO NO NÍVEL DE PRINCÍPIO]

O trabalhador é a unidade fundamental de dinâmica, abstrata e fisicamente localizada. Possui:

1. **Localização física** singular no grafo, em nó ou durante travessia de aresta (F-005).
2. **Capacidade de ação** compartilhada entre produção, construção e deslocamento (F-009).
3. **Demanda de itens** que poderá originar consumo efetivo, conforme regras ainda a definir.

A presença da demanda é ratificada; **sua composição, geração, periodicidade, prioridade, substituibilidade, satisfação e consequências de não atendimento permanecem em aberto**. Não confundir demanda com consumo material efetuado, compra, transferência de titularidade ou existência de estoque. A unidade não corresponde obrigatoriamente a um ser humano individual.

Nó e aresta oferecem o contexto físico que condiciona as ações; a infraestrutura modifica características desses elementos. O modelo não pressupõe mercado, moeda, governo ou preços nesta camada.

### F-013 — Capacidade universal de ação expressa em tempo [RATIFICADO COMO DIRETRIZ CONCEITUAL]

A capacidade universal F-009 é reinterpretada como **tempo disponível para realizar ações**. Não existem saldos separados de pontos de produção, construção ou movimento.

Para cada trabalhador e período, sob qualquer unidade temporal posteriormente escolhida:

- tempo usado em ações não pode exceder o tempo disponível;
- uma ação em andamento ocupa uma parcela desse mesmo orçamento;
- produzir, construir e deslocar-se são diferentes **resultados de aplicar tempo a processos com precondições e rendimentos físicos próprios**, e não moedas de ação desconexas;
- a execução de uma atividade deve respeitar a localização física do trabalhador e as propriedades dos nós/arestas;
- o tempo é recurso da unidade para **ações deliberadas**; a mera passagem de tempo e fenômenos passivos não são automaticamente tarefas que consumam esse orçamento.

A decisão sobre **quantum de tempo do turno, fracionamento entre tarefas distintas, simultaneidade, interrupções e regra detalhada de processamento** permanece em aberto. A continuidade de tarefas incompletas entre períodos foi ratificada em F-015. Exemplo "1 unidade por turno" é apenas normalização ilustrativa, não valor fixado.

A intensidade ou produtividade de uma ação pode depender da tecnologia, ferramentas, condições físicas e outros modificadores futuros, mas sem alterar o conceito de orçamento temporal único. Não se presume que todas as ações tenham a mesma eficiência ou requerimentos materiais.

### F-014 — Demanda, satisfação e consumo físico [ABERTO]

Para cada tipo de item, distinguir explicitamente:

1. necessidade/demanda do agente;
2. existência física e acesso ao item;
3. transferência ou disponibilidade para o agente (se for exigida pelas futuras regras);
4. consumo físico efetivamente registrado;
5. necessidade não atendida, caso exista.

Demanda **não produz bens**, **não efetiva transações automaticamente** e **não implica gasto de tempo de ação**. Um trabalhador poderá ter demanda mesmo quando parado ou em deslocamento, mas ritmo/quantidade/consequências permanecem abertos.

Hipótese para teste (não ratificada): separar **demanda basal associada ao transcurso do tempo** de **demanda adicional associada a atividades**. As duas devem referir-se às mesmas mercadorias físicas e não podem duplicar consumo da mesma necessidade.

### Experimentos do orçamento universal [PROPOSTOS]

- **Trabalhador imóvel:** orçamento temporal disponível, demanda pode existir, nenhuma ação produtiva; verificar separação entre inatividade e consumo.
- **Trabalhador em produção:** tempo aplicado em nó gera produção conforme uma relação ainda não ratificada e disponibilidade física.
- **Trabalhador em construção:** tempo aplicado e materiais eventualmente necessários produzem progresso persistente de obra; material não pode ser inventado.
- **Trabalhador em deslocamento:** tempo investido numa aresta gera progresso de travessia; enquanto a posição física estiver em trânsito, não atribuir produção indevida a nó de origem ou destino.
- **Trabalhador misto:** se ações parciais forem permitidas, a soma dos tempos utilizados não excede o orçamento. Nenhuma permissão para fracionamento foi ratificada ainda.

### Revisão de prioridade da F-011

A formulação anterior \`Q=R×min(W,A)\` e a alternativa de saturação suave seguem como **hipóteses de pesquisa, não como bases da nova versão**. Antes de fixar \`A\`, \`R\` ou qualquer curva, investigar o processo geral **ação no espaço → transformação física de estado**, com restrições do recurso. Essa ordem evita construir a arquitetura em torno de um teto inventado.

**A pauta do contrato temporal geral é contemplada por F-015.** A forma de representar o progresso físico e de disponibilizar resultados continua aberta em F-016; nenhum esquema de dados ou API está decidido.

### F-015 — Progresso temporal universal das ações [RATIFICADO COMO PRINCÍPIO]

Uma ação pode exigir mais tempo do que resta disponível ao trabalhador em um único turno/período. Nesse caso, **não é cancelada nem completada artificialmente**: a porção de ação efetivamente executada produz progresso, e o progresso necessário para continuar depois é preservado.

**Contrato lógico mínimo:**
1. **Verificar contexto:** identidade e localização do trabalhador, condições físicas de nó/aresta e demais pré-condições da atividade.
2. **Comprometer tempo:** aplicar apenas uma parcela não negativa do orçamento temporal ainda disponível da unidade.
3. **Aplicar efeito:** atualizar o estado físico correspondente ao tempo realmente executado, sob a regra específica da atividade e restrições de disponibilidade.
4. **Persistir o que importa:** registrar efeitos já realizados e somente o progresso incompleto necessário à continuação.
5. **Concluir quando cabível:** tornar disponível o efeito que dependa de um marco de conclusão; não antecipar esse efeito apenas porque houve progresso.

**Invariantes:**
- Uma mesma parcela de tempo não pode ser usada em duas ações.
- A soma do tempo comprometido não pode exceder o orçamento do período.
- Uma tarefa não recebe progresso sem aplicação válida de tempo, salvo regras explícitas posteriores de processos passivos.
- Efeitos físicos e insumos já consumidos não podem ser aplicados de novo ao retomar a ação.
- A continuidade não exige que uma atividade seja artificialmente concluída ou reiniciada a cada turno.
- Persistência não significa criar um contador universal redundante: sempre que possível, progresso é **o próprio estado físico relevante** (posição na aresta, avanço material da obra, quantidade efetivamente extraída).

**Crucial:** o contrato temporal é comum, mas **resultados não são obrigatoriamente graduais nem intercambiáveis**. Uma extração poderá disponibilizar produção incremental, uma obra poderá necessitar de marcos de conclusão e uma viagem só colocará o trabalhador no nó de destino quando efetivamente chegar. Essas políticas de disponibilização são específicas e permanecem abertas.

**Deliberações não tomadas por F-015:** ação simultânea para um mesmo trabalhador, divisão de trabalhadores, desperdício de tempo na troca de tarefa, efeitos da interrupção, definição de materiais obrigatórios, limites exatos de produção, custos temporais, cronograma de consumo, formas de progressão das atividades e regras de concorrência sobre o mesmo nó/aresta. A possibilidade de ações **sequenciais** distintas no mesmo período foi ratificada posteriormente em F-017.

### F-016 — Unidade de progresso físico e disponibilização do resultado [ABERTO]

A próxima investigação separa **trabalho temporal realizado**, **progresso físico existente** e **resultado utilizável**.

**Três cenários mínimos:**

- **Deslocamento:** travessia que demanda, por hipótese de cálculo, 1,4 unidades de tempo e trabalhador com 1 unidade disponível por turno. Primeiro turno investe 1 em progresso na aresta; segundo investe 0,4 e efetiva chegada. Restam 0,6 unidades de tempo no segundo turno, que **podem** ser usadas em outra ação sequencial, conforme F-017, desde que satisfeitas suas condições.
- **Construção:** progresso da obra pode ser preservado fisicamente enquanto a capacidade da infraestrutura só fica operacional ao concluir marcos definidos. Nenhuma porcentagem universal de operacionalidade é presumida.
- **Produção:** tempo de extração pode resultar em material já disponível, ou precisar alcançar um lote mínimo; a regra depende do processo físico e **não** se deduz da existência de tempo acumulado.

**Pergunta de arquitetura:** há necessidade de um objeto genérico permanente de "ação em andamento" em todos os casos, ou basta persistir os estados físicos mínimos de trabalhador, aresta, obra e recurso? Preferência preliminar pela segunda opção, desde que interrupções/retomadas e determinismo sejam verificáveis.

A unidade de tempo adotada internamente, os números ilustrativos e as fórmulas de progresso **não** foram ratificados.


### F-017 — Múltiplas ações sequenciais por trabalhador no mesmo período [RATIFICADO]

Um trabalhador pode executar **mais de uma atividade distinta, em sequência**, durante o mesmo período, desde que a soma dos intervalos temporais efetivamente consumidos não ultrapasse seu orçamento temporal disponível (F-009/F-013). Não há desperdício automático de saldo temporal apenas pelo término de uma tarefa anterior. A aplicação de cada ação deve respeitar a localização e o estado **resultantes da ação precedente**.

Invariantes:
1. Para um trabalhador, intervalos de ações distintas não se sobrepõem; não há simultaneidade de duas atividades deliberadas da mesma unidade.
2. Uma atividade só usa recursos/infraestrutura que estejam acessíveis e válidos **quando começar a ser executada**; concluir uma travessia é necessário antes de produzir no destino.
3. Tempo não utilizado permanece disponível até o encerramento do período; **não se presume transferência de saldo para o período seguinte**.
4. Resultado de ação anterior pode fundamentar uma ação posterior **apenas quando o resultado já estiver fisicamente disponível** conforme F-016.
5. A continuidade de tarefa entre períodos (F-015) e a possibilidade de alternar tarefas no mesmo período (F-017) são princípios distintos.
6. Não adicionar automaticamente tarifas de troca de atividade ou custos de ativação. Só introduzir se forem necessários e fisicamente justificados por regra posterior.

Exemplo exclusivamente didático: disponibilidade temporal \`T=1\`, deslocamento de \`0,4\` e produção de \`0,6\` no destino, se houver chegada e condições físicas para produzir. O orçamento termina em zero. Esses valores não são parâmetros aprovados.

**Limites do escopo:** esta decisão não define a ordem global dos trabalhadores, o modelo de concorrência, a escolha de comandos, a existência de ação instantânea, o consumo de demanda, nem a forma exata de particionar intervalos dentro do período.

### F-018 — Arbitragem entre trabalhadores em tempo compartilhado [ABERTO; prioridade seguinte]

O sistema não pode concluir de F-017 que "executar todas as ações do trabalhador A e depois todas as do B" seria automaticamente justo ou fisicamente coerente. Dois agentes podem disputar a mesma oportunidade de produção, material, trecho de transporte, obra ou disponibilidade de recurso. A ordem pode alterar o resultado.

**Cenário de risco:** o trabalhador A gasta \`0,4\` chegando a um nó e passa a produzir; o trabalhador B já se encontra nele. Processar todo A antes de B pode dar a A acesso retroativo a recursos que B poderia ter utilizado durante os mesmos intervalos de tempo.

**Diretriz proposta, não ratificada:** um período tem uma referência temporal comum aos agentes, mesmo que o motor execute computacionalmente eventos em ordem determinística. Identificar pontos onde as condições físicas mudam (chegadas, conclusões, disponibilizações, esgotamentos) e resolver a concorrência apenas quando necessário. Não presumir que o motor precise simular cada fração de segundo nem percorrer todos os trabalhadores em microticks.

**Alternativas a comparar:**
- A. Ordem inteira por trabalhador (simples, mas forte viés potencial de prioridade).
- B. Subetapas globais fixas (regras simples, mas granularidade arbitrária e custo elevado).
- C. Eventos temporalmente posicionados e arbitragem local para conflitos efetivos (potencialmente eficiente, mas exige um contrato mínimo de prioridade/empate).
- D. Resolução agregada local para produções contínuas e uso de eventos apenas em transições discretas (pode reduzir custo, a testar).

**Testes antes de ratificar:** dois trabalhadores e uma única árvore/obra/aresta; mesma ordem de comandos com IDs permutados; replay determinístico; conservação de materiais; ausência de tempo duplicado; respeito à causalidade temporal; escalabilidade de um Goldberg gigante.

**Não decidido:** duração numérica do período, resolução de empates, prioridade de ordens, acesso concorrente, instantaneidade de eventos, subdivisão de ações, tempo em viagem. Este tópico não pode forçar exceções implícitas na fórmula da F-011.


## 8. Governança da nova versão e critérios de reaproveitamento

**A fonte de verdade do novo desenho** é a sequência de decisões ratificadas nesta documentação. As decisões do jogo antigo **não exercem autoridade sobre ela**; tampouco foram revogadas para a versão antiga.

**Portão 1 — Formulação autônoma:** redigir leis, estados, casos-limite e invariantes sem consultar limitações ou soluções do código existente; não conservar subsistemas por inércia.

**Portão 2 — Validação conceitual:** confrontar cada lei com experimentos mínimos, contradições, custos de complexidade e coerência entre eras.

**Portão 3 — Contratos de implementação:** somente após ratificação, especificar interfaces mínimas e testes para a nova arquitetura, sem compatibilidade obrigatória com versões anteriores.

**Portão 4 — Triagem de legado:** comparar cada componente existente contra os contratos novos; classificar como reutilizável sem alteração, adaptável sem comprometer o contrato ou descartável. A facilidade de reaproveitar não justifica alterar uma lei já aprovada.

**Portão 5 — Implementação independente:** integrar somente partes aprovadas. Quando incompatíveis, substituir integralmente; não criar adaptadores permanentes, camadas de tradução ou estados duplicados apenas para preservar o passado.

**Gate documental mínimo para mudanças nesta nova versão:**
- citar os IDs F-* efetivamente aplicáveis;
- listar novas premissas para ratificação;
- demonstrar os invariantes afetados nos cenários mínimos;
- não converter pontos [PROPOSTO]/[ABERTO] em regra silenciosamente;
- distinguir a nova especificação de qualquer código legado citado como candidato de reaproveitamento.

**Observação de versionamento:** o apelido da nova versão, a estratégia de branches/repositórios e a data de migração ainda são questões operacionais abertas. Nenhum merge, descarte físico de código ou nova linha executável foi autorizado por este documento.

## 9. Histórico

- **2026-11-07 — abertura:** base geométrica/topológica, nó físico-produtivo, três recursos, capacidade natural e duas infraestruturas; ratificação explícita do **Modelo A — Trabalhador físico**.
- **2026-11-07 — segunda deliberação:** F-009 ratificada (capacidade universal de trabalho); F-010 ratificada (nova versão de desenho autônomo e descarte de código incompatível sem restrição de retrabalho); F-011 aberta (lei de produção).
- **2026-11-07 — estudo F-011:** examinada a validade local da produção linear e formulado candidato de saturação por oportunidades físicas, sem ratificação de fórmula nem parâmetros.
- **2026-11-07 — proposta F-012:** trabalhador como agente físico com uma capacidade universal de ação e um vetor de demanda; distinção entre demanda e consumo efetivo; nenhuma regra nova ratificada.
- **2026-11-07 — terceira deliberação:** F-012 aprovada no nível estrutural (agente = localização + ação + demanda) e F-013 aprovada como diretriz (orçamento único de tempo para ações). F-014 permanece aberta, assim como a dinâmica de produção F-011.
- **2026-11-07 — quarta deliberação:** F-015 ratificada (progresso temporal preservado entre turnos, sem aplicação antecipada ou duplicada de efeitos), F-016 aberta (representação física do progresso e disponibilidade de resultados).
- **2026-11-07 — quinta deliberação:** F-017 ratificada (ações distintas sequenciais no mesmo período, limitadas ao tempo disponível); F-018 aberta (arbitragem e causalidade temporal entre diferentes trabalhadores).
