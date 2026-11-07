# GA-FOUNDATION-000 — Revisão fundamental do Global Arena

**Data de abertura:** 2026-11-07  
**Versão conceitual:** 1.1  
**Situação:** deliberação em andamento; F-021 altera a arquitetura econômica anteriormente explorada  
**Escopo:** formulação de uma **nova versão conceitual autônoma** do jogo, sem dependência do kernel anterior; sem refatoração de código nesta entrega

> **Regra editorial:** este texto distingue rigorosamente o que foi decidido daquilo que ainda está sendo discutido. Nenhuma hipótese, exemplo ou equação ilustrativa pode ser implementada como regra canônica sem ratificação explícita.
>
> **Precedência conceitual:** F-021 remove os **estoques persistentes, a logística executada de mercadorias e o consumo item-a-item**, mas é esclarecida por **F-023**: as mercadorias continuam existindo como **quantidades setoriais de oferta/demanda**, negociadas e monetizadas por mercado espacial com escoamento abstrato. F-024 propõe separar impedância física e tarifas de infraestrutura. Os experimentos materiais anteriores são históricos, não requisitos de implementação.

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

O escopo inicial inclui **madeira, pedra e alimentos como mercadorias/setores economicamente distintos**: cada um pode ter quantidades produzidas, oferta, demanda, preço de mercado e escoamento espacial, porém **sem estoque persistente, carga material ou transporte executado no mapa** (F-021/F-023). O carvão é outro exemplo ilustrativo, não ampliação obrigatória do catálogo inicial. O valor econômico vem do **mercado**, não só de tempo e produtividade do nó.

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

Não se presumem edifícios específicos, níveis, custos, receitas ou regras de manutenção. Por F-021, construção de infraestrutura não exige diretamente madeira, pedra ou alimentos: seus insumos econômicos são monetários, além do trabalho que vier a ser requerido para executar a ação.

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
3. **Demanda econômica/consumo abstrato**, expresso monetariamente conforme F-021, com composição e atendimento ainda a definir.

A presença da demanda é ratificada; **sua composição, geração, periodicidade, prioridade, substituibilidade, satisfação e consequências de não atendimento permanecem em aberto**. Após F-021, não presumir que a demanda crie baixas de itens físicos ou obrigue transferência de lotes materiais. A unidade não corresponde obrigatoriamente a um ser humano individual.

Nó e aresta oferecem o contexto físico que condiciona as ações; a infraestrutura modifica características desses elementos. O modelo do agente não exige mercado físico de mercadorias, mas F-021 passa a prever dinheiro e registros econômicos agregados; sua origem, preços e fluxo contábil permanecem em aberto.

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

### F-014 — Demanda e consumo abstratos [RATIFICADA A ABSTRAÇÃO; REGRAS EM ABERTO]

Após F-021/F-023, a demanda do trabalhador poderá manter **composição por mercadoria** (madeira, pedra, alimentos etc.) e contribuir para **curvas monetárias de demanda/valoração do mercado espacial**, sem exigir estoque, entrega individual ou consumo material executado. A liquidação econômica precisa ser distinta do consumo item-a-item.

**Estão ratificados somente a existência de demanda e o abandono de estoques/fluxos físicos de mercadorias como mecanismo necessário de satisfação.**

**Continuam abertos:** formação e periodicidade da demanda, composição setorial, preço/curva de valor, condições de atendimento (caixa do agente, de grupo, de território ou outro), orçamento e sua procedência, relação com produtividade/bem-estar, insuficiência e eventual gasto adicional por atividade. Dinheiro fungível não implica apagar preferências ou demandas diferenciadas entre mercadorias.

**Invariante desejável:** distinguir necessidade econômica calculada, despesa efetivamente liquidada e resultado de atendimento; demanda não equivale por si só a pagamento, nem autoriza gerar moeda em duplicidade. Essa norma de liquidação precisa ser concluída em F-022.

### Experimentos do orçamento universal [REINTERPRETADOS POR F-021]

- **Trabalhador imóvel:** tempo de ação não utilizado; demanda monetária pode existir sem produção.
- **Trabalhador em produção:** aplica tempo no nó e gera **quantidade ofertável por mercadoria**; escoamento abstrato e mercado espacial precificam a produção; a venda gera receita, sem estoque persistente.
- **Trabalhador em construção:** aplica tempo caso a obra necessite execução física; os demais insumos econômicos são **somente monetários**, nunca materiais retirados de estoques do jogo.
- **Trabalhador em deslocamento:** aplica tempo nas arestas, preserva progresso físico; não transporta carga de mercadorias como exigência econômica canônica.
- **Trabalhador misto:** pode alternar tarefas sequencialmente no mesmo turno (F-017), sem duplicar tempo.

### Revisão de prioridade da F-011

A formulação anterior \`Q=R×min(W,A)\` (Q como quantidade por mercadoria) e a alternativa de saturação suave permanecem **hipóteses não ratificadas de produção quantitativa**, sem implicar estoques físicos. F-023 esclarece que **Q tem significado antes da monetização**; o rendimento monetário é calculado posteriormente, pelo escoamento e preço de mercado. Antes de fixar \`A\`, \`R\` ou qualquer curva, investigar o processo geral **ação no espaço → transformação física de estado**, com restrições do recurso. Essa ordem evita construir a arquitetura em torno de um teto inventado.

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

### F-018 — Tempo lógico global e causalidade física [RATIFICADO COMO PRINCÍPIO]

Todos os agentes físicos compartilham uma mesma **referência de tempo lógico**. A ordem usada pelo processador para executar operações não pode, por si só, modificar a cronologia nem o resultado físico do mundo.

**Invariantes ratificados:**
1. Toda ação ocupa um intervalo lógico compatível com a posição física e com o orçamento temporal único do trabalhador (F-013/F-017).
2. Uma ação somente pode depender de materiais e efeitos já existentes ou disponíveis no instante lógico em que eles forem usados; não há utilização retroativa nem acesso antecipado.
3. A ordenação de processamento interno **não** constitui uma regra canônica de prioridade entre trabalhadores.
4. Se o motor trocar a ordem interna de processamento preservando os mesmos comandos, condições e eventos lógicos, não poderá alterar os efeitos materiais por esse único motivo.
5. A reconstituição determinística de um mesmo histórico deve reproduzir os mesmos estados e transições; concorrência e empates devem ser resolvidos por contratos explícitos, não pelo acaso da execução.
6. Não exigir microturnos fixos: a implementação pode otimizar cálculos locais e resolver transições relevantes, desde que preserve causalidade e conservação.

**Limites da aprovação:** não foram ratificados o algoritmo específico de arbitragem, preferências de acesso, direitos sobre materiais, divisão proporcional, prioridades, modelo de eventos, representação do tempo interno, nem distribuição de recursos indivisíveis. A realização de eventos exatamente simultâneos ainda precisa de uma regra explícita. Ser temporalmente coerente **não implica** distribuir recursos igualmente nem garantir qualquer direito de acesso.

### F-019 — Arbitragem local de concorrência física [RATIFICADO COMO PRINCÍPIO]

A concorrência é resolvida **no local e no intervalo temporal em que realmente ocorre**, somente quando duas ou mais ações válidas dependem da **mesma disponibilidade física limitada** (material, oportunidade de atuação, capacidade da infraestrutura ou passagem). Não se presume disputa quando as ações são independentes.

**Contrato conceitual mínimo:**
1. **Determinar a elegibilidade física e os direitos de acesso aplicáveis** no instante lógico da ação. A existência de capacidade física não cria automaticamente direito de uso.
2. **Verificar se há concorrência real:** participantes válidos, períodos sobrepostos e demanda total de uso superior à disponibilidade compartilhada. Se não há conflito, resolver diretamente.
3. **Arbitrar apenas o bem/capacidade realmente disputado:** a resolução precisa conservar quantidades materiais, respeitar a cronologia e não duplicar tempo de ação ou produção.
4. **Aplicar resultados causais aos participantes**, registrando o que foi realmente utilizado, o que permaneceu disponível e o tempo efetivamente empregado; trabalho impedido por indisponibilidade não é consumido automaticamente.
5. **Preservar o determinismo sem atribuir precedência ao loop interno do motor.** Empates e conflitos exatos de simultaneidade exigem uma política explícita posteriormente ratificada.

**O que esta decisão não canoniza:** rateio proporcional, fila primeiro a chegar/primeiro a ser servido, desempate por ID, prioridades econômicas, mercado, propriedade específica, capacidade nominal por tile, granularidade numérica, custo por tentativa frustrada ou obrigação de microturnos globais. Essas são questões distintas.

**Interpretação física:** o recurso pode ser uma quantidade divisível; ou uma unidade indivisível; ou uma capacidade de acesso, na qual só uma quantidade limitada de agentes pode trabalhar ou atravessar simultaneamente. O núcleo da arbitragem é comum, mas as consequências físicas podem diferir.

### F-020 — Modalidades mínimas de arbitragem local [ABERTO; dois experimentos]

**Cenário A — Material divisível:** nó com 1 unidade de madeira acessível, sem regeneração no intervalo; B começa em \`t=0\`, A chega em \`t=0,4\`; cada um consegue extrair 1 unidade por unidade de tempo; ambos têm igual direito, operam simultaneamente e não interferem em capacidade operacional. Sob a hipótese de divisão por **taxa efetiva de extração**, B extrai 0,4 até a chegada de A e depois ambos extraem 0,3, com esgotamento em \`t=0,7\`; totais finais B=0,7, A=0,3, reserva=0. A permutação da ordem computacional deve preservar esses resultados. **Números e rateio são ilustrativos.**

**Cenário B — Recurso indivisível:** dois trabalhadores tentam ocupar o mesmo posto operacional, ou obter um único item que não pode ser dividido, **no mesmo instante lógico**. Nenhuma decisão temporal anterior determina sozinho o vencedor. O contrato futuro deve definir prioridade/empate legítimos, deterministicamente e sem duplicar a unidade. Repartição proporcional de material indivisível é inválida.

**Hipóteses a avaliar separadamente:**
- **Divisível:** participação proporcional às taxas físicas efetivas, com limites individuais e materiais disponíveis, *apenas* se os participantes forem igualmente elegíveis e puderem agir em paralelo. Caso contrário, regras de acesso/capacidade têm precedência.
- **Indivisível:** uma decisão discreta de concessão por oportunidade, com política de resolução de empate explícita. Não eleger IDs de agentes ou ordem de chegada em empate exato como princípio automático.
- **Capacidade compartilhada:** quando a limitação é operacional (posto, espaço, vazão de aresta), não a confundir com estoque de material. A infraestrutura pode modificar a capacidade sem alterar o algoritmo de autorização de acesso.

**Testes de aceitação antes de ratificação:** invariância à permutação de processamento; conservação de quantidade e acesso exclusivo de unidade indivisível; respeito aos instantes de chegada; saldo de tempo do agente após esgotamento ou bloqueio; concorrência repetida sem duplicar posse; resultados estáveis com mudanças na escala de discretização; execução local eficiente em grafos grandes.

**Próxima decisão prioritária:** escolher se a modalidade proporcional por taxa física é a norma-padrão *somente* para recursos genuinamente divisíveis, de acesso equivalente, com ações paralelas possíveis; e decidir como modelar um recurso indivisível sem arbitrar por mero ID. Nenhum destes métodos está ratificado.


### F-021 — Abstração de estoque, consumo e construção [RATIFICADO; ESCLARECIDO POR F-023]

**Decisão:** manter o mundo, os trabalhadores e suas ações físicas, mas **não manter estoques operacionais persistentes de mercadorias produzidas, nem executar cargas, entregas individuais e insumos específicos de materiais de obra**. O resultado financeiro da atividade produtiva é dinheiro **recebido após a valoração/venda de quantidades setoriais**, não emissão direta por tempo de trabalho.

**Contratos ratificados:**
1. O trabalhador permanece fisicamente localizado e aplica orçamento temporal universal às atividades.
2. O nó possui potenciais físicos para quantidades produzíveis de mercadorias **distintas** (madeira, pedra, alimentos; carvão somente como exemplo).
3. A produção apura uma **quantidade por mercadoria e ciclo**. A quantidade participa de escoamento/precificação abstratos (F-023) e não entra em inventário de mercadorias persistente.
4. O dinheiro é a **receita da venda no mercado**, dependente do preço de mercado e volume vendido. Não igualar quantidade produzida, valor potencial e receita realizada.
5. A demanda/consumo pode ser representada economicamente por mercadoria/setor, mas não exige retirada de lotes físicos no mapa.
6. Infraestrutura de nós/arestas custa **dinheiro em lugar de madeira, pedra ou outros insumos específicos**; o componente temporal de construção permanece sujeito a F-013/F-015 e à definição posterior de conclusão.
7. Arestas continuam representando deslocamento físico de agentes, além de uma **rede abstrata de custo de escoamento comercial**. O escoamento de mercadorias pode ser calculado sem simular cargas/veículos.

**Fora do novo núcleo operacional:** estoques físicos permanentes por produto, lotes individuais, custódia de cargas, entrega comercial individualizada, cadeia de materiais obrigatórios para obras. **Dentro do modelo econômico:** quantidades setoriais transitórias por ciclo, oferta/demanda, preços, receita, rotas abstratas e custos físicos de acesso aos mercados.

**Governança:** não reusar, de forma automática, o water-filling ou as fórmulas do código anterior; reavaliar princípios e só então auditar o reuso (F-010).

### F-022 — Financiamento e liquidação monetária [ABERTO]

A venda de mercadorias geradas em quantidades abstratas produz receita monetária, mas ainda precisamos definir **quem paga, qual orçamento financia a demanda e como se liquidam as transações**. Não inferir criação automática de moeda por produção.

**Alternativas em aberto:** transferência com compradores/contas efetivas; mercado abstrato com balanço agregado e fontes/sumidouros explícitos; arquitetura híbrida com demanda calibrada, desde que não haja dupla contagem de poder de compra.

**Decisões pendentes:** caixa por trabalhador/nó/instituição, liquidação dos gastos de consumo, origem e destino do dinheiro, preço final pago, tratamento de não venda quando demanda/orçamento insuficiente, financiamento de infraestrutura e possíveis impostos. Quando um custo apenas **condiciona preço**, não lançar simultaneamente despesa de caixa não especificada.

### F-023 — Mercado espacial: quantidade, preço e water-filling [RATIFICADO NO NÍVEL DE DESENHO]

A produção tem dois estágios lógicos distintos:

1. **Produção quantitativa no nó:** tempo de ação, capacidade natural, propriedades físicas, tecnologia e infraestrutura produtiva determinam a quantidade ofertável de uma mercadoria num ciclo. A fórmula exata de quantidade permanece F-011 [ABERTO].
2. **Escoamento e venda abstratos:** oferta por origem/mercadoria é alocada a destinos de demanda mediante um mecanismo **do tipo water-filling**, que considera curvas de valor/preço de mercado e **impedância física de transporte** das rotas; a receita resulta de quantidades efetivamente vendidas a preços de mercado, não de um fator monetário fixo de produtividade.

**Sem transporte de mercadorias em tempo real:** as rotas são usadas para precificação/alocação de **fluxos contábeis transitórios por ciclo**, não para movimentar cargas individualizadas no mapa nem acumular produtos não vendidos em estoques persistentes.

**Separação obrigatória:** quantidade produzida \`q_prod\`; quantidade efetivamente alocada/vendida \`q_venda\`; preço de transação \`p\`; receita bruta \`Σ(q_venda × p)\`; receita líquida após **despesas monetárias explícitas**. O custo físico de transporte influencia competitividade e preço, **não se transforma automaticamente em débito ou crédito de dinheiro**.

**Ainda abertos:** função exata de market clearing/water-filling, forma dos preços marginais, orçamento dos compradores, escoamento total versus oferta não absorvida, escala de cálculo, agregação de demanda de trabalhadores, eventuais restrições de capacidade em rotas e relação com consumo. Não assumir que toda a produção encontre comprador com pagamento garantido, salvo se deliberado.

**Invariante financeiro desejável:** quando uma venda monetária for liquidada entre contas representadas, o valor pago pelo comprador e recebido pelo vendedor deverá reconciliar, salvo transferências monetárias adicionais explícitas; a diferença de atratividade induzida por impedância física não é pagamento invisível a transportador.

### F-024 — Impedância física de transporte e cobrança por infraestrutura de terceiros [PROPOSTO]

**Esclarecimento ratificado:** o **custo físico de escoamento** é uma grandeza de impedância/deslocamento/logística, que condiciona a formação de preço e a alocação via mercado, **sem ser despesa de caixa nem receita financeira de transportador**. Não canonizar "pontos de movimento" como unidade definitiva.

**Hipótese de infraestrutura a validar:**
1. Uma aresta possui impedância física natural \`c0\`; uma infraestrutura de transporte reduz esse custo para \`cI\`, com \`cI ≤ c0\` nos casos de melhoria.
2. A redução \`Δc = max(0, c0 - cI)\` beneficia qualquer usuário admitido, seja titular ou não, pois a **impedância física real** passa a ser \`cI\`.
3. O **titular** da infraestrutura está **isento de cobrança monetária pelo uso da infraestrutura própria**, sem escapar da impedância física residual.
4. Um **não titular** pode arcar com uma **tarifa monetária de acesso**, atribuída a vendedor ou comprador segundo contrato futuro, e calculada com base no ganho físico \`Δc\` e na utilização; essa tarifa é **separada** do custo físico \`cI\`.
5. Exemplos ilustrativos: \`c0=1.000\`, \`cI=200\`, economia de \`800\` unidades físicas; a tarifa monetária usa alguma função \`T = F(Δc, volume, condições de mercado, direitos)\`. **Não** converter 800 diretamente em 800 unidades de dinheiro.
6. Em rota com múltiplas arestas ou titulares, o cálculo deve ser local/atribuível, evitando cobrança duplicada e sem usar "melhoria anterior" como referência móvel sem decisão específica.

**Cautelas fundamentais:**
- A **tarifa é efetiva saída de caixa** para a parte designada; sua contrapartida (receita do titular, conta institucional ou sumidouro explícito) está **EM ABERTO**. Não presumir que o custo físico resulte em pagamento.
- Se a cobrança for do **vendedor**, uma alternativa de liquidação é receita líquida = receita bruta de venda menos tarifa. Se for do **comprador**, não debitá-la novamente ao vendedor. A titularidade do vendedor/comprador e critérios de responsabilização ainda exigem definição.
- Water-filling deve ser sensível ao custo **efetivo** que afeta a decisão de rota/preço, mas **não descontar a mesma tarifa duas vezes** (uma na valoração líquida, outra como gasto financeiro).
- A cobrança não deve, por padrão, tornar toda melhoria infrutífera ao usuário; eventual vinculação da tarifa ao valor econômico da economia física, teto percentual de benefício, competição entre rotas e cenários de acesso devem ser estudados.
- A expressão "titular" precisa identificar quem possui direito sobre qual **segmento de infraestrutura**, especialmente em rotas com múltiplos proprietários, e não inferir automaticamente propriedade total da rota.

**Status:** proposta de incentivo e monetização de infraestrutura, **não** ratificação de função de tarifa, recebedor, payer, conversão unidade física/moeda ou regra de rateio.

### Experimento recomendado para próxima deliberação [ABERTO]

Dois produtores de carvão, um titular e outro não titular de uma infraestrutura, acessam um mercado remoto. Calcular, sob a mesma curva de demanda, a receita bruta que resulta do escoamento com \`c0=1.000\` e com \`cI=200\` (valores exemplificativos), depois adicionar **somente ao não titular** uma tarifa explícita baseada no benefício físico, verificando se:
- ambos se beneficiam do mercado mais acessível;
- a tarifa corresponde a uma movimentação financeira identificável e jamais ao custo físico do transporte;
- a rota, o preço e a receita não recebem dois descontos para o mesmo fenômeno;
- titularidade, capacidade e melhorias de arestas não criam dinheiro ou receita duplicados;
- o resultado segue relevante em diferentes eras sem inventários físicos de mercadorias.


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
- **2026-11-07 — quinta deliberação:** F-017 ratificada (ações distintas sequenciais no mesmo período, limitadas ao tempo disponível); F-018 aberta à época.
- **2026-11-07 — sexta deliberação:** F-018 ratificada no nível de princípio (tempo lógico global, causalidade e independência da ordem interna de processamento); F-019 aberta à época.
- **2026-11-07 — sétima deliberação:** F-019 ratificada como arbitragem física local apenas diante de conflitos reais, sem política automática de repartição; F-020 aberta à época, sob o modelo físico de mercadorias.
- **2026-11-07 — oitava deliberação:** F-021 ratificada (economia monetária abstrata, produção convertida em dinheiro, sem estoques ou logística de mercadorias; infraestrutura com componente monetário em vez de madeira/pedra/alimentos). F-022 aberta para contabilidade da emissão/transferência, consumo e contratação de obras; F-020 sobre mercadorias perde prioridade como hipótese histórica.
- **2026-11-07 — esclarecimento subsequente:** F-023 ratificada no nível de desenho: produção de quantidade diferenciada por mercadoria, escoamento abstrato via water-filling e preço determinado por mercado; sem estoque persistente. O custo físico de transporte forma preço, mas não é fluxo de caixa. F-024 proposta: infraestrutura reduz impedância para todos, titular não paga uso próprio e não titular pode pagar tarifa ligada à economia física; fórmula, pagador/recebedor e liquidação ainda abertos. A F-021 foi revisada para remover interpretação incorreta de emissão monetária direta por trabalho.
