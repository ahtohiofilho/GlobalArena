# GA-FOUNDATION-000 — Revisão fundamental do Global Arena

**Data de abertura:** 2026-11-07  
**Versão conceitual:** 3.6  
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

**Delimitação superveniente F-034:** toda **infraestrutura logística/transporte passível de investimento** pertence **exclusivamente a uma aresta individual**, não aos nós. Os nós seguem como locais de produção e podem possuir potenciais naturais ou, se deliberado futuramente, modificadores produtivos: esta decisão de logística **não** revoga automaticamente a categoria produtiva de nó de F-006. Novos investimentos produtivos em nós não estão sendo definidos agora.

### F-007 — Capacidade natural pré-infraestrutura [RATIFICADO]

Nós, arestas e trabalhadores possuem **capacidades naturais de base**, sem exigir infraestrutura construída para entrar na dinâmica. A infraestrutura é um modificador de capacidades, não uma licença para que a mecânica exista.

**Interpretação restritiva:** capacidade natural não significa que todo nó produza todo recurso; o potencial de um recurso em certo nó pode ser zero. Não se fixam valores positivos universais, velocidade ou produtividade.

**Esclarecimento F-030 (ratificado):** o nó/aresta natural **já é infraestrutura funcional** no sentido operacional; a melhoria construída por investimento modifica essa mesma infraestrutura, e o estado `investimento=0` deve entrar integralmente nas equações. A naturalidade não implica capacidade ilimitada, gratuidade física, circulação garantida ou cobrança financeira.

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

**Continuam abertos:** atualização do fator `k`, fórmula final de preço, condições de pagamento (caixa do agente, grupo, território ou outro), orçamento e sua procedência, relação com produtividade/bem-estar e eventual gasto adicional por atividade. **F-041 ratifica 1 unidade de demanda de referência por trabalhador e produto e venda universal; não é teto de quantidades compráveis**. Dinheiro fungível não implica apagar preferências ou demandas diferenciadas entre mercadorias.

**Invariante superveniente F-046:** distinguir demanda de referência, quantidade efetivamente absorvida pelo mercado e receita de venda creditada ao titular. Demanda não é carteira nem desembolso de comprador representado. **Não exigir registro de despesa individual do consumidor** como condição para atendimento; o setor comprador é externo ao perímetro contábil do jogo.

**Clarificação F-039/F-041 [RATIFICADO]:** sem estoques e sem demanda artificial de retenção. **Toda produção é vendida** pelo water-filling; demanda é o valor de referência no denominador da curva de preços, não uma quota máxima. A base inicial é **1 por trabalhador e produto**, com futuro modificador `k`; `Q>D` reduz o preço, mas não impede a venda.

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

**Fora do novo núcleo operacional:** estoques físicos permanentes por produto, lotes individuais, custódia de cargas, entrega comercial individualizada, cadeia de materiais obrigatórios para obras. **Também está descartado expressamente (F-039) o artifício legado de aumentar artificialmente a demanda para simular retenção ou formação de estoques invisíveis.** **Dentro do modelo econômico:** quantidades setoriais transitórias por ciclo, oferta/demanda genuína, preços, receita, rotas abstratas e custos físicos de acesso aos mercados. **F-041 corrige esta hipótese**: toda quantidade produzida será alocada a mercados e vendida; preço, não redução da quantidade negociada, responde à oferta relativa. Não há estoque entre ciclos.

**Governança:** não reusar, de forma automática, o water-filling ou as fórmulas do código anterior; reavaliar princípios e só então auditar o reuso (F-010).

### F-022 — Escopo financeiro: compradores abstratos e tesouros representados [REDEFINIDO POR F-046]

**Decisão superveniente e ratificada F-046:** compradores/trabalhadores **NÃO** possuem carteira, caixa individual de consumo, saldo a conferir, orçamento de compra finito nem obrigação de pagar mediante débito de um tesouro de destino. Seu poder de compra é **indeterminado e exógeno** ao subconjunto de itens que o jogo modela. As compras desses itens representam realocação de gastos dentro de uma economia mais ampla, com outras mercadorias/serviços **invisíveis**. Não criar "compensação financeira por ciclo" ou "banco comprador institucional" para substituir as carteiras que foram expressamente descartadas.

O mercado abstrato absorve **toda a produção** por função de preço (F-041/F-043), e a receita nominal é **creditada diretamente ao titular econômico do produtor** conforme o resultado da venda. Assim, o setor monetário explicitamente representado é **aberto**: não é obrigatório reconciliar receita de vendas com pagamentos de contas que **não existem** no modelo. Essa entrada representa o setor consumidor externo/implícito, não desconto em uma conta fictícia nem emissão monetária por ação de trabalho.

**Contabilidade que de fato existe [F-048]:** **tesouro da civilização**, que recebe a receita da produção realizada pelos seus trabalhadores/agentes produtivos, remunera investimentos de infraestrutura e registra eventuais transferências financeiras entre civilizações (tarifas e outras rubricas ainda abertas), além do capital incorporado às arestas. **Não existem carteira monetária do trabalhador nem tesouro monetário por nó por padrão.** Quando duas contas reais do modelo interagirem, débitos/créditos devem reconciliar. A circulação de moeda total, impostos e outros fluxos da economia invisível **não** precisa ser simulada.

**Ainda em aberto:** atribuição da produção à civilização quando houver situações de várias civilizações no mesmo nó, tratamento de impostos (especialmente se a receita já entra diretamente no tesouro público), investimento em infraestrutura de terceiros e sua contrapartida, tarifas financeiras e eventual balanço macroeconômico opcional. **A identidade da entidade recebedora da receita comercial é a civilização — F-048 ratificada.** **Não** reabrir a exigência de carteiras dos consumidores sem decisão explícita do usuário.

### F-023 — Mercado espacial: quantidade, preço e water-filling [RATIFICADO NO NÍVEL DE DESENHO]

A produção tem dois estágios lógicos distintos:

1. **Produção quantitativa no nó:** tempo de ação, capacidade natural, propriedades físicas, tecnologia e infraestrutura produtiva determinam a quantidade ofertável de uma mercadoria num ciclo. A fórmula exata de quantidade permanece F-011 [ABERTO].
2. **Escoamento e venda abstratos:** oferta por origem/mercadoria é alocada a destinos de demanda mediante um mecanismo **do tipo water-filling**, que considera curvas de valor/preço de mercado e **impedância física de transporte** das rotas; a receita resulta de quantidades efetivamente vendidas a preços de mercado, não de um fator monetário fixo de produtividade.

**Sem transporte de mercadorias em tempo real:** as rotas são usadas para precificação/alocação de **fluxos contábeis transitórios por ciclo**, não para movimentar cargas individualizadas no mapa nem acumular produtos não vendidos em estoques persistentes.

**Separação obrigatória:** quantidade produzida \`q_prod\`; quantidade efetivamente alocada/vendida \`q_venda\`; preço de transação \`p\`; receita bruta \`Σ(q_venda × p)\`; receita líquida após **despesas monetárias explícitas**. O custo físico de transporte influencia competitividade e preço, **não se transforma automaticamente em débito ou crédito de dinheiro**.

**Ainda abertos:** função exata de market clearing/water-filling, forma dos preços marginais, integração das impedâncias da rede e cálculo das tarifas, escala de cálculo, agregação da demanda de trabalhadores, eventuais restrições de capacidade de rota e relação com consumo. **F-046 resolve o escopo dos compradores:** vendas universais são liquidadas economicamente contra o setor consumidor implícito, sem carteira compradora nem restrição financeira adicional.

**Invariante financeiro superveniente:** a receita de venda entra no tesouro do titular produtor a partir da contraparte consumidora **externa ao perímetro modelado** (F-046), sem lançamento de débito em comprador individual. **Somente transferências entre contas monetárias realmente representadas** exigem contrapartidas espelhadas. A impedância física de transporte não é pagamento a transportador.

### F-024 — Infraestrutura abstrata e tarifação por nível [RATIFICADO NO NÍVEL DE PRINCÍPIO; FUNÇÃO EM ABERTO]

Toda aresta conserva condições naturais de transporte. A infraestrutura de transporte **não é tipificada** em estrada de pedra, rodovia, ferrovia ou categorias tecnológicas semelhantes. É uma **melhoria abstrata de eficiência** vinculada ao **investimento financeiro nela incorporado**, com evolução não linear e **retornos marginais decrescentes** nos níveis superiores.

**Princípios ratificados:**
1. O investimento efetivo em uma aresta determina sua evolução de nível/qualidade conforme uma função matemática (não necessariamente patamares inteiros).
2. A melhoria reduz a **impedância física** de transporte, e sua eficiência sob utilização deve também considerar a **saturação dependente da carga de mercadorias**.
3. A impedância física é grandeza **não monetária**; altera o escoamento e a precificação espacial, mas **não é um débito financeiro nem receita de transportador**.
4. O titular e os não titulares usufruem da **mesma melhoria física**; o titular não paga tarifa pelo uso da infraestrutura própria, e o uso por não titular pode gerar **tarifa monetária associada ao nível** e ao volume físico-equivalente efetivamente escoado.
5. A tarifa é separada da impedância logística; **não cobrar duas vezes** pela mesma rubrica nem converter automaticamente pontos de impedância em moeda.
6. Infraestrutura é economicamente construída com **dinheiro** (F-021), sem estoques de materiais específicos; eventuais exigências de trabalho/tempo para sua execução continuam a ser modeladas pelo orçamento universal de ação, conforme decisão posterior.

**Ainda não ratificados:** escala/equação física de nível, limites, degradação e função que converte capital em **peso proporcional da tarifa**. A **copropriedade por quotas de capital incorporado foi ratificada em F-062**; a autoridade político-diplomática para embargo segue F-063 [ABERTO]. **F-060 resolve a natureza percentual e o rateio:** o encargo recai sobre a parcela comercial da civilização produtora, sem carteira compradora, e a parte tarifária é destinada ao tesouro do titular da aresta, quando este for definido. A tarifa não é impedância física.

### F-025 — Investimento, eficiência e capacidade logística [RATIFICADO O DESENHO ABSTRATO; FÓRMULAS PROPOSTAS]

O estado econômico relevante de cada infraestrutura deve representar **capital efetivamente investido**, e não o saldo de caixa do titular. Para reduzir estado redundante, pode-se derivar **nível, eficiência e capacidade logística** desse capital e das propriedades físicas da aresta.

**Candidato matemático para nível contínuo [PROPOSTO]:**

\`L(I) = log₂(1 + I/I₀)\`, com \`I ≥ 0\`, \`I₀ > 0\`. A forma inversa é \`I(L)=I₀(2^L−1)\`: cada unidade adicional de nível exige mais capital que a anterior. Exemplo exclusivamente ilustrativo, \`I₀=100\`: \`I=0,100,300,700,1500\` produzem \`L=0,1,2,3,4\`. **Não canonizar números.**

**Duas consequências diferentes do nível [PROPOSTAS]:**
- **Eficiência em fluxo livre:** \`c_livre(L) = c_min + (c_natural-c_min)/(1+αL)\`, \`0 < c_min ≤ c_natural\`, \`α>0\`. A infraestrutura reduz a impedância básica sem eliminá-la.
- **Capacidade de referência:** \`K(L)=K_natural(1+βL)\`, \`K_natural>0\`, \`β≥0\`. Mais investimento permite sustentar maior carga logística antes de sobrecarga severa.

As equações são **candidatos simples de retornos decrescentes**, não funções canonizadas nem indicação de que devam existir estados persistentes separados para nível e capacidade. A eficiência e a capacidade precisam ser distinguíveis **conceitualmente**, pois o investimento pode afetar uma, outra ou ambas; a intensidade de cada efeito é questão de calibração.

### F-026 — Quantidade comercial, carga logística e valor de mercado [RATIFICADO COMO DISTINÇÃO; CALIBRAÇÃO ABERTA]

Há três grandezas conceitualmente distintas: **quantidade comercial**, **carga logística equivalente** e **valor monetário**. A saturação física da infraestrutura não depende diretamente do preço de mercado das mercadorias.

**Uma única equação logística permanece em estudo:**

\`B_e = Σ_r (a_r × q_{e,r})\`

\`B_e\` é carga equivalente sobre a aresta; \`q_{e,r}\` é quantidade comercial do produto \`r\` escoada por ela; \`a_r\` é **somente um fator de conversão entre unidade comercial e unidade logística**, e **não** um parâmetro livre a ser inventado para equilibrar itens.

**Restrição de filosofia RATIFICADA:** proibir índices arbitrários escolhidos item a item para simular segurança, raridade, prestígio, perecibilidade ou diferenças de jogabilidade. **Não exigir coeficiente específico por item** enquanto não houver critério físico homogêneo e justificável para determiná-lo.

**Caminho inicial mínimo [PROPOSTO]:** escolher uma **unidade comercial já normalizada em equivalentes logísticos**, de modo que \`a_r=1\` para todos os produtos e \`B_e=Σ_r q_{e,r}\`. Se a produção do item usa unidades econômicas diferentes entre mercadorias, a comparação de \`1\` unidade de pedra e \`1\` de diamante não pode ser interpretada como igualdade física sem normalização; ajustar as **definições de unidades** a um padrão objetivo, não inventar penalidades especiais.

**Alternativa futura não ratificada:** se o jogo passar a possuir metadados fisicamente definidos e coerentes (p.ex. massa ou volume representativos), \`a_r\` poderá ser **derivado** deles e não calibrado livremente. A forma específica de derivação permanece aberta.

**Princípios que ficam:** manter uma única infraestrutura abstrata, um só cálculo de carga e uma mesma função de saturação para todos os itens. Não adicionar índices ou subsistemas de risco, segurança, escolta, seguro etc. O preço de mercado permanece separado e é determinado pelo mecanismo de mercado.

**Nota dimensional:** a operação de trocar a unidade de \`q_r\` deve preservar \`B_e\` mediante normalização equivalente. A ausência de metadados físicos não autoriza fingir que valores econômicos e volumes físicos são equivalentes.

### F-027 — Saturação e tarifas associadas ao nível [ABERTO; candidatos para experimento]

**Índice de ocupação logística:** \`ρ_e = B_e/K(L_e)\`. É adimensional e avalia pressão de fluxo relativamente à infraestrutura daquele nível. O termo "capacidade" é **referência de congestionamento**, não limite absoluto de passagem, salvo regra futura.

**Função de sobrecarga candidata [PROPOSTA]:**

\`c_efetivo,e = c_livre(L_e) × [1 + γ (ρ_e)^p]\`

Com \`γ≥0\`, \`p>1\` em exemplo. A penalização aumenta mais rapidamente em altos volumes. Sob essa escolha \`K\` não é um limite rígido; a carga maior eleva impedância e reduz competitividade. Um limite duro requer decisão independente. Exigir crescimento monótono do custo com \`B\` e não usar preço da mercadoria para calcular \`ρ\`.

**Função nominal de tarifa histórica candidata [NÃO RATIFICADA; CONTRASTAR COM F-060]:**

\`τ(L) = τ_max × L/(L+ℓ)\`, com \`τ_max≥0\`, \`ℓ>0\`; tarifa monetária por unidade equivalente logística, nula em \`L=0\`, positiva e limitada quando \`L>0\`. Uma operação por não titular pagaria \`T_e = q_{e,r} × a_r × τ(L_e)\` na aresta \`e\`. Para vários trechos, somar **somente** tarifas incidentes em trechos utilizados e alheios, sem cobrar o titular pelos próprios trechos. Nenhum parâmetro numérico ou fórmula é ratificado.

**Observações de integridade:**
1. O **custo de impedância** \`c_efetivo\` afeta acesso/precificação e não gera fluxo financeiro; \`T_e\` é transferência/despesa monetária efetiva a reconciliar.
2. A tarifa não deve ser cobrada duas vezes no market clearing e no lançamento financeiro. O mercado compara alternativas pelo resultado líquido *esperado*, e a liquidação depois aplica o débito/crédito **uma única vez**.
3. Rotas com mais de uma aresta acumulam contribuições físicas e, onde houver titulares distintos, tarifas distintas; não cobrar novamente a mesma passagem por mudança de classificação de rota.
4. A função de tarifa deve ser confrontada com rotas alternativas e incentivos de investimento. Não presumir que maior tarifa gere maior receita líquida: pode provocar desvio de fluxo.
5. A escolha de rotas/volumes no water-filling altera \`B_e\` e, portanto, \`ρ_e\` e \`c_efetivo,e\`, que podem alterar novamente as rotas. **Há acoplamento endógeno entre alocação e congestionamento.** Uma implementação futura precisará de resolução convergente, determinística ou equivalente; não assumir que uma passagem isolada do algoritmo seja matematicamente suficiente.
6. O modelo é **unidimensional**: uma carga logística equivalente compartilhada determina saturação e, se ratificada, a base física para tarifação. Não criar custo ou índice de risco/proteção por produto (F-028 rejeitada). Separar quantidade comercial, carga equivalente e valor monetário.

**Próximo experimento recomendado:** duas arestas alternativas, produtores de pedra/diamante com quantidades iguais \`q=1\`, pesos logísticos diferentes, investimento acumulado crescente, titular e não titular. Testar sensibilidade de nível, impedância em vazio, congestionamento com fluxo agregado, tarifa por carga, desvio de rota, resultado monetário da venda, inexistência de criação automática de dinheiro e independência da ordem de cálculo. Definir janela/ciclo da carga de modo compatível com o mercado, sem confundi-la com o deslocamento temporal de trabalhadores.


### F-028 — Índice específico de proteção/risco por mercadoria [REJEITADO PARA O NÚCLEO INICIAL]

Proposta de segundo coeficiente \`s_r\` por produto (segurança, fragilidade, perecibilidade), com termo adicional \`s_r R(L)\` na impedância. **Não aprovada:** exigiria arbitragem/calibração específica por item e romperia a prioridade de simplicidade e parâmetros justificáveis fisicamente.

**Decisão expressa:** prosseguir com **uma única equação de carga/saturação**, a mesma para todos os itens, sem coeficientes arbitrários de risco. Diferenças econômicas entre mercadorias continuam no **preço, na quantidade e na demanda**, calculados pelo mercado, e não em uma simulação de escolta ou segurança.

A dimensão adicional só poderá ser reaberta mediante nova deliberação, após amadurecimento do jogo e demonstração de ganho material de gameplay frente à complexidade. **Rejeição nesta versão inicial não significa proibição permanente de evolução futura.**


### F-029 — Saturação linear exploratória e investimento compartilhado [HIPÓTESE SUPERADA PELA PREFERÊNCIA EXPRESSA EM F-031; NÃO RATIFICADO]

**Objetivo:** testar a regra de saturação mais simples compatível com F-024–F-028, sem coeficientes por mercadoria, sem categorias de infraestrutura e sem restrições de fluxo rígidas arbitrárias.

**Proposta de impedância por aresta:**

\`C_e(B,L) = C_livre,e(L) × [1 + B_e/K_e(L)]\`.

\`C_e\` representa impedância física/mercadológica **não monetária** da rota. \`B_e\` é **taxa de carga logística equivalente** na mesma janela temporal de \`K_e\`; \`K_e(L)>0\` é a carga de referência em que a impedância dobra relativamente à condição de fluxo livre. Não é um teto rígido: \`B>K\` é possível, mas menos competitivo. Para um mercado processado por ciclos de duração \`Δt\`, usar \`B_e = Σ_r q_{e,r}/Δt\` **após normalização em unidades logísticas comuns**, ou manter numerador e denominador na mesma janela; nunca comparar um fluxo acumulado de seis turnos com capacidade por turno. O ciclo de mercado não precisa equivaler a um turno de trabalho.

**Proposta de investimento e melhoria única por nível:**

\`U_e(I_e) = 1 + log₂(1 + I_e/I₀)\`,

\`C_livre,e(I_e) = C_natural,e / U_e(I_e)\`,

\`K_e(I_e) = K_natural,e × U_e(I_e)\`,

\`C_e(B_e,I_e) = [C_natural,e/U_e(I_e)] × [1 + B_e/(K_natural,e × U_e(I_e))]\`.

Onde \`I_e≥0\` é capital incorporado à infraestrutura, \`I₀>0\` é escala **global de investimento**, \`C_natural,e>0\` é impedância natural de aresta transitável e \`K_natural,e>0\` sua capacidade de referência natural. As propriedades naturais podem depender de geometria/terreno, mas a origem e normalização ainda precisam ser validadas. No núcleo inicial, **não** acrescentar parâmetros independentes \`α\`, \`β\`, \`γ\` e \`p\`. A evolução de eficiência e capacidade é acoplada intencionalmente pelo mesmo multiplicador \`U\`, por parcimônia; verificar se isso limita excessivamente o jogo antes de permitir desacoplamento.

**Critérios matemáticos desejáveis:**
1. \`B=0\` implica \`C=C_livre\`; \`I=0\` implica \`U=1\`, preservando estado natural; a impedância permanece positiva para investimento finito.
2. Para \`I\` fixo e aresta transitável, \`C\` cresce linearmente com \`B\`; não há aumento de saturação sem carga.
3. Para \`B\` fixo, investir mais reduz \`C\` e aumenta \`K\`, com retornos marginais decrescentes no multiplicador de nível.
4. **Não há descontinuidade ou bloqueio** em \`B=K\`. Isto representa congestionamento suave, não limite rígido.
5. \`C\` e \`K\` não são estados persistidos separadamente se derivados de \`I\` e atributos naturais.
6. A taxa de carga usa uma unidade logística comum: produtos diferentes **não** recebem penalidades artificiais, risco, escolta ou proteção por mercadoria (F-026/F-028).

**Exemplo apenas ilustrativo:** \`I₀=100\`, \`C_natural=10\`, \`K_natural=5\`, fluxo \`B=10\` por unidade de tempo de referência:

| Capital \`I\` | \`U\` | \`K\` | \`C_livre\` | \`C(B=10,I)\` |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 1 | 5 | 10 | 30 |
| 100 | 2 | 10 | 5 | 10 |
| 300 | 3 | 15 | 3,33 | 5,56 |
| 700 | 4 | 20 | 2,50 | 3,75 |

Números escolhidos apenas para examinar monotonicidade. Não representam a economia já calibrada, os valores reais de investimento ou qualquer limite recomendado. Investimento necessário para \`U\` crescer uma unidade aumenta exponencialmente.

**Teste de rotas concorrentes [PROPOSTO]:** oferta logística total de \`12\` unidades por intervalo, duas rotas alternativas sem tarifas. Na faixa utilizada, \`C_A(x)=4+x\` e \`C_B(y)=6+y/2\`, com \`x+y=12\`. A alocação de custos iguais seria \`x=16/3≈5,33\`, \`y=20/3≈6,67\`, ambas com impedância \`28/3≈9,33\`. Um *water-filling* espacial com custos dependentes de fluxo **não** pode atribuir todas as unidades à rota inicialmente mais barata; precisa considerar a elevação endógena de impedância. A propriedade de solução/única alocação deve ser demonstrada para o modelo de mercado completo, com demanda e tarifas.

**Questão técnica prioritária:** a carga depende da distribuição do mercado, enquanto a distribuição depende das impedâncias calculadas com a carga. Uma função crescente, contínua e simples facilita procurar equilíbrio determinístico, **mas não demonstra por si só convergência** do algoritmo completo. É necessário testar água/mercado com dois caminhos paralelos, múltiplos produtores, mudanças de preço marginal, ciclos temporais diferentes e caminhos com tarifas de terceiros (registradas em dinheiro, separadamente).

**Limitações expressas da proposta:** uma penalidade linear em \`B/K\` pode ser suave demais sob uso extremo; \`U(I)\` igual para ganho de eficiência e de capacidade pode ser restritivo; \`C_livre→0\` somente no limite \`I→∞\`, mas talvez deva haver piso físico em eras futuras; arestas inicialmente intransitáveis não se resolvem com \`K_natural=0\` nesta fórmula e exigem estudo à parte. Não introduzir pisos ou multiplicadores por cautela sem evidência de necessidade.

**Status [HISTÓRICO]:** a penalização linear foi julgada inadequada para capacidade natural muito pequena (ex.: descarga de cargueiro em praia); F-031 propõe saturação convexa. A hipótese de investimento contínuo e os critérios matemáticos seguem úteis para exame, mas não foram canonizados. A tarifa monetária é **um mecanismo distinto**, não incluído na equação \`C\`.


### F-030 — Infraestrutura natural é parte integral da infraestrutura total [RATIFICADO]

**Princípio:** os **nós (tiles) e arestas do grafo nu já constituem infraestrutura funcional natural**. Não existe uma camada de transporte sem infraestrutura, seguida por outra camada que só apareça depois da construção. Há **uma única capacidade operacional total**, com atributos naturais e modificações decorrentes de capital incorporado.

Consequências ratificadas:
1. **Investimento zero é um estado normal de infraestrutura**, não ausência automática de circulação ou de serviço. Os atributos naturais determinam impedância de fluxo livre, capacidade de referência, oportunidades físicas e limites de operação mesmo sem obras.
2. **Infraestrutura natural é limitada**: uma aresta costeira natural pode suportar pequena operação abstrata de desembarque, enquanto essa **mesma aresta** após investimento pode sustentar fluxo muito maior. A penalidade por sobrecarga deve tornar fluxos excessivos fortemente pouco competitivos, sem criar categorias de porto/estrada. O exemplo histórico de investimento no nó foi substituído por F-034.
3. **[Superado por F-033/F-034 quanto à logística de interface]** A hipótese inicial admitia capacidade adicional de carga/descarga no nó. A decisão posterior estabelece que a **capacidade de interface terra↔mar e todo investimento logístico residem na própria aresta** de contato, sem duplicar operações/capacidades no nó.
4. Melhoria construída não cria outro mecanismo de transporte; apenas altera parâmetros/capacidades da infraestrutura natural subjacente. Não introduzir tipologia fixa (praia, porto, rodovia, ferrovia) como classe de infraestrutura: tais nomes são exemplos físicos ou descrições de contexto.
5. Mesmo quando nenhuma melhoria pertence a um investidor, o **custo físico** natural de uso continua participando do water-filling. **Cobrança monetária por infraestrutura natural não é inferida** dessa dificuldade física. Eventual tarifa sobre investimento alheio segue contrato específico F-024/F-027.
6. Não atribuir automaticamente a uma única capacidade de nó a soma de produção agrícola, indústria e carga/descarga sem base física. A mesma entidade espacial pode ter diferentes **oportunidades de ação**, sem exigir categorias de infraestrutura construídas.

**Contrato de custo de rota candidato [PROPOSTO]:**
\`C_rota = Σ_{arestas e percorridas} C_e + Σ_{nós n com operação logística efetiva} C_n\`.

**[Hipótese histórica de F-030, substituída para logística de interface por F-034.]** O custo de embarque/desembarque deixa de ser uma segunda parcela de nó: pertence à **aresta terra↔mar**. Não somar duas vezes a mesma operação. Os potenciais produtivos naturais dos nós continuam pertinentes à produção, e não implicam um investimento logístico no nó.

**Caso-limite:** \`K_natural>0\` para operações naturalmente possíveis, mesmo que muito restritas. Um elemento realmente intransitável, com \`K_natural=0\`, não pode ser inserido diretamente numa expressão que divide por \`K\`; a possibilidade de habilitá-lo via investimento/tecnologia é decisão posterior.

### F-031 — Saturação convexa de capacidade natural e melhorada [PROPOSTO; CRESCIMENTO FORTE É REQUISITO]

**Requisito vindo do usuário:** a penalidade logística precisa **crescer fortemente quando a carga supera a capacidade de referência**, especialmente na infraestrutura natural de capacidade pequena. A lei linear \`1+ρ\` de F-029 não satisfaz bem o exemplo da praia frente a um cargueiro.

**Candidato mínimo**, com mesma regra para **nó e aresta**, e para **todas as mercadorias**:

\`ρ_x = B_x/K_x(I_x)\`

\`C_x(B_x,I_x) = C_livre,x(I_x) × [1 + (ρ_x)^p]\`, com \`p>1\`, para \`x\` um nó/aresta com operação logística, \`B_x≥0\`, \`K_x(I_x)>0\`.

\`B_x\` é taxa de carga equivalente na mesma janela temporal usada para \`K_x\`; \`C_livre,x\` inclui a impedância física natural ajustada pelo investimento. O expoente **global** \`p\` é o único controle adicional de convexidade; **não** atribuir \`p\` por produto, nó ou tipo de instalação sem decisão ulterior.

**Exemplo de forma (não ratificada):** \`p=4\`. Multiplicadores da impedância livre:
- \`ρ=0\` → 1; \`0,5\` → 1,0625; \`1\` → 2;
- \`ρ=1,5\` → 6,0625; \`2\` → 17; \`3\` → 82.

A curva é contínua e finita para toda carga finita, mas cresce intensamente após \`B≈K\`. Não confundir **congestionamento extremamente alto** com uma **proibição absoluta de fluxo**; um limite duro/assíntota finita precisaria ser aprovado expressamente. Comparar futuramente \`p=3\`, \`p=4\` e \`p=5\` apenas por comportamento agregado, sem ajustar coeficientes item a item.

**Integração conceitual com F-025/F-029 [PROPOSTA, NÃO RATIFICADA]:**
\`U_x(I)=1+log₂(1+I_x/I₀)\`,
\`C_livre,x(I)=C_natural,x/U_x(I)\`,
\`K_x(I)=K_natural,x U_x(I)\`.
Portanto \`C_x = [C_natural,x/U_x]×[1+(B_x/(K_natural,x U_x))^p]\`.

O estado natural \`I_x=0\` implica \`U_x=1\`; as condições físicas naturais entram **integralmente** na equação. Para \`K_natural,x\` muito pequeno, a sobrecarga pode ficar extremamente cara até mesmo com fluxo comercial moderado. A capacidade cresce com investimento sem criar uma categoria artificial de instalação. **Não canonizar essas funções nem o expoente nesta fase.**

**Teste da praia [PROPOSTO]:** comparar um nó litorâneo com capacidade natural pequena e um nó natural semelhante com capital incorporado, para **um mesmo fluxo de desembarque**; agregar as arestas adjacentes e eventuais operações dos nós como custos físicos separados e localmente determinados. Confirmar que praia pode atender baixo fluxo, mas sofre penalidade explosiva com volume muito alto; que um porto conceitual emerge apenas do investimento, sem classe "porto"; e que os fluxos comercialmente atribuídos ao nó/aresta não superam fisicamente uma restrição que seria de outra ordem. O termo "cargueiro" é **analogia**, não autorização para simular veículos ou cargas individuais.

**Riscos em aberto:** congestão convexa no water-filling pode exigir cálculo de equilíbrio mais cuidadoso; uma rota com alto custo marginal ainda pode receber pequeno fluxo; congestionamento extremo pode causar números muito grandes — exigir aritmética numérica estável; cálculo de nós deve contabilizar apenas operações reais e evitar dupla contagem por passo de grafo; geometria/topologia não podem atribuir capacidade natural infinita.

**Governança:** F-030 é ratificada como concretização de F-007. F-031 documenta a exigência de penalidade fortemente convexa e a função \`1+ρ^p\` como **candidata**, sem canonizar \`p=4\`, a fórmula de investimento, quaisquer capacidades naturais ou a regra de tarifa.


### F-032 — Teste de water-filling com gargalo natural e rota alternativa [EXPERIMENTO; NÃO RATIFICADO]

**Objetivo:** testar a F-030 (nó natural como infraestrutura), a forma convexa F-031, o ganho do investimento e a natureza do \`C(B)\` usado pelo mercado. O experimento separa fluxo logístico abstrato de cobrança monetária. Não requer simulação de cargueiro ou estoque de carvão/pedra.

**Cenário controlado:** um mercado recebe exatamente \`Q=6\` unidades equivalentes **por intervalo de referência**, com demanda suficiente, sem variação de preços nem tarifas neste teste isolado. Há duas rotas alternativas:

- **A — curta, descarregando em praia natural de baixa capacidade:** contribuição fixa das arestas = 2; nó de descarga com impedância natural livre = 1 e capacidade natural \`K_A=1\`. Ao investir \`I\` nesse nó, usar provisoriamente \`U(I)=1+log₂(1+I/100)\`, impedância livre do nó \`1/U\` e capacidade de descarga \`K_A=U\`; assim:
  \`C_A(x;I)=2+(1/U)×[1+(x/U)^4]\`.
- **B — rota mais longa e descarga naturalmente favorecida, sem novo investimento:** contribuição fixa das arestas = 7; nó de descarga com impedância natural livre = 1 e capacidade de referência = 10; assim:
  \`C_B(y)=8+(y/10)^4\`.
- \`x+y=6\`, com \`x,y≥0\`, e fluxo de uma mesma unidade logística normalizada.
- As operações de descarga entram no custo **uma vez por passagem relevante**; não somar custos de nós que apenas são transitados.

**Interpretação A — impedância \`C\` como grandeza marginal de admissão da próxima fração de fluxo [PROPOSTA PREFERIDA]:** no equilíbrio de rotas utilizadas, \`C_A(x)=C_B(y)\`, com rota não utilizada tendo custo inicial ≥ custo da rota utilizada. Resultado aproximado:

| Capital incorporado na praia | \`U\` | Fluxo A \`x\` | Fluxo B \`y\` | Impedância nas rotas utilizadas |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 1 | 1,4984 | 4,5016 | 8,0411 |
| 100 | 2 | 3,6428 | 2,3572 | 8,0031 |
| 300 | 3 | 6,0000 | 0 | 7,6667 em A |
| 700 | 4 | 6,0000 | 0 | 3,5156 em A |

Se as seis unidades fossem enviadas **sem investimento somente por A**, sua impedância marginal final seria \`C_A(6;0)=1299\`, enquanto a rota B oferece alternativa de menor custo; por isso a praia absorve somente pequena fração na alocação. Com investimento suficiente, a praia recebe todo o fluxo, sem criar categoria "porto". **Estes números são de laboratório**, não parâmetros canônicos.

**Atenção — custo marginal versus custo médio [CRÍTICO]:** a tabela pressupõe que \`C_e(B)\` seja a **impedância incremental/marginal** de adicionar uma unidade infinitesimal de carga. Se, em vez disso, o sistema interpretar \`C_e(B)\` como custo médio aplicado retroativamente a **todas as unidades**, uma otimização do ônus total \`xC_A(x)+yC_B(y)\` exige igualar as derivadas \`C+xC'\`, **não** apenas \`C\`; neste exemplo o resultado muda substancialmente:

| Capital I | Fluxo A pela igualdade de C (marginal interpretado) | Fluxo A minimizando xC(x)+yC(y) (C médio) |
| ---: | ---: | ---: |
| 0 | 1,4984 | 1,0151 |
| 100 | 3,6428 | 2,4446 |
| 300 | 6,0000 | 4,0750 |
| 700 | 6,0000 | 5,8580 |

A diferença **não é arredondamento**, mas definição de qual grandeza a função representa. A **recomendação conceitual ainda não ratificada** é interpretar \`C(B)\` como **impedância marginal** de acrescer fluxo no mercado abstrato, e, quando for necessário expressar esforço físico agregado, calcular a primitiva/integral \`H(B)=∫₀^B C(z)dz\`, em vez de \`B×C(B)\`. Para rotas paralelas, minimizar \`H_A(x)+H_B(y)\` produz exatamente a condição \`C_A(x)=C_B(y)\`, com a propriedade de solução única sob funções marginais contínuas e estritamente crescentes. Não transformar \`H\` em conta monetária de transportador.

**Por que essa escolha é economicamente relevante:** um water-filling que aloca incrementos de fluxo conforme a oportunidade marginal precisa usar custos marginais e preços marginais compatíveis. Se a função de receita de mercado usa outra convenção, é necessário reconciliar as definições antes de canonizar algoritmo; não misturar a leitura marginal na escolha de rotas com cobrança média retroativa na liquidação.

**Riscos/portões para a próxima decisão:**
1. Testar integração real das curvas de preço/demanda: aqui o preço foi mantido constante, o que não valida o clearing completo.
2. A tarifa monetária de terceiros deve influenciar a escolha líquida, mas permanecer **transferência financeira contabilizada uma vez**, não integrar indevidamente \`C(B)\` como impedância física.
3. Testar vários nós de operação e arestas de uma rota para evitar que custos de descarga/transbordo sejam contados duas vezes.
4. Confirmar que a carga é uma **taxa por unidade de tempo econômico**, coerente com os períodos em F-029/F-031.
5. A soma de contribuições de várias origens e itens, em rede compartilhada, exige cálculo de congestionamento com fluxo agregado, não impedância recalculada isoladamente por vendedor.
6. O caso \`x=6\` para A em \`I=300\` é válido nesta convenção marginal e ausência de tarifa; **não** representa garantia universal de que investimentos concentrem 100% da oferta em qualquer mercado.

**Status:** experimento numérico realizado e interpretado. F-030 ratificada; funções convexas F-031, integral marginal de impedância, solução do water-filling e níveis/valores de investimento permanecem **ABERTOS**.


### F-033 — Três ambientes de aresta sem modais explícitos [RATIFICADO O PRINCÍPIO DE EMBARCAÇÕES IMPLÍCITAS; DEMAIS DETALHES ABERTOS]

**Princípio de simplificação ratificado:** o transporte marítimo não precisa de uma camada separada de embarcações, frota, habilitação naval ou veículos como entidades. **A infraestrutura da aresta de contato terra↔mar (a "infraestrutura portuária" abstrata) já representa implicitamente a disponibilidade, adequação e capacidade operacional dos meios de transporte marítimo necessários ao escoamento ou deslocamento.** O nível natural/investido da aresta determina sua eficiência e capacidade. Não adicionar uma verificação redundante de "possui navio" ou "tecnologia naval X" para permitir o uso de uma rota que a própria infraestrutura torna utilizável.

**Uma única equação de capacidade/impedância/saturação** permanece candidata para as arestas terra↔terra, terra↔mar e mar↔mar, usando diferenças **naturais** de ambiente, distância, geometria e capital investido (F-007, F-024/F-025, F-030/F-031). **Não** criar classes de estrada, porto, navio, ferrovia etc. A classificação natural terra/mar é obtida dos nós adjacentes, sem requisito de três algoritmos.

| Aresta | Sentido da infraestrutura natural e investida |
| --- | --- |
| Terra↔terra | Capacidade de circulação no terreno |
| Terra↔mar | Capacidade abstrata de **transferência entre meios**, incluindo implicitamente embarque/desembarque e adequação das embarcações, **sem objeto porto/embarcação separado** |
| Mar↔mar | Capacidade de circulação pelo ambiente marítimo; a impedância de cada trecho continua contando, mesmo quando a interface costeira é excelente |

**Contrato de rota preferencial [PROPOSTO]:** custo de escoamento = soma das impedâncias marginais das **arestas efetivamente utilizadas**, incluindo as duas arestas de interface terra↔mar em uma travessia origem-terra → mar → destino-terra. **Não somar um segundo custo de embarque/desembarque em nó para a mesma transferência**, nem cobrar custo de "porto" em cada nó de mar aberto. Preservar a opção de operações logísticas em nós **somente quando constituam outra operação física distinta**, para não contradizer F-030.

**Viabilidade em vez de exceção tecnológica:** a viabilidade econômica de uma rota marítima emerge do custo, capacidade e saturação **de todos os seus trechos**, inclusive ambas as interfaces costeiras e o próprio mar aberto. Um ótimo acesso costeiro **não torna a travessia oceânica automaticamente barata**: o restante da rota ainda participa do cálculo. Uma passagem com capacidade natural baixa pode ser possível em pequena escala mas impraticável para fluxo elevado, sem exigir um bloqueio arbitrário de tecnologia. A elegibilidade física de ações de trabalhadores depende da própria capacidade de trânsito nas arestas e do consumo de tempo universal, sem teletransporte; a forma exata de representar deslocamento sobre mar permanece em estudo.

**Unicidade de cada aresta [RATIFICADO POR F-034]:** arestas distintas terra↔mar são unidades próprias de investimento e de capacidade, mesmo quando compartilham o mesmo nó costeiro. Investir em uma **não** eleva a qualidade ou capacidade das outras. Não criar pool de capacidade/porto compartilhado no nó. Atribuição de capacidade natural a cada contato continua baseada nas propriedades do trecho individual; eventuais comparações de mapas em resoluções distintas são questão futura de calibração geométrica, não razão para agregar os investimentos de arestas diferentes.

**Casos para homologação:** 
1. Caminho terra→mar→mar→terra: contar exatamente as duas arestas de interface e as arestas marítimas percorridas, **sem** módulo de navios e **sem** tarifa/custo adicional de descarga em nós.
2. Interface costeira muito desenvolvida + mar aberto naturalmente dificultoso: a rota continua onerosa, não vira travessia gratuita.
3. Interface costeira natural de baixa capacidade: admite pequeno fluxo, mas congestionamento convexo inviabiliza economicamente excesso elevado.
4. Dois pontos de contato na mesma costa e refinamento da malha: não criar capacidade ou produtividade ex nihilo ao aumentar número de arestas.
5. Worker atravessando mar: progresso temporal localizado e determinístico; a travessia não pode acontecer por teletransporte ou capacidade do trabalhador usada duas vezes.
6. Water-filling sem mercadorias físicas: os fluxos são abstrações econômicas, não navios operacionais, mas precisam respeitar a impedância da rede.

**Ainda aberto:** função exata das capacidades naturais por ambiente; modelagem geométrica costeira; decisão sobre investimentos/titularidade de arestas em mar aberto; limite real de atravessabilidade quando \`K=0\`; saturação convexa F-031; clearing marginal F-032. **Não** concluir que uma costa desenvolvida torne todo oceano navegável com custo desprezível, ou que o modelo exija três tipos de infraestrutura.


### F-034 — Investimento logístico exclusivo por aresta e trabalho nos nós extremos [RATIFICADO]

**Decisão superveniente às hipóteses de interfaces em nós das F-030/F-033:**

1. **Toda infraestrutura logística melhorável por investimento pertence a uma aresta identificada individualmente**, nunca ao nó. Cada aresta possui seus próprios parâmetros naturais, capital incorporado, capacidade, impedância e regime de titularidade. A existência de duas arestas com um nó em comum **não** cria compartilhamento automático de investimento, capacidade, melhoria ou receita.
2. As arestas terra↔terra, terra↔mar e mar↔mar obedecem à **mesma regra estrutural** de investimento na própria aresta. A infraestrutura terra↔mar representa implicitamente a interface de embarque/desembarque e os meios aquáticos pertinentes, sem custo duplicado de porto no nó.
3. **Para executar uma ação de melhoria/construção numa aresta \`e=(u,v)\`, o trabalhador deve estar fisicamente no nó \`u\` OU no nó \`v\` no instante lógico de trabalho.** Nenhum canteiro, objeto separado ou colocação do trabalhador na posição geométrica interior da aresta é exigido.
4. O trabalhador continua sendo agente físico de nó ou **em trânsito através de uma aresta** (F-005): o deslocamento parte de um nó e chega a outro, consome tempo e pode manter progresso no interior da travessia. **Estar em trânsito não equivale a estar em um nó extremo para construir.**
5. O mesmo trabalhador não pode construir a partir dos dois extremos simultaneamente nem aplicar sua parcela temporal a outra ação no mesmo instante (F-009/F-013/F-015/F-017). A obra deve se referir à **identidade da aresta-alvo**, não apenas aos seus dois extremos para evitar confusões quando houver arestas paralelas futuras.
6. O tempo de trabalho efetivo, a validação do caixa monetário e a persistência de progresso seguem os contratos gerais F-013/F-015. **F-035 ratifica a incorporação contínua de capital, pagamento à medida do trabalho executado e benefício progressivo**, sem marco artificial de conclusão. Taxa monetária de incorporação por tempo e conflitos de múltiplos trabalhadores continuam em estudo. Não lançar capital duas vezes.
7. A escolha de qualquer extremo é simétrica para a **elegibilidade de trabalho**, salvo restrições físicas que já impeçam o trabalhador de estar naquele nó; não acrescentar privilégios de terra/mar ou tecnologia de embarcação por exceção.
8. As **capacidades produtivas naturais dos nós** (F-002/F-007) permanecem válidas. **Escopo explícito desta ratificação: infraestrutura de transporte/logística.** A existência, posição e financiamento de eventuais melhoramentos produtivos não logísticos do nó constituem outra decisão; F-006 não é abolida silenciosamente.

**Exemplo mínimo:** arestas \`AB\`, \`AC\` e \`AD\` compartilham nó \`A\`. O trabalhador presente em \`A\` pode investir/trabalhar **em uma aresta-alvo por vez**, respeitando seu orçamento de tempo. Investimento em \`AB\` muda \`AB\`, não \`AC\` ou \`AD\`. Um trabalhador presente em \`B\` também pode executar obra em \`AB\`. Se estiver em trânsito entre A e B, não pode iniciar obra de extremidade até chegar.

**Contratos de teste necessários:** investimentos em arestas adjacentes sem vazamento de estado; trabalho de ambos os extremos da mesma aresta com resultados equivalentes; trabalhador em trânsito inelegível; persistência de obra por múltiplos turnos; não duplicar custo/benefício; rota comercial passando por duas interfaces de costa; titulares distintos em arestas contíguas; nenhuma variação em capacidade de nós por investimento exclusivamente logístico.

**Observação de escala espacial:** identidade única de aresta não é, sozinha, uma garantia de equivalência entre mapas Goldberg de diferentes resoluções. Parâmetros físicos naturais podem ser ajustados à escala geométrica em futura homologação, **sem** invalidar unicidade da aresta ou compartilhar seus investimentos com vizinhas. Não criar normalização global artificial como condição para esta ratificação.


### F-035 — Incorporação contínua de investimento mediante trabalho [RATIFICADO NO NÍVEL DE PRINCÍPIO]

**Decisão:** a infraestrutura logística da aresta **evolui continuamente**, sem etapas artificiais de obra pronta/não pronta. Seu progresso utilizável é representado pelo **capital monetário efetivamente incorporado àquela aresta**. Trabalho executado e pagamento correspondente são condições simultâneas da incorporação. A cada incremento válido de capital, a mesma função de nível/eficiência/capacidade já aprovada em princípio é reavaliada com o novo capital, sem esperar a conclusão de um projeto inteiro.

**Contrato fundamental:**
1. Um trabalhador presente em qualquer nó extremo \`u\` ou \`v\` da aresta \`e=(u,v)\` pode aplicar **tempo de ação** à sua melhoria (F-034), respeitando seu orçamento universal F-013/F-015/F-017.
2. Um compromisso de investimento, meta \`I_alvo\` ou ordem de obra representa uma **autorização/teto** de incorporação, não um desembolso imediato. A meta é opcional e não cria nova infraestrutura ou estado de "obra incompleta" como requisito do motor.
3. Somente o trabalho **efetivamente realizado e financeiramente coberto** autoriza incorporar \`ΔI_e≥0\`. O saldo da conta pagadora diminui exatamente pela incorporação realizada; o capital efetivo da aresta cresce exatamente na mesma quantia. Não debitar o valor total da intenção antes da execução nem adicionar capital antes da liquidação.
4. O valor \`I_e\` já incorporado é **o próprio progresso persistido e aproveitável**. Se a ação for interrompida, faltar dinheiro ou o trabalhador se deslocar, a aresta mantém a melhoria. Outro trabalhador elegível pode continuar na mesma aresta, respeitando o mesmo contrato.
5. A melhoria fica disponível **a partir do instante lógico de incorporação**, sem aplicação retroativa a comércio, deslocamento ou eventos físicos anteriores. Um mercado avaliado por ciclos utiliza o estado válido para o instante/janela do seu cálculo, sem permitir que a ordem interna de processamento gere preços diferentes (F-018).
6. Se o saldo monetário ou o tempo se esgotar antes de atingir a meta, o progresso é **parcial** e limitado ao que foi efetivamente coberto. Não aceitar dinheiro negativo nem consumo de tempo não aplicado como obra efetivamente executada.
7. Não criar etapas discretas de "porto/estrada/ferrovia pronta" nem estados duplicados \`percentual_de_obra\`/ \`investimento_prometido\` que substituam a propriedade \`I_e\`.
8. A titularidade da aresta permanece distinta da condição física do trabalhador; após F-048, **a civilização investidora utiliza seu próprio tesouro** para financiar o trabalho, e o trabalhador não paga a obra. F-062 estabelece que o capital efetivamente incorporado por cada civilização determina sua **participação econômica na aresta e nas tarifas**, sem domínio territorial automático. Autorização política de trabalho, embargo e transferência de quotas permanecem abertos.

**F-037 (ratificado):** a taxa **de referência** do trabalhador padrão passa a ser \`R₀=1 unidade monetária por turno completo\`. A fórmula operacional abaixo continua sendo candidata, mas agora deverá respeitar essa normalização, e sua generalização futura por eficiência individual. **Não** pressupor que todas as eras/trabalhadores tenham produtividade 1 para sempre, ou que esse número canonize o preço de produtos e o valor de I₀.

**Candidato operacional mínimo [PROPOSTO quanto à forma]:**
\`ΔI_e = min(R_w × Δt_efetivo, M_disponível, max(0,I_alvo−I_e))\`.
\`R_w\` tem dimensão moeda/tempo e representa taxa de incorporação por trabalho para trabalhador \`w\`; a normalização atual é \`R_w=R₀=1 moeda/turno\`. \`M_disponível\` é saldo financeiro efetivamente utilizável, \`Δt_efetivo\` é o tempo em unidades de turno e \`I_alvo\` é meta opcional. O caso sem meta usa limites financeiro e temporal. A fonte econômica futura de diferenças entre trabalhadores é tratada em F-037, sem valores arbitrários por profissional. Atenção: se a ação ficar limitada por dinheiro/meta antes do fim do tempo inicialmente oferecido, **apenas o tempo correspondente ao valor realmente incorporado deve ser comprometido**, sob uma taxa constante; o restante pode ser reaproveitado em outra ação válida (F-017).

**Nota financeira:** a transferência "dinheiro da conta → capital incorporado" **não significa necessariamente que dinheiro deixe de existir macroeconomicamente**. Seu tratamento de contrapartidas, política monetária e titularidade requer o contrato F-022; esta decisão exige apenas evitar dupla despesa e reconhecer capital incorporado. Não lançar simultaneamente um segundo custo material abstrato para a mesma parcela.

**Exemplo de teste com a taxa-base F-037:** \`R₀=1\` moeda/turno; meta ilustrativa de capital total \`I_alvo=10\`. Trabalhando 3 turnos com saldo suficiente, incorpora 3; noutros 3, mais 3; ao parar, \`I_e=6\` já proporciona melhoria. Os 4 restantes **não foram desembolsados**. Somente a taxa-base 1 está ratificada; meta 10 é ilustrativa.

### F-036 — Concor­rência de trabalhadores e liquidação de investimento contínuo [ABERTO; PRÓXIMO GATE]

**Questão de escala:** dois ou mais trabalhadores posicionados em extremos iguais ou opostos podem agir sobre a mesma aresta ou financiar a partir da mesma conta enquanto compartilham o tempo lógico. A continuidade de F-035 **não** determina automaticamente a regra de execução concorrente, a fonte do dinheiro ou eventual saturação física do trabalho de construção.

**Proposta de simplicidade para teste:**
- As contribuições efetivamente financiadas de cada trabalhador somam no **mesmo capital \`I_e\`**; não existe uma "obra de cada trabalhador" persistida como infraestrutura separada.
- O **tempo de cada agente** é debitado uma vez; o **dinheiro de cada conta** só pode ser reservado/comprometido uma vez, mesmo quando vários trabalhadores investem simultaneamente.
- Em caso de **mesma conta insuficiente**, arbitrar saldo compartilhado de forma determinística no mesmo instante lógico, sem depender da ordem de iteração interna (F-018/F-019).
- Não criar penalização por excesso de construtores, limite de quantidade de trabalhadores ou novo índice de construção **sem demonstração de gargalo físico real**.
- No mercado econômico periódico, os benefícios da infraestrutura são lidos por um **snapshot causal** de capital incorporado, sem aplicação retroativa, não pela ordenação de tarefas do processador.

**Exemplo de aceitação com R₀=1:** aresta \`AB\`, dois trabalhadores de referência em A e B, capazes de incorporar 1 moeda/turno cada, mas caixa compartilhado de 1,5. Após uma janela comum de 1 turno com trabalho pretendido equivalente a 2 unidades monetárias, \`Σ ΔI_w ≤ 1,5\`; o débito da conta é igual à incorporação, \`I_AB\` cresce exatamente por essa soma e cada trabalhador só gasta tempo efetivamente utilizado. Repartição proporcional de 0,75 moeda e 0,75 turno por trabalhador é **candidato ilustrativo** em caso de igual prioridade/taxa, não política de arbitragem ratificada. Empate e redistribuição do saldo não podem depender da ordem da lista.

**Em aberto:** regra para repartição de saldo comum, forma de atualizar \`R_w\` quando desenvolvimento/educação mudarem, eventual gargalo construtivo físico, instante da liquidação e compatibilização com investimentos simultâneos de vários titulares. **A taxa-base universal de referência 1/turno foi ratificada em F-037**; diferenças futuras de eficiência estão previstas, mas sem fórmula. Não ratificar fórmula de concorrência a partir deste cenário isolado.


### F-037 — Taxa-base de construção e futuras diferenças de eficiência socioeconômica [RATIFICADO O PRINCÍPIO; MODIFICADORES ABERTOS]

**Valor canônico inicial:** para trabalhador padrão, **um turno completo de trabalho de melhoria de aresta incorpora até 1 unidade monetária de capital**, desde que a quantia esteja disponível e realmente seja debitada. Tempo parcial é proporcional: \`Δt=0,25 turno\` oferece capacidade de execução de até \`0,25 moeda\` por trabalhador de referência. A base não é um prêmio monetário ao trabalhador, nem emissão de dinheiro; é **taxa de execução de investimento financiado**.

\`R₀=1 moeda/turno\`, com \`R_w=R₀\` para todos os trabalhadores na primeira versão, até posterior introdução de diferenças fundadas.

**Diretriz ratificada para evolução:** futuramente, a eficiência \`E_w\` diferenciará trabalhadores a partir de **(i) grau de desenvolvimento econômico do local de pertencimento do trabalhador** e, caso venham a existir, **(ii) educação e outros fatores sociais**. O desenvolvimento econômico do local de pertencimento também deverá influenciar o **peso desse trabalhador na demanda econômica**. Não há necessidade de implementar um sistema educacional agora.

**Fórmula de extensão apenas ilustrativa [PROPOSTA]:** \`R_w=R₀×E_w\`, com \`E_w=1\` atualmente; uma demanda econômica de trabalhador poderá usar, de forma separada, uma função \`D_w = D_base × G(desenvolvimento_local, ...)\`. **Não ratificar** a forma de \`E_w\`, \`G\`, sua igualdade entre si, normalização, índices, coeficientes, limites, elasticidades ou atualizações. A existência de correlação com o desenvolvimento não obriga que produtividade e demanda cresçam na mesma proporção.

**"Local de pertencimento" não é sinônimo automático de local físico de trabalho.** O trabalhador pode se deslocar e atuar em outra região; sua referência de desenvolvimento econômico precisa ser identificada por regra de afiliação/migração própria, ainda em aberto. Não recalcular seu potencial apenas por trocar de tile, salvo decisão posterior.

**Contabilidade de tempo:** o "turno" é a unidade de referência de \`R₀\`, não uma exigência de consumir blocos indivisíveis de tempo. F-013/F-015/F-017 seguem permitindo frações temporais e atividades sequenciais. Se restarem apenas 0,2 turno de ação, a capacidade-base máxima correspondente é 0,2 moeda, condicionada a caixa e elegibilidade.

**Guardrails de implementação:**
1. Não confundir dinheiro/turno incorporável em infraestrutura com a **receita** de produção de mercadorias: esta surge posteriormente de quantidade, demanda, escoamento e preço de mercado (F-023). Nenhum trabalhador cria 1 moeda por turno só por construir.
2. O parâmetro de escala da função de nível \`I₀\` não foi fixado por esta deliberação. A escolha \`R₀=1\` não implica um número canônico de turnos para atingir qualquer nível de infraestrutura.
3. A exigência de caixa e não duplicação de tempo/dinheiro/capital de F-035 permanece obrigatória.
4. Quando futuramente o desenvolvimento local afetar **eficiência construtiva e peso da demanda**, testar retroalimentação, estabilidade de poder de compra e não criação de saldos sem contrapartida (F-022). Não derivar o desenvolvimento de uma conta que dependa circularmente dele sem regra causal.
5. Se \`E_w\` variar durante o turno/ciclo, definir o instante de referência e garantir determinismo de F-018; não ler taxas inconsistentes em diferentes ordens de processamento.
6. Uma unidade monetária é **unidade de conta nominal do modelo**; calibração de preços e valores monetários entre eras é assunto posterior. Evitar índices artificiais de eficiência por item, modal ou infraestrutura.

**Exemplo inicial de homologação:** um trabalhador com 1 turno e 2 moedas disponíveis aplica 1 turno → debita e incorpora 1; meio turno → incorpora 0,5; com 0,3 moeda disponível e 1 turno, incorpora no máximo 0,3 e libera o tempo restante, se nenhuma outra restrição atuar. Produção econômica e peso de demanda permanecem processos separados.

**Status:** norma inicial \`R₀=1\` e fontes futuras de diferenciação ratificadas; fórmulas de eficiência/demanda, índice local, educação, migração e tempo de atualização **não ratificados**. A arbitragem entre construtores permanece F-036 [ABERTO].


### F-038 — Eficiência derivada de consumo real por trabalhador e diversidade endógena [RATIFICADO NO PRINCÍPIO; EQUAÇÃO ABERTA]

**Questão:** como substituir o "grau de desenvolvimento econômico do local de pertencimento" de F-037 por uma medida endógena de desenvolvimento material que não recompense simplesmente maior receita, acúmulo de dinheiro ou aumento declarado de demanda?

**Proposta preferida:** o desenvolvimento material da localidade \`n\` deve refletir **quantidades de mercadorias efetivamente adquiridas/atendidas pela economia local** por unidade de trabalhador pertencente a ela, com **utilidade marginal decrescente por produto**, de modo que a variedade contribua **sem prêmio adicional e arbitrário por tipo de mercadoria**.

**Separações fundamentais:**
1. \`demanda de referência\` ≠ \`venda efetiva\` ≠ \`consumo atribuído\`. Só quantidades realmente absorvidas pelo mercado na localidade (F-041/F-043/F-046), **sem carteira individual de comprador**, podem influenciar o indicador. **Aumento de demanda não equivale, por si só, a consumo atendido.**
2. A variável é **per trabalhador/unidade de pertencimento**, não consumo agregado bruto da localidade; não premiar um território simplesmente por conter mais trabalhadores. A métrica de população quando o trabalhador for agregado está aberta.
3. O benefício de variedade deve emergir **de retornos decrescentes do consumo repetido**, e não de índices especiais de segurança, raridade, preferência ou valor monetário arbitrariamente atribuído a cada mercadoria.
4. Consumo material é econômico/contábil, **sem estoque físico persistente**: em cada ciclo, atribuir quantitativamente o fluxo de mercadorias efetivamente vendido a demandas atendidas em destinos, sem duplicar o mesmo fluxo de comércio como consumo de mais de uma localidade.
5. O **preço de mercado** determina quanto deve ser pago, mas não é medida direta de consumo real. Uma alta nominal de preços sem aumento das quantidades atendidas não pode elevar automaticamente a eficiência.
6. A afiliação de pertencimento de cada trabalhador a uma localidade continua uma regra separada (F-037); migrar ou transitar não deve retroativamente reatribuir consumo de ciclos anteriores.

**Candidato matemático [ILUSTRATIVO, NÃO RATIFICADO]:**
\`S_n(t) = Σ_{i∈I(t)} ln(1 + c_{n,i}(t))\`,
onde \`c_{n,i}(t)\` é **quantidade efetivamente atendida por trabalhador da localidade**, normalizada em unidade de consumo economicamente comparável para mercadoria \`i\`. \`S_n\` é adimensional. A concavidade do log favorece, para uma quantidade total normalizada fixa, repartição entre mercadorias distintas em vez de concentração extrema num único produto; não exige bônus explícito por variedade.

**Caveat estrutural não resolvido:** normalizar mercadorias diversas é necessário. Uma unidade de pedra e uma de diamante não têm automaticamente o mesmo benefício de bem-estar; não inventar pesos por item para forçar resultados. Adicionalmente, simplesmente **dividir um item em duas categorias artificiais pode elevar \`S_n\`**, portanto a ontologia de itens e sua estabilidade importam. Categorias precisam ter significado real e não podem ser multiplicadas só para explorar o índice. Essa objeção pode justificar fórmula alternativa sem catálogo arbitrário de itens.

**Aplicação temporal proposta:** eficiência construtiva no período/ciclo seguinte depende do consumo **realizado e liquidado no ciclo anterior**, \`R_w(t+1)=R₀×E(S_{origem(w)}(t))\`, com \`E\` inicialmente igual a 1 para todos até ratificação de fórmula. Uma demanda futura \`D_w(t+1)=G(S_{origem(w)}(t),...)\` pode usar a mesma referência \`S\` mas **não precisa** ter a mesma forma, magnitude ou elasticidade da eficiência. Usar resultado passado (ou média histórica, se comprovadamente necessária) evita causalidade instantânea circular, mas **não garante estabilidade econômica por si só**.

**Critérios de integridade antes de ratificar:**
- A demanda ampliada por \`G\` não é orçamento automático; o clearing e a liquidação precisam de fontes de pagamento verificáveis (F-022). Não aceitar ciclo "declaro demanda maior → aumenta produtividade/caixa → compro mais" sem produção real e financiamento.
- \`E(S)\` deve ter retornos decrescentes/limitações economicamente justificáveis; medir crescimento de longo prazo e estabilidade, sem declarar teto arbitrário por prudência. Decisões sobre função, limites e escalas em aberto.
- Maior consumo financiado por transferências legítimas pode elevar bem-estar e eficiência mesmo sem maior produção local; isso pode ser desejável para localidades comerciais. Não confundir localização do produtor com localidade da demanda atendida.
- Educação e sistemas sociais futuros devem entrar como modificadores adicionais só se houver ganho de gameplay demonstrado; não duplicar o mesmo consumo sob nomes diferentes.
- Uma mesma mercadoria não pode contar duas vezes como consumo ao percorrer vários trechos de uma rota abstrata water-filling.
- A métrica de referência \`R₀=1 moeda/turno\` (F-037) **não muda** enquanto esta proposta estiver aberta.

**Alternativas de comparação:** receita local per trabalhador (ignora consumo); riqueza/caixa (pode não atender necessidades); gasto nominal (sensível a inflação/preço); satisfação efetiva relativa a necessidades (mais direta, mas exige regra não arbitrária de necessidade por produto). Nenhuma foi aprovada como indicador canônico.

**Status [RATIFICADO NO PRINCÍPIO]:** a medida de desenvolvimento material deverá, preferencialmente, emergir do **consumo efetivamente atendido por trabalhador na localidade de pertencimento**, considerando quantidade e diversidade sem bônus arbitrários por item. A fórmula de utilidade `Σ ln(1+c)`, normalização, contribuição de mercadorias materialmente diferentes, função de produtividade/demanda e atualização temporal **permanecem PROPOSTAS/ABERTAS**, não decisões canônicas. F-039 remove expressamente o antigo mecanismo de demanda artificial de retenção. F-040 propõe contrato de atribuição por ciclo e controles contra dupla contagem.


### F-039 — Sem estoques e sem demanda artificial de retenção [RATIFICADO; CORRIGIDO POR F-041]

**Decisão:** não há estoque persistente de mercadorias, reserva material, compra fictícia para retenção nem mecanismo artificial de elevar demanda para estocar no produtor. **F-041 explicita que toda a quantidade produzida deve ser vendida**: a curva de preço absorve excesso de oferta, sem descarte de produtos.

A antiga interpretação documentada de que uma produção de 10 poderia vender só 7 e deixar 3 sem venda **foi expressamente revogada pelo usuário**. O correto é \`Q_vendida=Q_produzida\`, distribuída entre mercados válidos, sem inventário entre ciclos.

### F-040 — Atribuição do consumo após venda universal [CONTRATO PROPOSTO; REVISADO POR F-041]

**Pipeline candidato:**
1. Apurar quantidades \`Q_{o,i}\` efetivamente produzidas por origem \`o\`, mercadoria \`i\`, no ciclo.
2. Definir demanda **de referência** por mercado \`n\` e item \`i\`: \`D_{n,i}=Σ_{w∈n} k_w\`, com \`k_w=1\` inicialmente, salvo regra futura de qualificação. Uma mercadoria distinta usa a mesma base 1 por trabalhador; nenhuma escala específica é arbitrariamente atribuída por item.
3. Alocar **a totalidade** da oferta via water-filling entre destinos com demanda de referência positiva e rotas válidas. Para cada mercado: \`Q_{n,i}=Σ_o q_{o→n,i}\`, e \`x_{n,i}=Q_{n,i}/D_{n,i}\`. O preço depende da oferta **agregada de todos os produtores** daquele item no mesmo mercado, não apenas de uma origem isolada.
4. Exigir \`Σ_n q_{o→n,i}=Q_{o,i}\` para cada origem e item. \`D\` não é limite superior de absorção; \`Q>D\` é sobreoferta com queda de preço. Não carregar saldo excedente.
5. Calcular receita pela quantidade integral vendida no preço do mercado: \`Receita_{o,i}=Σ_n q_{o→n,i} P_{n,i}\`. O saldo é creditado **à civilização responsável por cada origem/atividade produtiva (F-048)**, não ao trabalhador nem ao nó. F-046 mantém compradores sem carteira; tarifas e impostos, caso existam, possuem lançamentos separados e regras próprias.
6. Atribuir ao mercado consumidor quantidades efetivamente compradas uma só vez: \`A_{n,i}=Q_{n,i}\`; não há carteira compradora a reconciliar (F-046), e a mesma mercadoria não pode ser duplicada ao atravessar arestas. O indicador material proposto F-038 usa \`c_{n,i}=A_{n,i}/N_n\` para \`N_n>0\` e observa período/ciclo anterior, sem estoque físico.
7. O clearing deve ser determinístico quando o preço depende endogenamente do \`Q_{n,i}\` que o water-filling está alocando.

**Teste mínimo:** produzir 10 unidades, ter dois mercados com demandas de referência 3 e 2. Se as rotas forem válidas, o motor atribui **todas as 10** às localidades, em alguma divisão não negativa somando 10. As unidades acima de \`D\` recebem preços menores, jamais deixam de ser vendidas. Dois produtores abastecendo o mesmo mercado compartilham a mesma base de preço calculada com \`Q\` agregado.

**Ressalvas:** nenhum mercado economicamente acessível com \`D>0\` torna impossível calcular \`x=Q/D\` e precisa de regra de borda explícita. A capacidade de compra é **indeterminada** e não limitada por saldo simulado, por F-046. Não criar carteiras compradores, empréstimos, contabilidade de consumo ou restrições financeiras artificiais para essa compra.

### F-041 — Venda universal, demanda de referência unitária e preço por mercado [RATIFICADO]

**Requisitos canônicos enunciados pelo usuário:**
- **Toda a produção de cada mercadoria é escoada e comercializada**, sem saldo não vendido e sem formação de estoque. \`Σ_n Q_{n,i}=Σ_o Q_{o,i}\`.
- **Demanda-base = 1 por trabalhador e por mercadoria** em cada ciclo. O trabalhador possui futuro modificador de qualificação \`k_w\` relacionado aos parâmetros socioeconômicos da F-037/F-038; inicialmente \`k_w=1\`. \`D_{n,i}=Σ_{w∈n} k_w\`. As regras de afiliação e atualização de \`k\` estão abertas.
- \`x=Q_{n,i}/D_{n,i}\` mede oferta relativa à **demanda de referência**. D não impõe teto de compras; excesso de oferta reduz preço. Preços são determinados **para cada item em cada mercado**, segundo \`Q\` agregado após resolver a alocação water-filling, que pode exigir solução simultânea de preço e fluxos.
- Receita bruta é **preço final × quantidade integralmente vendida** em cada mercado. Sem alteração fictícia de demanda para criar estoques.

**Curva histórica informada pelo usuário, não ratificada:** \`P(x)=x^{-1/2}\` para \`0<x<1\`, \`P(1)=1\`, \`P(x)=x^{-2}\` para \`x>1\`. Seu objetivo: receita não decrescente ao aumentar a oferta, mas com ganho marginal decrescente, e penalidade por escassez relativa. **Inconsistência descoberta:** quando \`Q>D\`, \`R=Q(D/Q)^2=D²/Q\`, portanto a receita **cai** com o crescimento de Q. O ramo x^-2 deve ser substituído por outra lei antes de adoção.

**Bordas não fechadas:** nenhuma localidade acessível com \`D>0\`; a natureza de absorção econômica abstrata versus consumo material produtivo. **F-046 exclui a hipótese de saldos financeiros limitados de compradores**. Nenhum desses pontos permite reabrir venda parcial.

### F-042 — Estudo histórico da função racional [SUBSTITUÍDA COMO CANDIDATA PRINCIPAL POR F-043; NÃO RATIFICADA]

**Alternativa histórica (não selecionada): função racional única, sem parâmetros livres adicionais:**
\`P(x)=2/(1+x)\`, para \`x≥0\` e \`D>0\`.
\`R(Q,D)=Q P(Q/D)=2DQ/(D+Q)\`.

**Verificações:**
- \`P(1)=1\`; \`R(D,D)=D\`. O ponto Q=D é normalização de preço, **não** máximo de receita.
- \`∂R/∂Q=2D²/(D+Q)²>0\` e \`∂²R/∂Q²=-4D²/(D+Q)^3<0\`: receita sempre aumenta e tem ganho marginal estritamente decrescente com demanda fixa.
- \`P\` cai suavemente de \`P(0)=2\` até zero assintótico, e \`R\` se aproxima de \`2D\` quando a oferta se torna muito alta. A saturação de receita é **característica desta proposta**, não um teto canônico.
- Com \`D=1\`: \`Q=0,25 → P=1,6, R=0,4\`; \`Q=0,5 → P=1,3333, R=0,6667\`; \`Q=1 → P=1, R=1\`; \`Q=2 → P=0,6667, R=1,3333\`; \`Q=4 → P=0,4, R=1,6\`; \`Q=10 → P=0,1818, R=1,8182\`.

**Alternativa se for prioritário preservar a escassez da curva histórica:**
\`P(x)=x^{-1/2}\` para \`0<x≤1\`; \`P(x)=2/(1+x)\` para \`x≥1\`.
Ambos os ramos têm preço e primeira derivada coincidentes no ponto de encontro \`x=1\`, receita total crescente e ganhos marginais decrescentes. Porém usa dois ramos e preço ilimitado para \`Q→0\`, enquanto a racional única não tem essa singularidade. Nenhuma foi aprovada.

**Implicação do water-filling:** maximizar \`Σ_n R(Q_n,D_n)\` sujeito a \`Σ Q_n=Q_prod\` usa **receita marginal** \`∂R/∂Q\` na escolha dos destinos; não confundir preço médio de liquidação \`P=R/Q\` com receita marginal. Se o water-filling adotará a ótica de agentes formadores de preço ou de preços tomados, e como combina impedância física (não monetária) com receita e tarifas (monetárias), está em aberto.

**Financeiro [SUPERADO POR F-046]:** os compradores não têm contas representadas nem limites de caixa. A receita de venda é gerada pelo mecanismo de mercado e creditada ao titular produtor; não se simula débito de consumidor nem se cria comprador institucional fictício. Contas reais dos titulares ainda registram investimentos, tarifas e transferências.


### F-043 — Curva única de preço por oferta/demanda [RATIFICADO]

**Decisão aprovada:** para toda mercadoria \`i\` e mercado de destino \`n\`, usar a **mesma equação única de preço** \`P(x)=x^{-1/2}\` em ambos os regimes de escassez (\`x<1\`) e de excesso de oferta (\`x>1\`). \`x=Q_{n,i}/D_{n,i}\`, \`Q_{n,i}>0\`, \`D_{n,i}>0\`.

**Equações canônicas:**
- \`P_{n,i}=√(D_{n,i}/Q_{n,i})\`, preço unitário de **liquidação** naquele mercado;
- \`R_{n,i}=Q_{n,i} P_{n,i}=√(Q_{n,i} D_{n,i})\`, receita bruta de toda a quantidade daquele item vendida naquele mercado, antes de tarifas monetárias adicionais;
- \`R'_{n,i}(Q)=0,5√(D_{n,i}/Q)\`, receita **marginal** de acrescentar pequena quantidade ao mercado, a demanda fixa;
- \`R''_{n,i}(Q)=-√D_{n,i}/(4Q^{3/2})<0\`, retornos marginais decrescentes.

**Invariantes:**
1. \`P(1)=1\`, \`R(D,D)=D\`; \`Q=D\` é referência de preço, **não máximo da receita**.
2. \`R'>0\` em \`Q>0, D>0\` e \`R''<0\`. Toda oferta vendida acrescenta receita positiva, mas menor nas unidades seguintes.
3. Não existe **teto fixo de receita total** imposto pela curva. Para \`Q→∞\` a demanda fixa, \`R→∞\` lentamente como \`√Q\`, enquanto \`P→0\`.
4. \`R'=P/2\` exatamente. Não confundir **preço médio final por unidade** com **receita marginal** de alterar a oferta agregada e, portanto, o preço de TODAS as unidades liquidadas no mercado.
5. O preço depende de \`Q\` agregado **de todas as origens** que vendem o mesmo produto no mesmo mercado, não é recalculado isoladamente para cada produtor.
6. Para \`Q=0\`, registrar \`R=0\` e **não cotar preço de transação**: \`P\` diverge na expressão original. Para \`D=0\`, preço não tem definição econômica; **não** criar mercado fictício sem regra específica (questão de borda de F-041/F-022).
7. Demanda inicial \`D_{n,i}=Σ_{w∈n}1\`, corrigível futuramente por \`k_w\`; \`D\` é referência, não teto, e **toda a produção é vendida** (F-041), com nenhuma quantidade em estoque (F-039).
8. Esta equação é universal entre produtos; nenhum expoente ou preço-base arbitrário por mercadoria.

**Preço e renda no mesmo destino:** se o mercado n receber \`q_1\` e \`q_2\` de duas atividades produtivas para o mesmo produto i, \`Q=q_1+q_2\`; a **civilização responsável por cada atividade** recebe \`q_j P\` por sua parte, com o mesmo preço unitário de mercado. Custos e tarifas de rota podem afetar a **receita líquida de cada civilização** de modo diferente. Por F-046, a venda gera receita efetiva ao titular representado, com contraparte no setor consumidor implícito, **sem saldo financeiro individual de comprador**.

**F-042 permanece como registro de alternativas matemáticas superadas:** a racional \`2/(1+x)\` é válida, mas foi preterida por \`x^{-1/2}\` por simplicidade e ausência de limite assintótico da receita.

### F-044 — Water-filling sem custos espaciais: distribuição proporcional à demanda [RESULTADO MATEMÁTICO VERIFICADO; ESCOPO RESTRITO]

**Problema de teste:** um único produto, oferta total \`Q_T>0\`, mercados consumidores \`n=1...m\` com demandas positivas \`D_n>0\`, todas as rotas com **condições de acesso idênticas e sem tarifas**, e mesmo tratamento financeiro. Alocar integralmente \`Q_T\` maximizando a **soma das receitas brutas** \`Σ_n √(D_n Q_n)\`, sujeito a \`Σ_n Q_n=Q_T\`, \`Q_n≥0\`.

A condição de primeira ordem é igualdade de receita marginal:
\`R'_n=0,5√(D_n/Q_n)=λ\`. Portanto \`Q_n/D_n\` é constante e a solução é:
\`Q_n = Q_T × D_n/(Σ_j D_j)\`.

**Consequências exatas:**
- Com \`D_T=Σ_j D_j\`, todos os mercados ativos terminam com \`Q_n/D_n=Q_T/D_T\`, logo **preço unitário igual** \`P_n=√(D_T/Q_T)\`.
- Receita total \`R_T=√(Q_T D_T)\` em ausência de diferenças espaciais; nenhuma oferta é retida.
- Exemplo: \`D_A=4\`, \`D_B=1\`, \`Q_T=10\` ⇒ \`Q_A=8\`, \`Q_B=2\`, \`P_A=P_B=1/√2≈0,70710678\`, \`R_A≈5,65685\`, \`R_B≈1,41421\`, \`R_T≈7,07107\`.

**Nota de governança:** a identidade matemática fica demonstrada **sob o objetivo declarado de maximizar receitas agregadas e sem impedâncias assimétricas**. Não canoniza automaticamente o mesmo critério de maximização para produtores independentes, tarifas de terceiros, mercado com oferta por muitas origens ou water-filling em rede. A igualdade \`R'=P/2\` implica que **nesse caso simples** igualar preços ou receitas marginais gera a mesma alocação, mas isso deixa de resolver isoladamente o problema quando outros custos e incentivos são introduzidos.

### F-045 — Estudo de fechamento monetário entre compradores [SUPERADO PELA F-046]

O raciocínio anterior exigia conciliar a receita de vendas de F-043 com carteiras finitas dos consumidores, criando falsa incompatibilidade com venda integral F-041. **Essa exigência era importada da versão antiga e foi rejeitada expressamente.**

**Decisão superveniente:** os compradores **não têm carteira no jogo**, logo a comparação "compras de 7,071 com saldo comprador de 5" **não se aplica**. Não criar comprador institucional, orçamento de consumo, banco, crédito, compensação entre carteiras invisíveis ou dinheiro de comprador simulado apenas para reconciliar contas que deliberadamente não são modeladas.

**Permanece legítima** a necessidade de registrar recebimentos e pagamentos **de agentes efetivamente representados** (tesouros de produtores/instituições, investimentos, tarifas, impostos futuros). A contabilização das receitas oriundas de compras de consumo pertence ao **setor externo/implícito**, que é uma fronteira de modelagem, não um banco ou carteira oculta.

O outro problema identificado na versão anterior — a dependência dos fluxos na impedância e vice-versa — continua sendo relevante e foi reaberto como F-047.

### F-046 — Economia consumidora aberta, sem carteiras de compradores [RATIFICADO]

**Princípio canônico:**
1. **Nenhuma carteira, orçamento de compras, saldo comprador, limite de pagamento ou verificação de caixa** para os trabalhadores/localidades na função de consumidores. Os compradores **compram e pronto**, em quantidades definidas pelo mercado e pela referência de demanda \`D=Σ k_w\`, não por caixa disponível.
2. Os produtos nomeados no jogo são um subconjunto de uma **economia abstrata mais ampla**, com outros bens e serviços **invisíveis**. A despesa com as mercadorias modeladas pode crescer ou diminuir à custa de gastos invisíveis, cujo volume/valor não será simulado. **Poder de compra consumidor é indeterminado.**
3. **Toda a produção vendida** no preço canônico \`P=√(D/Q)\` gera receita nominal efetiva creditada ao **tesouro da civilização responsável pela atividade produtiva**, sem débito em conta de comprador representado (F-048).
4. Uma economia financeira **aberta e parcial** não precisa preservar quantidade de moeda no submodelo visível. A entrada de dinheiro por vendas é **receita de transação com setor consumidor implícito**, não dinheiro material emitido diretamente por trabalhadores nem um débito em "tesouro consumidor" inventado.
5. Os saldos financeiros **que o jogo representa** continuam reais: compras de infraestrutura F-035 exigem caixa, tarifas alheias e impostos futuros podem transferir recursos e o dinheiro de um tesouro não pode ser gasto duas vezes. Somente interações entre contas explicitamente simuladas devem ter débitos/créditos espelhados.
6. Não reaproveitar exigências, controles ou invariantes legados de "financiar a demanda com a carteira do consumidor", "pagar a venda com orçamento predeterminado" ou "aumentar demanda para guardar mercadorias". A F-022 é reinterpretada restritivamente.
7. Para consumo material F-038/F-040, o atendimento \`A_{n,i}=Q_{n,i}\` consiste na **quantidade comercialmente absorvida** naquele mercado, sem comprovante de saldo consumidor, estoque ou entrega individual.
8. A possibilidade futura de mercados diferentes terem padrões de consumo e produtividade não implica que o trabalhador receba dinheiro para gastar nem que sua demanda seja uma carteira.

**Testes de regressão conceitual:**
- Oferta Q=10 e demanda D=5 ⇒ P=√(5/10), receita ≈7,071, creditada à civilização produtora sem necessidade de demonstrar saldo comprador ≥7,071.
- Um trabalhador comprar economicamente mais que sua unidade-base de demanda não cria estoque, limite financeiro ou dívida.
- A entrada de receita em um tesouro não permite que ele **gaste duas vezes** o saldo em investimento ou tarifa.
- Duas contas reais trocando uma tarifa mantêm transferências financeiras reconciliadas; o setor consumidor externo **não** é uma conta real adicional.

**Questões ainda abertas:** critérios de vinculação de operações produtivas a civilizações quando há ocupações compartilhadas, propriedade e aplicação de tributos, tarifas, escala nominal da moeda, como o consumo por mercadoria contribui para desenvolvimento F-038. **A receita não pertence ao trabalhador nem ao nó: F-048 resolveu isso.** **Não reabrir carteiras compradoras para resolver essas pendências.**

### F-047 — Expectativa fixa de carga por ciclo versus equilíbrio endógeno [HIPÓTESE EM ANÁLISE; NÃO RATIFICADO]

**Problema real:** em F-031, a impedância de uma aresta depende de \`B_e\`, mas o \`B_e\` agregado depende da alocação do water-filling, que por sua vez utiliza a impedância. \`B → C(B) → distribuição → B\` é **acoplamento simultâneo**, não impossibilidade lógica. Pode-se encontrar equilíbrio endógeno resolvendo a rede, mas em mapas grandes pode ser custoso; as escolhas de normalização e objetivo matemático continuam em aberto.

**Método de expectativa trazido pelo usuário, como proposta de implementação para avaliar:** estimar a **taxa de carga equivalente** \`B̂_{e,t}\` **antes** do ciclo \`t\`, usando informação de ciclos anteriores, e fixar o custo de impedância unitário **para todo o ciclo**:
\`C_{e,t}^{fixo} = C_{livre,e}(I_{e,t}^{snapshot}) × [1+(B̂_{e,t}/K_e(I_{e,t}^{snapshot}))^p]\`.

Depois realizar water-filling de toda oferta com essas impedâncias congeladas, sem recalcular o custo unitário de cada aresta quando cada parcela do fluxo é alocada; obter a carga **efetiva** \`B_{e,t}\` resultante; atualizar a expectativa para o **próximo** ciclo, por regra ainda não ratificada. Exemplo mínimo candidato: \`B̂_{e,t+1}=B_{e,t}\`. Para evitar distorção se a duração dos ciclos econômicos variar, **todas essas quantidades são taxas por unidade de tempo de referência**, nunca totais brutos sem normalização.

**História operacional:** um porto implicitamente representado pela aresta terra↔mar pode ter volume esperado quase constante. O custo unitário de utilização durante um ciclo deriva dessa expectativa (ex.: tráfego normalmente com cargueiros cheios), e uma mudança real de carga afeta o custo **no ciclo posterior**. Isto **não** exige modelar cargueiros/contêineres individualmente e é coerente com F-033/F-034.

**Vantagens:** preço/impedância de uso da infraestrutura **único por aresta e ciclo**; nenhuma dependência operacional imediata de \`B_e\` no próprio market clearing; resultado independe da ordem de iteração das transações; redução de custo de solução. **Não elimina** a dependência do preço de mercado \`P_{n,i}=√(D/Q)\` em relação ao \`Q\` agregado que o water-filling está determinando, mas esta dependência é monotônica/concava para D>0 e pode ser resolvida como problema de alocação.

**Riscos que impedem ratificação imediata:**
1. **Oscilação entre rotas:** se duas arestas paralelas têm custo natural igual, p alto e total Q=2 com capacidades K=1, uma rota que levou todo Q no ciclo anterior teria \`C=17 C_livre\`, a outra vazia \`C=C_livre\`; no próximo ciclo, todo fluxo pode mudar de lado e repetir inversamente indefinidamente, se não houver divisão de caminhos equivalentes ou regra estável. O próprio congelamento não garante convergência temporal.
2. **Primeiro ciclo:** como inicializar expectativas de carga de arestas nunca utilizadas, principalmente mar aberto, sem supor fluxo positivo exógeno injustificado? Inicializar pela carga anterior zero é simples, mas pode produzir choque de congestionamento no ciclo seguinte.
3. **Infraestrutura melhorada durante um ciclo:** F-035 garante benefício contínuo desde a incorporação de capital no instante lógico de construção. Se o mercado roda em ciclos discretos, usar estado logístico do início do ciclo para **aquele clearing** e novas melhorias no próximo clearing; isso não significa negar efeitos físicos imediatos no movimento dos trabalhadores dentro do turno.
4. **Tarifas monetárias** por trecho podem permanecer vinculadas ao nível da infraestrutura e à utilização **efetivamente vendida** do ciclo, sem congelar o montante total devido ao titular. Não confundir preço de impedância não monetário com tarifa financeira.
5. **Water-filling ainda precisa resolver preços endógenos da oferta agregada**, mesmo se as impedâncias são fixas por ciclo; não presumir que um único passe guloso por produtor seja o ótimo.
6. **Sem estoque e com venda integral:** congestionamento alto não permite descartar produção; a alocação precisa entregar todos os fluxos mesmo se só existem rotas muito caras. Rotas fisicamente intransitáveis versus inviabilidade de custo são casos distintos.

**Experimento comparativo recomendado antes de escolher método:**
- Rede com uma rota curta costeira limitada e rota alternativa longa, oferta fixa e demanda com curva F-043;
- congestionamento crescente (F-031) com custos endógenos versus custos congelados por expectativa \`B̂\`;
- choque súbito de produção, abertura/melhoria de infraestrutura e alternância de destinos;
- condições de simetria de rotas e surgimento de ciclos de oscilação (em particular com p=4);
- medir estabilidade, preços únicos por mercado/ciclo, qualidade da alocação e custo computacional em mapas Goldberg grandes.
**Governança [DECISÃO SUPERVENIENTE F-049]:** o usuário escolheu expressamente **equilíbrio simultâneo para a implementação inicial**, revogando a consideração da expectativa congelada como estratégia principal. A expectativa por ciclo permanece **fallback documentado**, acionável apenas após evidências de custo/desempenho que justifiquem nova decisão. Não introduzir coeficiente de suavização arbitrário nem contratos marítimos. F-049 ratifica o *método simultâneo*, não o expoente p nem uma função específica de otimização.


### F-048 — Tesouro e receita da civilização, nunca do trabalhador [RATIFICADO]

**Princípio explícito:** **quem recebe o dinheiro pela venda de produção é a CIVILIZAÇÃO responsável pela produção**, não o trabalhador nem o nó onde se produziu. O trabalhador continua sendo o agente físico que aplica tempo e produz quantidade; o nó continua fornecendo território/recursos naturais/mercado, sem possuir necessariamente saldo em moeda.

**Contrato:**
1. Uma atividade produtiva gera quantidade física/econômica de mercadoria \`Q_{o,i,c}\` vinculada à civilização \`c\`, cuja atribuição de comando/pertencimento exige um identificador explícito, inclusive quando trabalhadores de várias civilizações estiverem no mesmo nó.
2. O clearing de mercado F-041/F-043 atribui venda total e preço por item/destino; para a parcela oriunda da civilização \`c\`: \`ReceitaBruta_c=Σ_{o,n,i}q_{o→n,i,c}×P_{n,i}\`. **A titularidade dessa receita produtiva pertence à civilização \`c\`, jamais ao trabalhador/nó; após F-060, o caixa creditado a ela é a parcela LÍQUIDA de eventuais tarifas proporcionais de arestas alheias**, enquanto as demais parcelas são creditadas diretamente aos respectivos tesouros proprietários. Sem tarifas, líquido=bruto. Não creditar o bruto à civilização e depois novamente o rateio, nem cobrar duas vezes.
3. **O trabalhador não possui carteira monetária**, não recebe salário em moeda automaticamente pela produção e não pode ser o destinatário de receita por vender. Seu peso de demanda \`k_w\` e sua eficiência F-037/F-038 não são dinheiro individual.
4. **A localidade/nó não possui tesouro próprio por padrão.** Um nó pode produzir, abrigar trabalhadores, demandar e ter estrutura física sem possuir caixa ou apropriar-se da receita; seu eventual vínculo administrativo com uma civilização não substitui o identificador econômico da atividade.
5. Obras em arestas F-034/F-035/F-037 são executadas fisicamente pelos trabalhadores autorizados, mas o **pagamento é realizado pelo tesouro da civilização investidora**: debitar \`M_c\` e incorporar capital na aresta na mesma operação; não debitar do trabalhador nem de um "tesouro do nó".
6. Eventuais **tarifas monetárias de terceiros** deverão usar contas das civilizações pagadoras/recebedoras ou outra entidade explicitamente autorizada por decisão futura. Não presumir que o trabalhador pague tarifa nem que a propriedade da aresta coincida com o território de origem. **Por F-059, quando a tarifa existir, ela influencia o clearing antes de qualquer desempate técnico; F-060 RATIFICOU sua natureza proporcional à receita comercial e o rateio único, sem desembolso antecipado.**
7. A venda de mercadorias para o setor consumidor **implícito e sem carteira** (F-046) é entrada aberta de moeda no subconjunto modelado. **Transações financeiras entre civilizações representadas** precisam de contrapartidas coerentes; circulação monetária fora desse subconjunto não será simulada.
8. Não criar tributação automática sobre receita já recebida integralmente pelo tesouro da própria civilização: isso seria transferir dinheiro da civilização para ela mesma. Eventuais impostos entre agentes distintos, setor externo ou outras instituições precisam de definição posterior.
9. Saldo da civilização pode financiar atos enquanto houver dinheiro; dinheiro de compras de consumidores não é limitado por um saldo modelado. **Não confundir** abertura do lado comprador com caixa ilimitado do tesouro da civilização.

**Casos de teste:** dois trabalhadores da mesma civilização produzem em nós diferentes e suas receitas somam no **mesmo tesouro**; trabalhadores de civilizações distintas no mesmo nó mantêm créditos separados; produção zero não cria receita; construir duas arestas com mesmo tesouro não gasta o saldo duas vezes; um trabalhador de outra civilização não redireciona automaticamente receita por estar fisicamente no mesmo local.

**Pendências:** quem autoriza mão de obra de uma civilização a trabalhar para outra, quem recebe a receita dessa ação contratada, como serão deliberados embargos em arestas compartilhadas F-063, tributação externa/institucional e relação entre afiliação de desenvolvimento F-037 e titularidade de produção. **A regra de destinatário final da moeda é canônica: civilização.**

### F-049 — Equilíbrio simultâneo para congestionamento e water-filling [RATIFICADO NO MÉTODO; EQUAÇÕES E SOLVER ABERTOS]

**Escolha explícita:** o primeiro algoritmo de mercado deverá **resolver o equilíbrio simultâneo** entre volumes logísticos e impedâncias, **sem congelar impedância pelo volume esperado do ciclo anterior**. Se os testes de escalabilidade demonstrarem custo computacional excessivo, a estratégia poderá ser revista mediante nova deliberação.

**Dependência resolvida conjuntamente dentro do ciclo econômico \`t\`:**
- Arestas e investimentos são fixados no **estado físico/logístico pertinente à janela de clearing**; \`I_e(t)\` fornece \`K_e(I_e)\` e impedância livre.
- Os fluxos de **todas as mercadorias e civilizações** que atravessam a aresta determinam sua carga equivalente conjunta \`B_e=Σ_iΣ_{rotas\ que\ usam\ e} a_i q_{rota,i}\` (normalizada pela mesma unidade de tempo de referência da capacidade), mantendo F-026 sem índices arbitrários de produto.
- O congestionamento \`C_e(B_e,I_e)\` depende da carga **efetiva simultânea**; \`C_e\` influencia as escolhas de rota e as próprias cargas. O equilíbrio satisfaz o sistema de relações de forma conjunta.
- O preço \`P_{n,i}=√(D_{n,i}/Q_{n,i})\` depende da quantidade **totalmente atribuída** ao destino para aquele item, que também deve ser decidida no mesmo clearing; F-043 mantém a regra canônica de preço por mercado.
- A soma dos fluxos distribuídos para cada origem/mercadoria/civilização **é exatamente a quantidade produzida**: não formar estoque nem descartar oferta F-039/F-041.
- Só **depois** de resolver o estado estável é que se apuram vendas e créditos monetários **no tesouro da civilização produtora**, além de tarifas entre civilizações conforme contratos futuros.
- O resultado da solução deve ser **determinístico para o mesmo estado de entrada**, inclusive quando houver soluções equivalentes; desempates não podem depender da ordem dos trabalhadores, nós, arestas ou threads.

**Distinção matemática importante:**
- Um **equilíbrio de rotas com origem/destino fixos** e impedâncias crescentes \`C_e(B_e)\` pode, se \`C_e\` for admitida como *impedância marginal*, ser formulado pela minimização da soma de primitivas \`Σ_e ∫₀^{B_e} C_e(z)dz\` sob conservação de fluxos. A integral é um **potencial matemático da rede**, NÃO um pagamento financeiro e NÃO exige congelar custos.
- Entre **mercados de destino diferentes**, é necessário combinar esse princípio com a atratividade comercial e a receita marginal \`R'_{n,i}=P_{n,i}/2\`. Como receita monetária e impedância física não monetária são grandezas distintas, a função global exata de objetivo/equilíbrio **ainda precisa ser definida dimensionalmente**. Não somar/subtrair pontos físicos e dinheiro sem regra/normalização canônica.
- Equilíbrio simultâneo não significa atualizar preços e custos após cada fração de venda, nem fixar os valores com base em expectativa do ciclo anterior: ambos são obtidos como **resultado agregado simultâneo** do cálculo.
- **Não prometer unicidade matemática absoluta** antes da análise de convexidade, mercados acessíveis, conectividade, produtos compartilhando arestas e possíveis múltiplas rotas equivalentes. Exigir um resultado canônico reproduzível.
- Obras F-035 podem melhorar a aresta ao longo do tempo; um clearing referente a uma janela temporal precisa usar snapshot causal coerente e não antecipar obra futura, sem apagar benefícios imediatos aplicáveis aos deslocamentos físicos dos trabalhadores.

**Plano de validação prioritário:**
1. Duas rotas paralelas idênticas, mesma origem/destino e carga total 2, capacidade natural 1 cada, com convexidade \`p>1\`: equilíbrio simétrico deve distribuir carga **1+1** e não alternar integralmente A→B→A entre ciclos, nos limites do caso de teste.
2. Uma rota curta costeira congestionável e alternativa longa; adicionar investimento apenas numa aresta e comprovar realocação, receita e conservação de toda a oferta.
3. Dois destinos com \`D_A=4\` e \`D_B=1\`, total \`Q=10\`; sem assimetrias logísticas, recuperar \`Q_A=8\`, \`Q_B=2\` do teste F-044.
4. Dois itens diferentes compartilhando uma aresta estreita, e duas civilizações produzindo em origens distintas; verificar que congestionamento agrega todos os fluxos, mas crédito da venda **separa os tesouros**.
5. Verificar invariância à ordem de execução, reprodutibilidade por replay, comportamento para \`Q=0\`, \`D=0\`, arestas intransitáveis, e desempenho em grafos Goldberg crescentes.
6. Caso haja gargalo computacional real, comparar solver simultâneo com opção de custos defasados **sem alterar nenhuma regra econômica ratificada**; a troca exige nova deliberação.

**Status:** método de equilíbrio simultâneo e fallback condicionado a desempenho **ratificados**; fórmula de saturação \`p\`, construção exata do potencial conjunto, algoritmo numérico, tolerâncias, desempate, distribuição por propriedade de arestas e tarifas seguem **EM ABERTO**.


### F-050 — Experimento histórico de atratividade P/L [SUBSTITUÍDO POR F-056]

**Problema de integração [ESTUDO HISTÓRICO ANTERIOR À F-056]:** a receita monetária do produto vendido no mercado \`n\` segue \`P_{n,i}=√(D_{n,i}/Q_{n,i})\`, com \`R_{n,i}=√(D_{n,i} Q_{n,i})\` por F-043. A impedância \`C_e(B_e,I_e)\` de F-031/F-049 é **não monetária**, física/logística. Não se pode subtrair diretamente custo físico de receita em moeda, mas o water-filling precisa preferir mercados suficientemente valiosos e rotas fisicamente acessíveis.

**Candidata simples de índice de atratividade de cada rota \`r\` até destino \`n\` para mercadoria \`i\`:**

\`L_r(B,I)=Σ_{e∈r}C_e(B_e,I_e)\`, com \`L_r>0\` e a mesma unidade física normalizada para todas as arestas.

\`A_{r,i}=P_{n,i}(Q_{n,i})/L_r(B,I)\`.

Como \`R'_{n,i}=P_{n,i}/2\` para a curva canônica, usar \`A^{marg}_{r,i}=R'_{n,i}/L_r\` é **equivalente na classificação de rotas** (multiplicador positivo global \`1/2\`); há uma **mesma unidade de preço por mercado**, mas **várias atratividades relativas conforme a rota**.

**Interpretação:** não é um pagamento monetário \`P-L\` nem uma despesa lançada ao tesouro. O preço \`P\` continua sendo recebido pela civilização produtora por unidade vendida, e \`L_r\` representa resistência relativa de acesso. Multiplicar **todas** as impedâncias por uma mesma constante positiva não muda a ordenação das atratividades; não se introduz parâmetro de conversão de "pontos físicos" em moeda. A escolha de dividir pelo custo é, contudo, **hipótese econômica de comportamento do mercado**, não resultado matemático obrigatório de F-043/F-049.

**Candidato de condição de equilíbrio:** para cada origem, mercadoria e regime de propriedade/civilização, todas as rotas/destinos usados devem apresentar o mesmo valor máximo de \`A^{marg}\` entre as alternativas economicamente elegíveis, respeitando a conservação de **100% da produção**. Fluxos, \`Q_{n,i}\`, \`B_e\` e \`C_e\` precisam ser resolvidos **simultaneamente**, conforme F-049. Impedância de rota é soma das **impedâncias marginais** na interpretação de F-032, não custo médio monetário, e suas taxas de carga precisam usar janela temporal consistente.

**Laboratório analítico sem congestionamento [PROPOSTO]:** uma origem produz \`Q_T=10\` de uma mercadoria; mercados A e B têm \`D_A=4,D_B=1\`; impedâncias fixas \`L_A=1,L_B=2\`. Igualdade de \`P_A/L_A=P_B/L_B\`, com \`Q_A+Q_B=10\`, implica:
\`Q_A/Q_B=(D_A/L_A²)/(D_B/L_B²)=16\`,
\`Q_A=160/17≈9,4117647\`, \`Q_B=10/17≈0,5882353\`,
\`P_A≈0,6519202\`, \`P_B≈1,3038405\`, \`A_A=A_B≈0,6519202\`.
Com \`L_A=L_B\`, recupera F-044 \`Q_A=8,Q_B=2\` com preço igual.

**Laboratório simultâneo com congestionamento (todos os coeficientes a seguir são ilustrativos, não ratificados):**
\`C_A(q_A)=1+(q_A/5)^4\`,
\`C_B(q_B)=2×[1+(q_B/10)^4]\`,
\`D_A=4,D_B=1,Q_T=10\`; resolver
\`√(4/q_A)/C_A(q_A)=√(1/q_B)/C_B(q_B)\` e \`q_A+q_B=10\`.
Resultado aproximado de bisseção:
\`q_A≈6,1256452346\`, \`q_B≈3,8743547654\`,
\`C_A≈3,2528244306\`, \`C_B≈2,0450637717\`,
\`P_A≈0,8080794734\`, \`P_B≈0,5080428074\`,
\`P_A/C_A≈P_B/C_B≈0,2484239438\`;
receita bruta total \`q_A P_A + q_B P_B≈6,9183462475\` a ser creditada ao tesouro da civilização originadora, antes de eventuais tarifas monetárias explícitas.

**Propriedade desejável:** ao aumentar volume na rota inicialmente mais barata, a própria impedância sobe e torna outras alternativas relativamente mais interessantes. A distribuição é calculada com cargas finais, **sem congelar custos defasados**.

**Riscos/limites críticos antes de ratificar:**
1. **Global versus individual:** maximizar \`Σ_n R_n(Q_n,D_n)\` para todo o mercado não é, necessariamente, o mesmo problema que cada civilização/produtor maximizar sua receita. Para um produtor \`c\` com parcela \`q_{c,n}\` no destino e \`Q_n=Σ_c q_{c,n}\`, sua derivada própria de receita é \`∂[q_{c,n}P_n(Q_n)]/∂q_{c,n}=P_n(Q_n)×[1-q_{c,n}/(2Q_n)]\` se as demais quantidades estiverem fixas, e não \`P_n/2\` exceto quando ele é único vendedor. A regra de competitividade e o estatuto de cada civilização precisam ser estabelecidos conscientemente; **a F-048 preserva crédito integral às civilizações pela quantidade que realmente venderem, e não resolve sozinha o objetivo do allocator**.
2. **Cauda infinita de destinos:** quando \`Q_{n,i}→0^+\`, \`P_{n,i}→+∞\`, logo \`A_{r,i}→+∞\` em qualquer rota de impedância positiva finita. Em modelos contínuos com oferta distribuível infinitesimalmente, todo mercado conectado com \`D>0\` tende a receber **alguma parcela positiva**; isso pode gerar elevada densidade de destinos/rotas num grafo Goldberg muito grande. Nenhum limiar arbitrário foi aprovado para eliminar essa cauda.
3. **Não garantia de potencial global simples:** \`P_n/L_r\` não é automaticamente a derivada de um potencial de otimização convexo em redes multiorigem/multimercado com congestionamento compartilhado. É possível formular condições de equilíbrio como sistema ou desigualdade variacional, mas prova de existência/unicidade, convergência, estabilidade, simetria e desempate determinístico precisa ser feita antes de afirmar que o solver está definido.
4. **Comparabilidade logística:** a razão presume \`L_r>0\` e uma impedância física comum, derivada de propriedades naturais/geométricas e investimento. Multiplicar todas as impedâncias pelo mesmo fator não muda a divisão; fatores individuais por item são rejeitados F-028. A função e unidade exatas de \`C_e\` seguem F-031 [PROPOSTO].
5. **Tarifas reais:** tarifa de infraestrutura de terceiro é dinheiro e deve ser contabilizada no tesouro civilizacional, não confundida com \`L_r\`. Como tarifa afeta escolha do produtor e liquidação, a versão completa do índice com tarifas ainda não está definida; não cobrar a mesma tarifa duas vezes.
6. **Algoritmo:** igualdade de atratividade fornece condição candidata de ótimo local/interior, mas não define sozinho solver de rede. Comparar métodos de solução simultânea, reprodutibilidade de estado e comportamento numérico com \`Q\` muito pequeno.

**Status:** regra \`P/L\` e seu equilíbrio simultâneo são uma **candidata que combina F-043 e F-049 sem nova variável monetária**. Resultados dos exemplos foram demonstrados; **nenhum destes resultados ratifica a regra de mercado, o objetivo agregado, a função específica de saturação ou o solver**.

### F-051 — Gate de validação do clearing multi-civilização [ALTERNATIVA A RATIFICADA PELA F-054; ESTUDO HISTÓRICO]

**Próximo contrato a decidir:** qual é o agente/critério de escolha do water-filling quando várias civilizações concorrem pelos mesmos destinos e o preço é compartilhado?

**Alternativa A [RATIFICADA COMO ARQUITETURA DE MERCADO EM F-054]:** alocador global determinístico, conservando 100% da oferta por civilização e atribuindo a receita nominal de cada parte ao seu tesouro. **A fórmula matemática particular P/L ou R'/L permanece proposta em F-050/F-053**, não é canonizada pela escolha do alocador global.

**Alternativa B [mais estratégica]:** cada civilização atua como ofertante que procura maximizar sua própria receita pela participação \`q_{c,n}\` e depende dos demais no mesmo mercado, possivelmente produzindo um equilíbrio competitivo/Nash diferente; maior custo de cálculo e possibilidade de desigualdade entre civilizações com quotas distintas.

**Experimento F-052 realizado:** duas civilizações, duas origens, dois destinos, infraestrutura compartilhada, preços comuns, venda integral sem carteira, créditos separados e congestionamento endógeno. Confirmou solução global coerente, mas **divergência entre critério de atratividade logística e maximização unilateral de receita monetária**. Eficiência computacional em mapas maiores, convenções de \`Q→0\` e regra de desempate continuam pendentes.


### F-052 — Laboratório 2 civilizações × 2 mercados: alocação global versus interesse monetário individual [VALIDADO EM CENÁRIOS; CONCLUSÃO DE DESIGN AINDA PROPOSTA]

**Escopo:** não alterar as decisões ratificadas F-041 (100% vendido), F-043 (preço \`√(D/Q)\`), F-046 (compradores sem carteira), F-048 (tesouro por civilização) e F-049 (equilíbrio simultâneo). Testar F-050 (\`A=P/L\`, ainda proposta) sob duas civilizações com origens distintas e mesmas duas arestas de gargalo que alimentam os mercados.

**Dados laboratoriais (não canônicos):**
- civilização \`C₁\` produz \`Q₁=6\`; civilização \`C₂\` produz \`Q₂=4\`; total 10 de uma mercadoria;
- mercados \`X\` e \`Y\` têm \`D_X=4\` e \`D_Y=1\`, de modo que \`P_X=√(4/Q_X)\`, \`P_Y=√(1/Q_Y)\`;
- gargalo compartilhado final para X: \`C_X(Q_X)=1+(Q_X/5)^4\`; gargalo compartilhado final para Y: \`C_Y(Q_Y)=2×[1+(Q_Y/10)^4]\`. Expoente 4 e capacidades são **apenas ensaio** da F-031;
- as duas origens possuem acesso físico independente às arestas de gargalo; os fluxos das duas civilizações **se somam no mesmo congestionamento dos trechos finais**, mas as receitas são creditadas separadamente. Não introduzir custo separado por item.

**Cenário A — acessos iniciais simétricos:** cada origem adiciona impedância 0,5 até o tronco de ambas as rotas, isto é, \`L_X=0,5+C_X(Q_X)\` e \`L_Y=0,5+C_Y(Q_Y)\`.
Equilíbrio \`P_X/L_X=P_Y/L_Y\`, \`Q_X+Q_Y=10\`:
- \`Q_X≈6,2471005034\`; \`Q_Y≈3,7528994966\`;
- \`P_X≈0,8001856324\`; \`P_Y≈0,5161982560\`;
- \`L_X≈3,9368789382\`; \`L_Y≈2,5396732457\`; ambas atratividades \`A≈0,2032538071\`.
- Como as duas civilizações têm acesso equivalente, uma regra de desempate **proposta, não ratificada**, é fracionar cada destino na proporção das respectivas ofertas \`6:4\`:
  - \`C₁:\` vende \`3,7482603\` a X e \`2,2517397\` a Y; recebe \`R₁≈4,1616481452\`;
  - \`C₂:\` vende \`2,4988402\` a X e \`1,5011598\` a Y; recebe \`R₂≈2,7744320968\`;
  - \`R_total≈6,9360802421\`. Não houve moeda recebida por trabalhadores, carteira compradora ou mercadoria sem venda.
- **Comparação estratégica:** se \`C₁\` pudesse escolher seus destinos visando **somente receita monetária bruta** com o fluxo de C₂ fixo, sua melhor resposta deslocaria \`q_{1,X}\` de \`3,7483\` para ≈\`4,9134\`, elevando sua receita de ≈\`4,1616\` para ≈\`4,2849\`; no caso de C₂, melhor resposta unilateral \`q_{2,X}≈3,5597\` em vez de \`2,4988\`, elevando ≈\`2,7744→2,9019\`. Logo **a alocação P/L não é o equilíbrio de civilizações que maximizam receitas brutas individualmente e ignoram logística**.

**Cenário B — vantagens geográficas assimétricas:** C₁ tem abordagem logística 0,3 até X e 1 até Y; C₂, inversamente, 1 até X e 0,3 até Y. Cada abordagem distinta se conecta ao **mesmo gargalo final por mercado**, logo as arestas congestionadas têm as cargas agregadas das duas civilizações.
- Solução de equilíbrio de atratividade F-050: C₁ envia 6 para X; C₂ envia 4 para Y. \`Q_X=6\`, \`Q_Y=4\`.
- \`P_X≈0,8164965809\`, \`P_Y=0,5\`, \`C_X=3,0736\`, \`C_Y=2,0512\`.
- C₁: \`A_{1,X}=0,81649658/(3,0736+0,3)≈0,2420253\`, acima de \`A_{1,Y}=0,5/(2,0512+1)≈0,1638700\`. C₂: \`A_{2,Y}=0,5/(2,0512+0,3)≈0,2126574\`, acima de \`A_{2,X}=0,81649658/(3,0736+1)≈0,2004361\`.
- Receita nominal: \`R₁=6 P_X≈4,898979486\`; \`R₂=4 P_Y=2\`; \`R_total≈6,898979486\`.
- **Incentivo divergente:** se C₂ redirecionar 1 de suas 4 unidades de Y para X, mantendo C₁ fixa, sua receita **bruta** subiria de 2 para aproximadamente \`√(4/7)+3×√(1/3)≈2,487979754\`, apesar de alterar os congestionamentos e violar a escolha por atratividade. **A receita bruta não internaliza a impedância física como despesa**: uma civilização que controle destinos por lucro nominal pode preferir uma solução diferente daquela atribuída pelo mercado automático. Isso não prova que a mudança fosse ótima ao considerar impedância, nem determina equilíbrio estratégico completo.

**Conclusão demonstrada:** \`P/L\` pode distribuir integralmente a produção de **várias civilizações** em rotas compartilhadas usando preços finais comuns e congestionamento simultâneo; o crédito monetário continua civilizacional. Mas **não** é equivalente a permitir que cada civilização controle unilateralmente destinos e maximize sua receita bruta \`Σ q_{c,n}P_n\`. Propor **alocação de mercado global automática** como representação da economia abstrata, com jogadores influenciando **produção, localização, trabalhadores e infraestrutura**, em vez de manipular a escolha de compradores para cada unidade. Essa recomendação de controle do jogador **ainda requer ratificação**.

**Escala/robustez:** nenhum benchmark em grafos Goldberg grandes foi realizado neste experimento; logo não afirmar custo operacional validado. Exigir resultado reprodutível com permutações de ordem de produtores e concorrência multi-item antes da implementação.

### F-053 — Estudo histórico G=L/P [ATUALIZADO PELA F-056]

**Transformação exatamente equivalente de ranking:** se \`A_r=P_n/L_r\` (F-050) e \`P_n=√(D_n/Q_n)\` (F-043), com \`Q_n>0,D_n>0,L_r>0\`, então **maximizar** \`A_r\` é equivalente a **minimizar**
\`G_r=L_r/P_n=L_r×√(Q_n/D_n)\`.

\`G\` é uma **impedância generalizada relativa ao preço**; não é um custo monetário descontado da receita. É formalmente definida em \`Q_n=0,D_n>0\` por extensão contínua \`G_r=0\` se a impedância da rota permanecer finita. Evita representar infinito do preço para mercado sem fluxo em solver numérico; não significa que exista venda com preço zero ou receita criada sem quantidade.

**Equilíbrio tipo Wardrop [CANDIDATO]:** para cada origem/civilização/mercadoria, todas as rotas utilizadas minimizam \`G_r(f)\`; rotas viáveis não utilizadas possuem \`G_r(f)\` maior ou igual ao mínimo. As cargas por aresta, os volumes de cada mercado e os preços devem ser avaliados em \`f\` simultâneo. É uma definição de **equilíbrio de uso de rede com agentes infinitesimais**, não maximização privada de lucros de produtores finitos; sua validade estratégica precisa de aprovação.

**Caso sem congestionamento, uma única origem com rotas de impedância fixa:** igualar \`P_n/L_n\` e impor \`Σ_nQ_n=Q_T\` produz uma **solução analítica**:
\`Q_n=Q_T×(D_n/L_n²)/(Σ_j D_j/L_j²)\`.
A grandeza \`D_n/L_n²\` funciona como **demanda ponderada por acessibilidade**, derivada da equação canônica de preço e da hipótese P/L, **sem novos índices por item**. Em rotas equivalentes (\`L_n=L\`), recupera-se F-044 \`Q_n∝D_n\`. Em acessos desiguais, \`D_X=4,D_Y=1,L_X=1,L_Y=2,Q_T=10\` ⇒ \`Q_X=160/17≈9,41176\`, \`Q_Y=10/17≈0,58824\`. Essa expressão é **apenas** válida com \`L_n\` fixo; no equilíbrio F-049 com congestionamento endógeno, \`L_n(f)\` deve ser resolvida simultaneamente, não congelada na solução analítica.

**Consequência da curva:** para qualquer mercado \`D_n>0\` e rota com \`0<L_n<∞\`, a solução analítica dá \`Q_n>0\`. Assim, uma economia com milhares de mercados conectados pode alocar frações **muito pequenas** a todos eles (cauda longa), mesmo os extremamente distantes. Isso é um **risco de escalabilidade e interpretação geográfica**, não uma autorização para suprimir rotas por tolerância arbitrária. Não confundir mercado geograficamente inacessível com mercado apenas caro.

**Cautelas:** em grafos multiorigem, multimercado, multiproduto e com arestas compartilhadas, \`G_r\` pode não ser gradiente de um único potencial convexo. A existência de uma equação contínua de equilíbrio **não garante unicidade de fluxos por rota**, solução rápida, nem convergência de iterações simplistas. Os fluxos equivalentes podem exigir desempate determinístico; soma de cargas/preços pode ser única mesmo quando a decomposição de rotas não é. Benchmark numérico e teste de degenerescência são gates necessários.

**Decisões supervenientes:** F-054 ratificou alocação automática global e F-055/F-056 ratificaram fluxos por aresta e A=P/(1+L), inclusive mercado local. O solver simultâneo e a normalização física seguem abertos (F-057).


### F-054 — Mercado global automático, sem escolha individual de destinos [RATIFICADO]

**Decisão aceita pelo usuário:** a economia utiliza um **único alocador comercial global e determinístico**, não um mercado em que cada civilização dá ordens de venda por unidade ou escolhe compradores individuais. O controle do jogador é estratégico: **produção, trabalhadores, localização e infraestrutura**. O mercado determina os destinos comerciais, respeitando a demanda, a acessibilidade e o equilíbrio simultâneo.

**Invariantes:**
1. **100% da oferta** de cada civilização e mercadoria participa do mercado e é vendida, sem estoque, perda ou teto de demanda de referência (F-041).
2. Preço de cada produto em cada destino depende da **oferta agregada de todas as civilizações**; a receita de cada parcela é creditada **somente ao tesouro da civilização produtora** (F-043/F-048).
3. O mercado comprador não possui carteira nem orçamento de consumo que limite compras; o setor consumidor é abstrato (F-046).
4. A rede logística e os preços devem ser resolvidos pelo **equilíbrio simultâneo**; a carga de congestionamento inclui fluxos compartilhados de todas as civilizações e itens (F-049).
5. Não exigir que cada civilização maximize sua receita bruta por manipulação dos destinos: o alocador global é uma **regra do sistema**, não um equilíbrio estratégico de comerciantes autônomos.
6. Reprodutibilidade determinística em estados equivalentes, sem dependência da ordem de iteração ou do número de threads.
7. F-054 ratificou apenas o alocador global; **F-056 superveniente** ratificou a forma de atratividade A=P/(1+L), com L físico adimensional. Continuam não ratificados o expoente de congestionamento p, a escala física exata de L e o algoritmo numérico.

**Racional de design:** foco nas decisões estratégicas, sem micromanagement de carga, mercado ou preços; favorecer especialização emergente e um motor único.


### F-055 — Mercado espacial como fluxo transitório em arestas, sem enumerar rotas [RATIFICADO NO PRINCÍPIO; SOLVER ABERTO]

**Problema:** a função de preço P=√(D/Q) e a atratividade candidata P/L podem levar uma parcela positiva, ainda que minúscula, a todo mercado acessível que tenha D>0. Armazenar antecipadamente todas as rotas origem-destino ou todos os caminhos do grafo é incompatível com um mapa Goldberg de grande escala. Um limite arbitrário de distância, número de compradores ou quantidade mínima alteraria a lei econômica.

**Proposta de estado econômico transitório no ciclo:**
- s[n,c,i] = produção/injeção do item i no nó n, atribuída à civilização c;
- a[n,c,i] = quantidade do item i vendida no mercado n e atribuída à civilização c;
- f[u→v,c,i] ≥ 0 = fluxo comercial abstrato nos arcos orientados da aresta u--v, mantido somente durante o cálculo econômico. O mesmo registro de aresta não é um estoque, veículo ou carga material persistente.

**Conservação por nó, civilização e mercadoria:**

s[n,c,i] + Σ entradas f[u→n,c,i] = a[n,c,i] + Σ saídas f[n→v,c,i].

O balanço global preserva a venda integral por civilização/mercadoria: Σ_n a[n,c,i] = Σ_n s[n,c,i]. A produção pode ser vendida localmente sem percorrer uma aresta.

**Agregação que respeita os contratos já aprovados:**
- Quantidade final no mercado: Q[n,i] = Σ_c a[n,c,i].
- Preço por mercado: P[n,i] = √(D[n,i]/Q[n,i]), quando Q>0 e D>0, F-043.
- Receita comercial **bruta atribuída** à civilização: ReceitaBruta[c] = Σ_n,i a[n,c,i] × P[n,i], F-048; **o crédito líquido efetivo ao tesouro desconta a repartição proporcional entre cotitulares das arestas F-060/F-062**. Não creditar bruto e depois creditar novamente a mesma parcela.
- Carga da aresta não orientada e={u,v}: B[e] = Σ_c,i pesoLogistico[i] × (f[u→v,c,i] + f[v→u,c,i]) / intervaloReferencia.
  Somar as duas direções, em vez de cancelar fluxos opostos. A conversão pesoLogistico permanece exclusivamente dimensional (F-026), não índice comercial arbitrário; onde unidades já forem normalizadas, usar 1.

**Simplificação:** não precisamos distinguir origem A e origem B após a mistura dos fluxos se as duas produções são do MESMO item e da MESMA civilização: o tesouro credor é o mesmo, e a carga física agregada por aresta também. Mas civilizações diferentes exigem canais contábeis separados, pois seus tesouros são diferentes.

**Estratégia operacional candidata:**
1. Resolver o equilíbrio simultâneo diretamente sobre conservação em nós, absorções em mercados e cargas dos arcos, utilizando caminhos candidatos descobertos sob demanda por busca de menor impedância, em vez de enumerar todas as rotas simples antecipadamente.
2. Preservar fluxos em sentidos opostos quando forem economicamente necessários, mas evitar circulação artificial e loops improdutivos se elevarem impedância sem aumentar consumo ou receita.
3. Não afirmar que a solução será barata ou esparsa apenas porque a representação usa arcos. Existem potencialmente muitos mercados com fluxo positivo (cauda da curva), e o custo de um solver multimercadoria/multicivilização pode ser alto; medir com benchmarks.
4. Para cada ciclo, as variáveis são transitórias; apenas saldos dos tesouros, capital de infraestrutura, estado físico dos trabalhadores e demais estados canônicos devem persistir. Resultados de preço, receita e consumo podem ser agregados por ciclo para histórico sem estoque.
5. Como as rotas podem dividir arestas, a carga B[e] precisa ser resolvida com todos os produtos/civilizações no mesmo instante lógico, não por produtor sequencial.
6. A mesma entrada deve gerar exatamente o mesmo resultado, independente da ordem de civilizações, nós, arcos e threads; definir precisão, critérios de desempate e comportamento para Q≈0 em gate posterior.

**Ensaio de conservação:** civilização A produz 6 unidades de madeira, B produz 4; ambas utilizam a mesma aresta até um mercado com D=5. Então Q=10, P=√(5/10)≈0,70710678; carga equivalente na aresta =10 por intervalo de referência, receita A≈4,24264 e receita B≈2,82843. Trabalhadores e compradores não recebem moeda. Um fluxo oposto real de outra civilização também ocuparia capacidade e entraria como soma em B, não como desconto.

**Ponto de atenção: mercado local:** F-050/F-053 usam L=Σ_e C_e. Quando produção e mercado estão no mesmo nó, a rota vazia tem L=0; o critério P/L fica indefinido, e G=L/P faria todo tráfego local possuir impedância comercial zero, dominando os demais destinos mesmo quando a oferta local está saturada. **Não inserir custo logístico de nó ou porto por conveniência** (F-034). F-056 deve avaliar uma regra universal de acessibilidade que admita L=0 sem preferência infinita.

**Status [RATIFICADO]:** o estado comercial transitório em **arcos/arestas**, com conservação em cada nó por civilização e item, agregação de carga nos dois sentidos e ausência de enumeração prévia de todas as rotas é canônico. F-057 mantém abertas a escolha do solver, as tolerâncias, os benchmarks e a demonstração de convergência; o comércio no mesmo nó é permitido sem percorrer arestas (F-056).

### F-056 — Acessibilidade logística com mercado local e distância zero [RATIFICADO NA FORMA; NORMALIZAÇÃO EM ABERTO]

**Defeito identificado na candidata F-050:** P/L só faz sentido se L>0. Comércio no próprio nó usa rota de zero aresta, L=0; então P/L é singular. Se a alternativa G=L/P for usada literalmente, G=0 para toda quantidade local, o que artificialmente favorece venda local infinita.

**Forma ratificada:** definir impedância logística **adimensional e normalizada** L≥0 em unidade física comum e adotar
A = P/(1+L)
ou, numericamente de forma equivalente,
G = (1+L)/P = (1+L)√(Q/D).

A unidade adimensional 1 é o **nível-base universal da atenuação de acessibilidade**, não pagamento, instalação logística ou custo fixo em nó; no próprio nó L=0 e A=P, finita quando há venda Q>0. **Fica ratificada a forma A=P/(1+L), não uma calibração física específica do que vale uma unidade de L**. O critério de normalização precisa ser documentado a partir de propriedades geográficas/tempo/transportes já modelados, sem parâmetros livres por mercadoria ou civilização.

**Checagem demonstrativa sem congestionamento:** dois mercados D_A=4, D_B=1, impedâncias L_A=0 (local) e L_B=2 (remoto), oferta total Q=10. A igualdade de atratividade P_A/(1+0)=P_B/(1+2) produz razões Q_A:Q_B = (4/1²):(1/3²)=36:1, isto é, Q_A=360/37≈9,72973, Q_B=10/37≈0,27027. O mercado remoto não é descartado; também não existe prioridade matemática absoluta para o mercado local. Em cenário equivalente à F-044, se L_A=L_B=0, a distribuição continua proporcional à demanda (8 e 2).

**Alternativa aberta:** utilizar outra forma universal de atenuação de acesso derivada diretamente da geometria ou do tempo real de transporte. Não inventar custos fixos em nós, veículos, porto ou tarifa financeira para contornar L=0.

**Status e próximo gate:** o usuário ratificou F-055 e F-056. A forma A=P/(1+L), e sua transformação exata G=(1+L)/P, substituem a hipótese P/L **como regra de atratividade do modelo inicial**; a forma final de C_e, a unidade natural de normalização de L, o algoritmo numérico e a robustez do equilíbrio permanecem abertas. F-057 organiza o contrato de equilíbrio e os critérios de validação do solver.


### F-057 — Contrato do equilíbrio simultâneo e critérios de aceite do solver [RATIFICADO NO CONTRATO; MÉTODO NUMÉRICO ABERTO]

**Contexto [RATIFICADO]:** F-049 já ratificou o **equilíbrio simultâneo**; F-054, o alocador global; F-055, fluxos transitórios por aresta; F-056, atratividade **A=P/(1+L)** com L≥0 adimensional e mercado local L=0; F-043, preço **P=√(D/Q)**. **Este contrato de equilíbrio com conservação de oferta, avaliação simultânea e determinismo foi aceito pelo usuário; o algoritmo de cálculo, parâmetros numéricos e limites de convergência ainda não estão ratificados.**

**Formulação matemática de referência (pode utilizar caminhos como variáveis matemáticas sem materializar todos os caminhos em execução):**
- Para cada origem o, civilização c, mercadoria i, com oferta s[o,c,i]≥0, escolher fluxos não negativos x[o,c,i,r] sobre caminhos simples r elegíveis até destinos n(r), incluindo o **caminho vazio** quando a venda é no mesmo nó.
- Conservação exata de oferta: Σ_r x[o,c,i,r] = s[o,c,i]. Em execução, F-055 permite representar a solução por f[arco,c,i] e a[n,c,i], sem uma tabela persistente de todos os caminhos.
- Quantidade em cada mercado/mercadoria: Q[n,i] = Σ_{o,c,r:n(r)=n} x[o,c,i,r]. Os pesos de demanda D[n,i]>0 representam presença consumidora, **sem carteira ou orçamento individual**.
- Carga simultânea de cada aresta e (contando ambos os sentidos): B[e] = Σ_{o,c,i,r:e∈r} aLog[i] x[o,c,i,r]/Tref. O custo C[e](B[e],I[e]) é a impedância **candidata de congestionamento F-031** no estado do clearing; o valor p segue aberto.
- Impedância acumulada do caminho: L[r] = Σ_{e∈r} C[e](B[e],I[e]) / Lref, onde Lref é **uma referência física universal de normalização a ser definida**, não uma tarifa e nem índice livre por produto. O caminho local vazio produz L=0.
- Preço monetário por destino/item: P[n,i] = √(D[n,i]/Q[n,i]) quando Q>0; nenhuma cotação de preço de transação para Q=0. Crédito final: Receita[c] = Σ_{n,i} a[n,c,i] P[n,i] ao **tesouro da civilização** (F-048).
- Critério de atratividade equivalente ao da F-056 para cada caminho r: A[r] = P[n(r),i]/(1+L[r]), ou a expressão numérica inversa
  G[r] = (1+L[r])√(Q[n(r),i]/D[n(r),i]).
  G é **índice relativo de escolha**, não cobrança monetária. Para Q=0 e D>0, a extensão contínua fixa G=0 se L é finito; evitar calcular P=∞.

**Condição candidata de equilíbrio de uso da rede:** em cada conjunto de origem o, civilização c e mercadoria i, caminhos usados com x>0 têm G[r] no menor nível alcançável entre caminhos válidos; caminhos não usados têm G[r] não inferior. Todos Q, B, C, L e G são avaliados **no mesmo vetor final de fluxos**, sem custo congelado da rodada anterior e sem fixar preço por ordem de chegada de lotes.

A condição pode ser escrita como **desigualdade variacional**, com conjunto factível de fluxos positivos que conservam cada oferta: encontrar x* tal que Σ_r G_r(x*) [y_r − x*_r]≥0 para todo vetor factível y. Essa é uma **formulação matemática candidata da regra F-056**, não prova de que ela coincide com uma função objetivo de maximização de receita global. Com número finito de nós, destinos e caminhos simples, contínuos e custos finitos, existência de ao menos uma solução é plausível pelos teoremas usuais para desigualdades variacionais; **unicidade e convergência numérica NÃO são garantidas** sem hipóteses adicionais. Estruturas com nenhuma demanda acessível, arestas intransitáveis ou custo não finito exigem contrato de entrada antes da aplicação.

**Critério de aceite computável (gap de equilíbrio, não apenas diferença entre iterações):**
Para cada fonte o,c,i, seja m[o,c,i]=min_{r viável} G_r(x*) sobre destinos elegíveis. Definir
Gap(x*) = Σ_{o,c,i,r} x[o,c,i,r] G_r(x*) − Σ_{o,c,i} s[o,c,i] m[o,c,i].
Este valor é ≥0 em condições exatas e vale **zero se e somente se** todo fluxo efetivo utiliza alternativas de custo generalizado mínimo, sujeito à conservação e ausência de ciclos artificiais. O teste deve usar referência final e incluir todos os mercados conectados, não só a lista de caminhos materializados no solver. A implementação por arcos pode obter m com um **oráculo de caminho mínimo** com pesos de aresta C[e] do estado final, seguido da penalidade final sqrt(Q[n,i]/D[n,i]); sem enumerar todas as rotas.

**Estratégia de implementação sugerida [PROPOSTA, NÃO GARANTIA DE CONVERGÊNCIA]:**
1. Snapshot causal e imutável de nós, arestas, capital/condições naturais, trabalhadores/demandas e produção do ciclo.
2. Construir uma solução **factível** (conservar toda oferta por civilização e produto, mesmo quando os custos ainda estão longe do equilíbrio). Se nenhuma demanda positiva for acessível para um componente com produção positiva, lançar falha explícita de pré-condição, **não perder oferta** nem inventar consumidor.
3. Agregar fluxos efetivos em arcos por civilização/item; recalcular B, C, Q e G para o **mesmo** estado provisório, com todos os itens compartilhando carga.
4. Para cada origem e mercadoria, consultar um **oráculo de menores caminhos em arestas** até mercados acessíveis para identificar alternativas mais atraentes, em vez de enumerar todos os caminhos simples.
5. Atualizar a distribuição de forma **amortecida e conservativa** (combinação convexa de soluções factíveis), avaliando o Gap do estado final. **Não** adotar atualização ingênua do tipo "mover toda a produção para a melhor rota" a cada passo; pode oscilar. Passo fixo, método de amortecimento, critérios de linha de busca e solver exato de VI precisam de benchmark antes de ratificação.
6. Parar quando houver **conservação de volume, gap de equilíbrio dentro da precisão homologada, receitas corretas, ausência de fluxos circulares artificiais e determinismo**. A tolerância numérica não deve ser usada como raio econômico de exclusão de mercados: não declarar parcelas positivas como zero por regra de gameplay arbitrária.
7. Se não houver convergência, expor falha de solver e diagnóstico, sem aceitar silenciosamente resultado não equilibrado. A alternativa de expectativa congelada de F-047 só pode ser adotada após decisão específica motivada por custo/desempenho.

**Experimento mínimo (um nó produtor local e mercado remoto):** Q=10 de um item; demanda D_local=4 e D_remoto=1; venda local com L=0; aresta única até remoto com **custo ilustrativo** C_remote(q)=1+(q/5)^4, tomada já na unidade de referência Lref=1 para esse ensaio, p=4 não ratificado. O equilíbrio exige
sqrt((10−q)/4) = [1+C_remote(q)] sqrt(q/1),
com q=quantidade comercializada remotamente.
Solução por bisseção: q_remoto≈0,5881293258; q_local≈9,4118706742; impedância remota≈1,0001914306; preço local≈0,6519165705; preço remoto≈1,3039579378; receita bruta total≈6,90265035497, integralmente creditada à civilização produtora. O caso comercial local **não força fluxo integral local**, e nenhum estoque é gerado.

**Por que não basta uma passagem gulosa:** começando com todo Q=10 no mercado local, a opção remota inicialmente vazia tem G_remoto=0 (extensão contínua), enquanto G_local=√(10/4)≈1,5811; transferir **tudo** ao remoto faz G_local=0 e G_remoto muito alto, gerando ida e volta se o solver escolher a melhor opção e transferir todo o lote. Isso não contradiz o equilíbrio simultâneo; exige algoritmo de aproximação com verificação de gap, não simples troca de caminho por período.

**Teste de robustez a realizar:** três ou mais nós com rotas paralelas e ciclos potenciais; produção de duas civilizações sobre a mesma aresta; produtos distintos competindo por capacidade; venda local; rotas de impedância nula ou intransitável; permutações de enumeração; grandes mapas Goldberg; preço/fluxo muito pequenos; condição de D=0; gargalo costeiro forte; remuneração por civilização distinta; benchmark de tempo e memória. Não afirmar esses testes já executados — apenas a bisseção do caso escalar acima foi verificada.

**Status superveniente [RATIFICADO NO CONTRATO]:** a condição de equilíbrio com **conservação por origem/civilização/mercadoria**, avaliação conjunta de preços/congestionamento, restrições de não-negatividade, **gap de equilíbrio como critério de homologação** e determinismo foi aceita. A desigualdade variacional é **formulação matemática de referência do contrato** enquanto se testa sua aplicabilidade em redes gerais; não constitui prova de unicidade nem de que uma implementação particular convergirá. **Oráculo, iteração amortecida, passo, tolerâncias, desempates, solver final e p=4 continuam abertos**. F-058 fornece evidência de laboratório e revela não unicidade de decomposição de rota.


### F-058 — Ensaio executado: rede diamante com duas civilizações, dois itens e dois caminhos [TESTE NUMÉRICO DE LABORATÓRIO; RESULTADOS VERIFICADOS]

**Objetivo:** confrontar F-057/F-056/F-055/F-049 em um exemplo de rede, incluindo venda local, rotas paralelas, congestionamento em ambos os sentidos, dois produtos distintos e tesouros de duas civilizações, sem introduzir carteira de compradores, estoque, transferência física de cargas ou coeficiente logístico por item. **Este é um experimento numérico isolado em Python/SciPy, NÃO uma implementação do kernel do jogo nem benchmark de mapas grandes.**

**Rede:** nós A, B, C, D. Corredores A–B–C (superior) e A–D–C (inferior); civilização Alfa produz em A, civilização Beta em C; os próprios nós A e C também são mercados. Em cada um dos dois mercados, \`D_madeira=D_pedra=2\`. Demanda é referência de preço, sem orçamento de comprador.

**Ofertas por ciclo (normalização de tempo e produtos somente para este ensaio):**
- Alfa em A: 8 de madeira e 2 de pedra.
- Beta em C: 2 de madeira e 8 de pedra.
- Total por produto: **10 unidades**, total econômico abstrato **20 unidades**. Fluxo de ambas as direções ocupa capacidade da aresta.

**Impedância de arestas [p=4 E COEFICIENTES ESTRITAMENTE ILUSTRATIVOS]:**
- \`C_AB(B)=C_BC(B)=0,17×[1+(B/1,6)^4]\`.
- \`C_AD(B)=C_DC(B)=0,25×[1+(B/2,6)^4]\`.
- \`L_r=Σ_e C_e(B_e)\` já tomado na unidade de referência \`Lref=1\` **apenas neste experimento**, e a atratividade canônica F-056 usa \`A=P/(1+L_r)\`; cada origem dispõe de venda local com L=0, remoto pelo corredor superior ou inferior.
- Preço \`P_{n,i}=√(D_{n,i}/Q_{n,i})\` e \`G=(1+L)√(Q/D)\`.

**Protótipo exploratório:** algoritmo **extragradiente projetado** em blocos de simplex de três alternativas por civilização/mercadoria: local, corredor superior, inferior. Cada iteração avalia simultaneamente as cargas de todas as arestas/itens/civilizações, depois modifica vetores de fluxo mantendo a oferta de cada fonte exatamente constante. O protótipo não foi integrado ao código do GA nem demonstra desempenho aceitável em produção.

**Resultado convergente (precisão exibida, não parâmetros canônicos):**
- Alfa madeira: 6,93545479 local; 0,66707616 via superior; 0,39746905 via inferior; Alfa pedra: 2 local.
- Beta madeira: 2 local; Beta pedra: 6,93545479 local; 0,66707616 via superior (sentido oposto); 0,39746905 via inferior (sentido oposto).
- Carga nas arestas AB e BC: \`B≈1,33415232\` por aresta, somando madeira+pedra/sentidos opostos. Carga nas arestas AD e DC: \`B≈0,79493810\` por aresta.
- Impedância unitária final \`C_e≈0,25218464\` em **cada** aresta de ambas as alternativas; custo do corredor superior \`L≈0,50436928\`, inferior \`L≈0,50436928\`. Embora as capacidades e custos naturais difiram, o congestionamento simultâneo iguala as impedâncias finais das alternativas efetivamente utilizadas.
- Mercado A: madeira \`Q≈6,93545479\` (P≈0,53700401); pedra \`Q≈3,06454521\` (P≈0,80785233).
- Mercado C: madeira \`Q≈3,06454521\` (P≈0,80785233); pedra \`Q≈6,93545479\` (P≈0,53700401).
- **Receita da civilização Alfa ≈6,20006699 moedas; Beta ≈6,20006699**; receita total ≈12,40013398. Trabalhadores não recebem moeda e consumidores não são debitados.
- Conservação por fonte: exatamente \`8,2,2,8\` (até arredondamento computacional de ponto flutuante); por item: exatamente 10+10; no mercado Q=Σ civilizações; diferença entre impedâncias dos corredores cerca de \`2×10⁻¹¹\`.
- Gap total da condição de F-057 **≈5,63×10⁻¹⁰**, e máximo desvio de G entre alternativas efetivamente utilizadas **≈3,83×10⁻¹¹**.

**Testes de partida e estabilidade:**
- Começando com tudo vendido localmente, protótipo com passo \`α=0,04\` atingiu gap menor que \`10⁻⁹\` em ~2500 iterações; começando com todo fluxo na rota inferior, resultado agregado equivalente em ~2500 iterações.
- Começando com tudo na rota superior e mesmo passo \`α=0,04\`, **NÃO CONVERGIU** em 15 mil iterações (gap ~8,85×10² naquele ensaio). Diminuindo passo para \`α=0,01\`, convergiu com gap menor que \`10⁻⁹\` em ~10.300 iterações.
- Portanto **o método iterativo simples é sensível ao passo e à condição inicial**; a convergência num caso favorável não autoriza canonizar passo fixo nem considerar o algoritmo robusto.
- Com inicialização aleatória, o protótipo convergiu ao **mesmo preço, Q por mercado, carga B por aresta e receita de cada civilização**, mas com **decomposição diferente dos fluxos madeira/pedra pelos corredores**: por exemplo, madeira superior \`≈0,469072\` versus \`≈0,667076\` no resultado acima, compensada por maior fluxo de pedra no mesmo corredor, mantendo cargas agregadas. Isso é **não unicidade de decomposição de mercadoria/rota** apesar de resultados agregados praticamente iguais.

**Consequência arquitetural de alto impacto:** se tarifas monetárias e propriedades de arestas forem depois implementadas, duas soluções com idênticos preços/cargas/receitas brutas podem gerar **receitas tarifárias e custos líquidos diferentes conforme qual civilização/mercadoria percorreu a rota**. Portanto o solver precisa de **desempate canônico economicamente neutro e determinístico**, não apenas equivalência de cargas agregadas. Esse desempate não foi ratificado. Não permitir que ordem de iteração, semente aleatória ou detalhe de precisão altere indevidamente tarifas de civilizações.

**Conclusão:** viabilidade demonstrada do equilíbrio simultâneo para pequena rede multicommodity com conservação e atribuição monetária civilizacional. **Não** demonstradas unicidade de todos os fluxos, robustez do passo numérico, precisão bit-a-bit entre plataformas, escalabilidade nem efeitos tarifários. Não transformar estes resultados experimentais em contratos do jogo sem deliberação.

### F-059 — Tarifas dentro do equilíbrio; desempate somente após distinguir resultados econômicos [RATIFICADO NO PRINCÍPIO; POLÍTICA SECUNDÁRIA ABERTA]

**Problema extraído da F-058:** a rede pode ter a mesma solução econômica agregada (preços por destino/produto, cargas por aresta, receitas brutas por civilização), mas múltiplas decomposições válidas de fluxo por mercadoria/civilização/corredor. Sem regra canônica, replays podem divergir em fluxos por titular e eventuais tarifas financeiras futuras.

**Princípio ratificado com esta deliberação:** distinguir:
1. **Equilíbrio econômico primário completo:** além de Q[n,i], B[e], preços P[n,i] e créditos brutos das civilizações, incluir **tarifas monetárias efetivas por trecho/titular quando houver cobrança** e suas consequências sobre atratividade e receitas líquidas. A tarifa monetária é incentivo econômico real, não desempate técnico.
2. **Seleção canônica secundária:** somente entre soluções **realmente equivalentes inclusive para os saldos/transferências das civilizações**, selecionar decomposição de fluxo por regra explícita, simétrica e independente da ordem de processamento; não alterar Q, B, preço, receita líquida ou tarifa de civilização por uma conveniência numérica.
3. **Empate físico, diferença financeira:** não é empate econômico. Se a regra primária ainda admitir várias soluções com pagamentos monetários diferentes, **é necessário um critério econômico adicional a deliberar**, e não um desempate técnico silencioso. Não presumir que uma escolha tecnicamente neutra exista automaticamente.

**Candidatos para o desempate secundário, SOMENTE quando a contabilidade também coincide [NÃO RATIFICADOS]:**
- **Distribuição proporcional neutra** das parcelas de civilizações/mercadorias entre corredores equivalentes, condicionada às demandas, ofertas, cargas por aresta e restrições de conservação. Evitar índice livre de favoritismo por civilização/item.
- **Projeção de norma mínima/critério convexamente regularizado**, aplicado somente ao conjunto de decomposições já equilibradas e equivalentes, com identidades estáveis; não adicionar penalidade arbitrária ao próprio preço ou à impedância do equilíbrio primário.
- **Identificadores estáveis como último desempate**, após fixar as quantidades econômicas, jamais substituir critérios físicos/econômicos por ordem de enumeração em caso de diferenças reais.

**Ressalvas:** nem toda diferença de fluxos é empate: se a titularidade e a tarifa monetária de arestas alterarem efetivamente decisões de transporte, a tarifa precisa integrar o contrato econômico antes da seleção canônica; não minimizar artificialmente tarifas por desempate. Se duas rotas têm carga agregada equivalente mas pertencem a civilizações diferentes, testar se critério secundário pode deslocar caixa sem justificativa de gameplay. Também não se pode presumir que exista um único equilíbrio primário para redes arbitrárias.

**Solver [EM ABERTO]:** avaliar passos adaptativos, busca de linha, métodos VI robustos e inicializações viáveis; medir gap de equilíbrio e conservação, não só diferença entre iterações. O teste F-058 mostrou instabilidade de passo fixo e variação entre decomposições. Tolerâncias para números muito pequenos não devem apagar mercados efetivos por regra econômica implícita.

**Experimento F-060 documentado:** no diamante F-058, manter as mesmas quantidades/preços/cargas e atribuir o corredor superior à Alfa; hipoteticamente, aplicar taxa de 10% da receita das **vendas da Beta que utilizam aquele corredor**. As duas decomposições antes consideradas equivalentes pela logística produzem cobranças diferentes (≈0,05389 contra ≈0,06989). Isso demonstra por que cobrança não pode ser adicionada depois do desempate. **A partir do momento em que a tarifa entra no equilíbrio, as antigas decomposições sem tarifa deixam de ser candidatas presumidamente equilibradas; o experimento ex post serve para demonstrar o erro, não para calcular o novo equilíbrio tarifado.**

**Status:** a inclusão das tarifas reais no equilíbrio primário e a proibição de desempates técnicos que redistribuam moeda foram **RATIFICADAS**. A fórmula tarifária, a política de desempate genuinamente neutra e o solver robusto seguem em aberto.


### F-060 — Tarifas proporcionais à receita comercial e repartição contábil [RATIFICADO O RATEIO; PESOS ABERTOS; COPROPRIEDADE F-062]

**Motivação:** F-041 obriga venda de **toda** a produção; F-043 fixa \`P=√(D/Q)\` e \`P→0\` para Q muito maior que D. Uma tarifa **nominal fixa por unidade**, mesmo que logisticamente coerente e cobrada por titular de aresta externa, pode superar o preço unitário e forçar **receita líquida negativa**. Isso entraria em conflito operacional com tesouros finitos das civilizações e o compromisso de venda integral, sobretudo em mercados distantes com acesso somente por infraestrutura alheia. Não resolver criando compradores com carteiras nem descartando quantidades.

**Decisão superveniente e ratificada:** calcular o encargo tarifário **como fração da receita bruta efetivamente produzida pela venda**, proporcional ao volume e ao preço do item no mercado de destino, em vez de um valor monetário absoluto que independa do preço. **A estrutura de rateio conjunto descrita a seguir fica adotada como contrato inicial**, mantendo **a função específica dos pesos \`w_e=φ(I_e)\` e sua normalização/limites EM ABERTO (F-061)**. A copropriedade econômica foi fechada em F-062, enquanto autoridade diplomática e embargos permanecem F-063. Não inferir tarifa positiva em aresta sem capital incorporado nem função φ validada.

**Generalização superveniente de F-062 [RATIFICADA]:** cada aresta e pode ter vários titulares econômicos. Capital \`I[e,c]\` define quota \`s[e,c]=I[e,c]/Σ_j I[e,j]\` quando o denominador é positivo; w_e=φ(I_e) continua variável de intensidade **ABERTA**. O vendedor c deve somente participações alheias: \`W[c,r]=Σ_{e∈r}Σ_{j≠c}w_e s[e,j]\`. Uma aresta não melhorada com capital total zero não cobra tarifa. A forma histórica de titular único corresponde ao caso s=1 para somente uma civilização.

Para uma venda de quantidade \`q\` no mercado \`n\` do item \`i\`, com preço final \`P_{n,i}\`, o **rateio financeiro aprovado** é:

\`V_bruta=q×P_{n,i}\`;

\`V_liquida_produtora=V_bruta/(1+W_{c,r})\`;

\`T_{e,j←c,r}=V_bruta×w_e s[e,j]/(1+W_{c,r})\`, para cada cotitular j≠c da aresta e. A própria quota do vendedor é isenta.

**Identidade financeira exata:** \`V_liquida_produtora + Σ_{e,j≠c} T_{e,j←c,r} = V_bruta\`. Não há dinheiro inventado entre contas representadas: a receita comercial bruta entra do **setor consumidor externo/implícito F-046** e é repartida contabilmente entre a **civilização produtora** e os **titulares das arestas de terceiros**. Não criar carteira de trabalhador ou consumidor.

**Atratividade logística com tarifa [CANDIDATA, NÃO CONFUNDIR COM A RATIFICAÇÃO DO RATEIO]:**
\`A_{c,r,n,i} = [P_{n,i}/(1+W_{c,r})]/[1+L_r]\`,
\`G_{c,r,n,i}=(1+L_r)(1+W_{c,r})√(Q_{n,i}/D_{n,i})\`.
Esta forma preserva F-056 quando todas as tarifas são zero; não converte pontos de impedância em moeda. O fator \`1+W\` é **proposta de desenho de cobrança comercial**, independente do fator logístico \`1+L\`. Se uma rota própria e outra externa possuem as mesmas condições físicas, o proprietário do próprio trecho não paga tarifa e pode preferi-la por esse motivo. A carga comercial e a forma de propriedade do trecho são consideradas simultaneamente no clearing F-049.

**Teste dimensional/numérico de transação única:** com \`q=5\` unidades vendidas a \`P=1,4\`, \`V_bruta=7\` moedas. Se o trajeto atravessa uma única aresta alheia com \`w_e=0,25\`, então \`W=0,25\`, a civilização produtora recebe \`V_liquida=7/1,25=5,6\` moedas, e a proprietária recebe \`T=7×0,25/1,25=1,4\`. O exemplo corresponde a **20% de receita bruta** de tarifa, não a 25%, porque \`w\` é intensidade adimensional anterior à conversão \`w/(1+w)\`; não confundir os dois números. Se \`P\` diminuir sem mudar a rota nem a propriedade, o valor nominal de ambos cai proporcionalmente; a receita líquida de venda permanece **não negativa** para \`q,P≥0\` e \`W\` finito.

**Teste de preferência por rota:** para o mesmo destino/preço P, comparar corredor externo \`L_externo=0,5,W_externo=0,25\` com corredor próprio \`L_proprio=1,W_proprio=0\`. Seus divisores de atratividade são \`(1+0,5)(1+0,25)=1,875\` e \`(1+1)(1+0)=2\`. Logo o corredor externo continua mais atrativo, apesar de sua tarifa, devido à vantagem física. Se a intensidade da tarifa subir para \`W_externo=0,5\`, o divisor externo passa a \`2,25\` e a própria infraestrutura fica mais atraente, ceteris paribus. Na rede completa, o congestionamento resultante de cada escolha também realimenta L e precisa de equilíbrio simultâneo.

**Demonstração ex post de desempate não neutro (F-058; não é solução de rede com tarifas):**
- Considere a solução anterior sem tarifas com Alfa produzindo madeira e Beta pedra. Preço de exportação de ambos ≈\`0,80785233\`.
- Faça o corredor superior pertencer à civilização Alfa, cobrando hipoteticamente **10% da receita da Beta** que percorre esse corredor. O corredor inferior é não tarifado; a Alfa não paga tarifa na sua própria aresta.
- Em uma decomposição com \`q_Beta,superior≈0,66707616\`, o valor devido de Beta→Alfa é \`≈0,05388990\`.
- Outra decomposição, igualmente equilibrada em preços/cargas quando não existe tarifa, pode usar \`q_Beta,superior≈0,8650803\`, elevando a transferência para \`≈0,069886\`, apesar de os preços finais, cargas físicas e receitas **brutas** continuarem iguais. Diferença ≈\`0,015996\` de moeda entre tesouros determinada apenas pela decomposição.
- **A comparação só serve para provar que as duas soluções SEM tarifa não são financeiramente equivalentes quando se aplica tarifa depois.** Se incluirmos a cobrança corretamente no equilíbrio desde o princípio, é necessário **recalcular a alocação**. Não tratar tais decomposições anteriores como dois equilíbrios do novo sistema com tarifa.

**Questões que a ratificação de F-060 NÃO encerra:**
1. A F-024 permitia tarifa sobre uso alheio; **a natureza proporcional foi agora ratificada em F-060**, mas a origem dos direitos tarifários, a função de intensidade e a titularidade compartilhada não foram fechadas.
2. Uma cobrança proporcional ao valor de mercado permite tarifas monetárias elevadas em bens de maior preço ainda que tenham carga física igual: isso pode ou não ser desejado. O peso tarifário \`w_e\` é universal, mas o montante monetário acompanha o preço do produto. É escolha econômica, não exigência física.
3. Mesmo com \`V_liquida≥0\`, um proprietário de gargalo indispensável pode capturar parcela muito alta de receitas se \`W\` crescer muito, criando incentivos monopolistas. Não definir teto arbitrário antes de discutir investimento, tarifação e incentivos.
4. Várias arestas de terceiros no mesmo trajeto devem cobrar **uma única repartição do valor da venda** conforme a identidade acima; não tributar repetidamente a receita já reduzida em sequência dependente da ordem dos trechos.
5. Quando a mesma civilização possui capital em várias arestas, créditos são agregados ao seu tesouro; uma venda sua permanece isenta **apenas da própria quota de cada aresta**, e paga quotas de outras coproprietárias por F-062.
6. A decisão de onde reside a titularidade de uma aresta e como investimentos de outra civilização afetam sua propriedade precisa de deliberação específica.
7. Caso o jogo futuramente inclua seguros, contratos, preferências de produtos ou outras rubricas, não adotar novas contas por inferência; preservar escopo monetário simples do jogo.

**Gates próximos recomendados:** (a) analisar a função \`φ(I)\` e os incentivos de investimento/propriedade sem novos parâmetros arbitrários (F-061); (b) decidir direitos sobre arestas sem titular, investimento de terceiros, titularidade e eventual copropriedade (F-062); (c) executar equilíbrio simultâneo **com tarifa realmente endógena dentro do índice** em rede 2×2; (d) determinar desempate secundário neutro somente entre soluções equivalentes inclusive em pagamentos.

**Status:** contrato F-059 de incluir tarifas antes do desempate ratificado; **F-060 ratificada na proporcionalidade e na regra de rateio \`V/(1+Σw)\`, \`Vw_e/(1+Σw)\`**; a atratividade tarifada multiplicativa, a determinação de \`w_e\`, propriedade e solver final ainda requerem validação. Não introduzir tarifas arbitrárias antes da titularidade. O preço de mercado \`P\` permanece o mesmo para todos os vendedores do item/destino; o rateio altera somente a **distribuição monetária da receita bruta**.


### F-061 — Intensidade de tarifa vinculada ao ganho físico de acessibilidade [TESTE EXPLORATÓRIO; FÓRMULA NÃO RATIFICADA]

**Objetivo:** encontrar \`w_e=φ(I_e)\` não negativo, universal para produtos, contínuo no investimento e sem percentuais arbitrários por item/aresta, preservando a tarifação proporcional ratificada em F-060. Uma tarifa é percentual monetário derivado do valor da venda; **não se confunde com o custo físico da aresta**. O investimento logístico físico é financiado pelo tesouro da civilização investidora com trabalho F-035/F-048.

**Funções físicas de referência, já propostas mas não canônicas em F-025/F-031:**
\`U(I)=1+log₂(1+I/I₀)\`,
\`C_livre,e(I)=C_natural,e/U(I)\`,
\`K_e(I)=K_natural,e U(I)\`.
Usar \`c₀=C_natural,e/Lref\` e \`c(I)=C_livre,e(I)/Lref\` **adimensionais**. \`Lref\` é a referência física comum de F-056 e segue EM ABERTO; \`I₀\` também não está calibrado.

**Hipótese ingênua analisada e preterida:** \`w=1−1/U\` (fração de redução da impedância em fluxo livre). Com \`c₀=1\`, apenas **uma** aresta de terceiro e \`U=1,01\`:
- Antes: divisor da atratividade \`(1+c₀)(1+w)=2\`.
- Depois: \`c=1/1,01≈0,990099\`, \`w≈0,009901\`, divisor \`(1+c)(1+w)≈2,009803\`.
Assim, **pequena melhoria de infraestrutura torna a rota comercialmente pior imediatamente para terceiros**, porque tarifa cresce mais que o benefício físico. Isso não viola melhoria física F-024, mas revela incentivo questionável na regra de intensidade tarifária. Não canonizar \`w=1−1/U\` sem fundamento.

**Candidata preferida de laboratório: intensidade baseada no ganho de acessibilidade em fluxo livre:**
\`w_e = 1/(1+c_e(I_e)) − 1/(1+c_{0,e})\`.
Supor \`0≤c_e(I_e)≤c_{0,e}<∞\`. Essa função tem:
- \`I=0 ⇒ c=c₀ ⇒ w=0\`, sem tarifa sobre infraestrutura natural ainda não melhorada;
- melhorias físicas que reduzem \`c\` aumentam \`w\`, sem variável de preço de produto e sem coeficiente tarifário adicional;
- \`0≤w_e<1\` quando \`c₀\` finito e \`c≥0\`, daí a **participação tarifária efetiva de uma única aresta** \`w/(1+w)<50%\` (limite matemático da candidata, **NÃO um teto já ratificado**);
- o cálculo usa impedância **livre**, e não a carga B efetiva do ciclo, para evitar realimentação adicional de nível de tarifa pelo próprio congestionamento; o efeito do congestionamento sobre distribuição, preço e receita efetiva continua endógeno.

**Amostra numérica com \`c₀=2\` e \`U=1+log₂(1+I/I₀)\`; números ilustrativos:**

| \`I/I₀\` | U | c livre | w | Percentual efetivo numa aresta \`w/(1+w)\` | Divisor do acesso terceirizado \`(1+c)(1+w)\` |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 1 | 2,000000 | 0 | 0% | 3,000000 |
| 1 | 2 | 1,000000 | 0,166667 | 14,2857% | 2,333333 |
| 3 | 3 | 0,666667 | 0,266667 | 21,0526% | 2,111111 |
| 7 | 4 | 0,500000 | 0,333333 | 25,0000% | 2,000000 |
| 15 | 5 | 0,400000 | 0,380952 | 27,5862% | 1,933333 |

**Propriedade analítica no caso isolado de UMA aresta (sem outros segmentos):**
\`(1+c)(1+w)=(1+c)×[1+1/(1+c)−1/(1+c₀)]=1+(1+c)×c₀/(1+c₀)\`, que **diminui conforme c diminui**. Assim, nessa hipótese, o acesso comercial do não proprietário MELHORA monotonicamente enquanto o titular captura uma parcela percentual da venda. A taxa efetiva também cresce com a redução de c.

**Limite importante em rotas com múltiplas arestas (ainda não resolvido):**
A regra de atratividade tarifada candidata F-060 multiplica \`(1+L_r)(1+W_r)\`. Uma elevação local \`w_e>0\` incide sobre o fator de **toda a rota**, ao passo que a melhoria física reduz somente \`c_e\`. Portanto, **nenhuma função estritamente positiva local \`w_e(c_e)\` pode garantir que melhorar a aresta aumente a atratividade líquida para terceiros em TODAS as rotas arbitrariamente longas** sob esse produto de fatores, pois as demais impedâncias podem ser grandes. Contraexemplo exato com \`c₀=2\`, \`c=1\`, \`w=1/6\`, \`L_{outros}=10\`, nenhuma outra tarifa:
- antes do investimento, divisor \`(1+10+2)(1+0)=13\`;
- depois, divisor \`(1+10+1)(1+1/6)=14\`; piora da atratividade apesar da melhoria física.
Isso é um potencial **efeito econômico do pedágio**, não perda de eficiência física. Se desejarmos futuramente a propriedade adicional "todo investimento melhora também a atratividade comercial para todo não titular", será necessário rever a fórmula global de atratividade/repasse, não apenas ajustar o parâmetro de \`w_e\`.

**Teste simples de equilíbrio com concorrência entre mercado local e remoto (apenas fórmula F-060 CANDIDATA para atratividade):** um produtor de 10 unidades, D_local=4 com rota local L=0 e D_remoto=1 alcançado por uma aresta de outra civilização; impedância da aresta em fluxo livre c₀=2, sem congestionamento neste laboratório. Em cada investimento I, \`H=(1+c(I))(1+w(I))\`; a solução exata do equilíbrio é
\`q_remoto=Q×[D_remoto/H²]/[D_local + D_remoto/H²]\`.
Com \`I/I₀=0\`, \`q_remoto≈0,27027\`, receita tarifária 0. Com \`I/I₀=1\`, \`q_remoto≈0,43902\`, tarifa nominal ao titular ≈\`0,09466\` moeda/ciclo; com \`I/I₀=7\`, \`q_remoto≈0,58824\`, tarifa ≈\`0,19174\` moeda/ciclo. O proprietário ganha receita tarifária e o acesso remoto cresce, mas **a receita líquida da civilização produtora pode cair** (de ≈6,75838 a ≈6,71094 no último caso). A rentabilidade do investimento exige comparar receitas incrementais acumuladas por vários ciclos com o capital incorporado; **não** presumir ROI positivo imediato.

**Riscos que permanecem:** grande quantidade de arestas de terceiros na mesma rota pode reduzir muito a fração de receita do produtor mesmo com pesos individuais limitados; diferenças no crescimento de capacidade K e impedância livre podem alterar o fluxo real; congestionamento endógeno e tarifas têm de ser resolvidos conjuntamente; não há ainda prova genérica de equilíbrio único, custo computacional ou incentivos não exploratórios. **A candidata de w não está ratificada** e não deve entrar no kernel até fechar propriedade e normalização de impedância.

### F-062 — Copropriedade por capital efetivamente incorporado em arestas [RATIFICADO; QUESTÕES DIPLOMÁTICAS ABERTAS]

**Decisão expressa:** a titularidade econômica das melhorias logísticas em cada aresta é **compartilhada em proporção ao capital efetivamente incorporado por cada civilização**; a primeira investidora não captura um monopólio exclusivo permanente e a titularidade econômica não decorre automaticamente do controle territorial.

Para aresta e e civilização c, persistir capital incorporado \`I[e,c]≥0\`; capital total \`I[e]=Σ_c I[e,c]\`. Se \`I[e]>0\`, participação \`s[e,c]=I[e,c]/I[e]\`; se \`I[e]=0\`, não existem quotas definidas nem receita tarifária pela melhoria natural. Cada parcela de capital exige trabalho efetivo de um trabalhador elegível e débito simultâneo do **tesouro da civilização investidora**, F-035/F-048. O investimento de outra civilização melhora a mesma aresta física e altera as participações por nova soma, sem apagar capital anterior e sem exigir etapas fixas de construção.

**Receita tarifária em copropriedade (extensão de F-060):** a civilização vendedora c fica isenta **apenas da sua própria participação** no peso tarifário da aresta e. Para j≠c que possui quota, peso devido ao cotitular j = \`w_e s[e,j]\`, onde w_e≥0 é a intensidade total da aresta e sua forma exata segue F-061 [ABERTA]. Em rota r, \`W[c,r]=Σ_{e∈r}Σ_{j≠c}w_e s[e,j]\`; receita líquida do vendedor da venda V = \`V/(1+W[c,r])\`; cotitular j da aresta e recebe \`V w_e s[e,j]/(1+W[c,r])\`. Soma dos recebimentos = V; o vendedor não paga sua própria quota. Se o vendedor detiver 100% da aresta, sua tarifa é zero; se não tiver participação, paga a soma das quotas dos demais. Uma aresta não melhorada com capital zero não gera tarifa por titularidade.

**Exemplo (parâmetros ilustrativos):** capital A=1 e B=3, s_A=25%, s_B=75%, peso w_e=0,25. Venda da própria B usando essa aresta: peso externo \`0,25×0,25=0,0625\`; B recebe \`V/1,0625≈94,1176%\` e A recebe \`5,8824%\`. Venda de terceira civilização C: W=0,25; C recebe 80%, A recebe 5%, B recebe 15%. Nenhum trabalhador possui tesouro de receita; o mercado comprador continua sem carteira.

**Sem inferir governo político da copropriedade:** participações monetárias não concedem automaticamente veto, controle militar, domínio territorial ou capacidade de suspender tráfego. A aprovação expressa da possibilidade de **embargo dirigido a civilização específica** é registrada em F-063, mas seu exercício, quórum e autoridade diplomática estão em estudo. Não criar poder automático de veto de um cotitular com participação ínfima.

**Abertos:** eventual compra/cessão/confisco/transferência de quota; morte ou incorporação de civilização; investimentos durante conflito; autorização de terceiros para trabalho na aresta; dois investidores simultâneos e arbitragem determinística; função w_e ligada à infraestrutura, normalização da impedância e aplicação de embargo. A copropriedade econômica capital-proporcional não resolve nenhuma dessas questões automaticamente.

### F-063 — Embargo comercial seletivo numa aresta compartilhada [CAPACIDADE RATIFICADA; GOVERNO E EXECUÇÃO ABERTOS]

**Decisão expressa do usuário:** a arquitetura da copropriedade deve **permitir embargo contra uma civilização específica**, sem demandar proprietário único ou reorganizar todo o mercado para introduzir essa opção mais tarde.

**Separação de responsabilidades:**
1. Copropriedade econômica da F-062 governa **quotas de capital e direito à tarifa**, não confere, por si só, competência diplomática sobre quem transita.
2. Embargo comercial sobre aresta e contra civilização c define restrição de elegibilidade de **fluxos abstratos comerciais atribuídos a c**. Durante o ciclo de embargo, o water-filling F-049/F-054/F-057 não pode usar essa aresta para produtos de c, mas pode utilizá-la para outras civilizações elegíveis. A distinção é por civilização da **produção**, não carteira do comprador (inexistente).
3. O clearing global é recalculado simultaneamente com o novo conjunto de caminhos elegíveis, sem congelar preços nem congestionamento. A produção de c precisa continuar **100% comercializada**, usando mercado local e outras rotas permitidas, sempre que existirem.
4. Embargo não apaga quota de capital nem automaticamente confisca direito de receita tarifária sobre fluxos de outras civilizações. Bloqueio militar, deslocamento físico de trabalhadores, imigração, confisco e embargo de importação são decisões independentes; a aprovação presente abrange **capacidade de embargo comercial**, não essas extensões.
5. Aresta compartilhada pode impor ou revogar embargo apenas por **autoridade/governança explicitamente deliberada**; nenhum cotitular recebe automaticamente veto unilateral por participação mínima. Não presumir maioria simples, unanimidade, quórum numérico nem poder territorial sem aprovação.
6. **Conflito com venda universal:** se o embargo retirar todos os mercados alcançáveis com D>0 de uma produção positiva, o contrato de F-041 deixa de ser solucionável sem regra adicional. Não resolver descartando estoque, inventando consumidor, quebrando embargo ou reduzindo a venda a menos de 100%. Bloqueios que provoquem isolamento total exigem uma **política de contingência deliberada** antes de sua habilitação na implementação.
7. Embargo é uma política por identidade de **aresta e civilização-alvo**, com vigência/ciclo lógico definido; matriz densa de todas as civilizações×arestas não é exigida. Uma representação esparsa de alvos ativos é opção futura, não estrutura ratificada. Restrição em ambos os sentidos é comportamento candidato, mas direcionalidade e embargo de importação não foram definidos.

**Laboratório conceitual mínimo:** aresta melhorada com titulares A=60%, B=30%, C=10%. Caso uma autoridade competente decida embargar D, fluxos comerciais de D ficam inelegíveis nessa aresta; A/B/C e outros não embargados continuam elegíveis, congestionamento é recalculado, quotas de investimento permanecem 60/30/10. A mera participação de C=10% NÃO demonstra poder de C para embargar D sozinha.

**Gates obrigatórios:** determinar poder de embargo (votação ponderada por quotas, diplomacia territorial ou contrato), eventual embargo de coproprietário, se embargo é bidirecional, efeitos sobre direitos de tarifa, como evitar isolamento total incompatível com venda universal, duração/revogação, desempates de votação e replay determinístico; não inferir decisões militares.

**Status:** coexistência de copropriedade com capacidade de embargo seletivo **RATIFICADA**; mecânica de decisão, quórum, ciclo, alvo efetivo no trânsito, contingência e governo diplomático seguem **ABERTOS**. Não implementar bloqueio imediato no kernel com premissas inventadas.

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
- **2026-11-07 — nona deliberação:** F-024 especifica infraestrutura abstrata baseada em investimento com retornos decrescentes; F-025 documenta nível contínuo e distingue eficiência/capacidade; F-026 distingue carga logística de quantidade e preço (não impõe coeficientes individuais arbitrários); F-027 mantém funções de saturação e tarifa abertas. Nenhuma equação ou parâmetro foi ratificado.

- **2026-11-07 — estudo da exigência especial por mercadoria:** F-028 propôs separar carga física e exigência de proteção; rejeitada expressamente pelo usuário para o núcleo inicial por exigir índices específicos arbitrários. Preservada uma única equação logística.

- **2026-11-07 — décima deliberação:** adotada filosofia de uma única dimensão logística, sem índices de proteção/segurança por mercadoria e sem coeficientes de balanceamento item a item. F-028 rejeitada para a fase inicial. F-026 restringe fatores `a_r` a conversões justificáveis ou normalização unitária; fórmulas e calibração continuam abertas.

- **2026-11-07 — estudo de saturação mínima:** F-029 proposta com impedância linear na taxa `B/K`, multiplicador de investimento `U=1+log₂(1+I/I₀)`, eficiência e capacidade derivadas do mesmo nível e exemplo de rotas concorrentes. Fórmulas, parâmetros e convergência não ratificados. Foi ressaltada a necessidade de normalizar fluxo por duração do ciclo econômico.

- **2026-11-07 — revisão da saturação pela infraestrutura natural:** F-030 ratificada: nós e arestas naturais já são infraestrutura funcional e entram em todos os cálculos, inclusive quando investimento=0; necessidade de modelar capacidade dos nós para carga/descarga, não só arestas. F-031 propõe penalização convexa `1+ρ^p`, com expoente global e teste da praia/cargueiro; versão linear F-029 deixa de ser candidata principal. Fórmulas e p não ratificados.

- **2026-11-07 — experimento do gargalo natural:** F-032 documenta duas rotas com uma praia natural de capacidade baixa e um acesso alternativo, testando fluxo de 6 unidades, função convexa p=4 e níveis de investimento. Detectada ambiguidade crítica entre interpretar C(B) como impedância marginal (water-filling iguala C) e como custo médio (ótimo requer C+B C'); recomendado testar a interpretação marginal e integral de esforço H(B)=∫C(z)dz, sem ratificação prematura.

- **2026-11-07 — estudo de três tipos naturais de conexão:** F-033 proposta: mesma equação para terra↔terra, terra↔mar e mar↔mar, com elegibilidade de navegação e transferência de meio como possíveis distinções qualitativas mínimas. Evitar contagem de custo/capacidade por tile da costa ou por tile oceânico e evitar investimentos/privatização involuntários em mar aberto. Nenhuma regra adicional ratificada.

- **2026-11-07 — simplificação das travessias marítimas:** F-033 reformulada por decisão de eliminar embarcações/tecnologia naval como controles separados: a aresta terra↔mar representa implicitamente meios de embarque, desembarque e transporte necessários. A rota usa as mesmas equações de todas as arestas, sem custo de transição duplicado nos nós. Permanecem abertos geometria/capacidade costeira e a atribuição de investimentos em mar aberto.

- **2026-11-07 — investimento individual por aresta:** F-034 ratificada: toda infraestrutura logística melhorável pertence à aresta, não ao nó; investir numa ligação não melhora outras mesmo se compartilham extremo. Trabalhador pode construir a partir de qualquer nó extremo, nunca enquanto em trânsito; execução respeita orçamento temporal e progresso físico. Hipóteses anteriores de infraestrutura portuária em nó foram explicitamente superadas. Categoria de investimento produtivo no nó (F-006) não foi redefinida nesta deliberação.

- **2026-11-07 — décima primeira deliberação:** F-035 ratificada como melhoria contínua por trabalho e desembolso monetário efetivos, sem marcos de obra concluída; capital incorporado é progresso persistente, e efeitos entram em vigor no tempo lógico correto. F-036 aberta para múltiplos trabalhadores/contas concorrentes e taxa de execução; fórmula de taxa e critérios de empate ainda não ratificados.

- **2026-11-07 — taxa-base de execução de infraestrutura:** F-037 ratificada com 1 unidade monetária de capital incorporável por turno completo de trabalhador padrão, com frações temporais e condicionada a caixa. Diferenciação futura pela economia do local de pertencimento (também afeta demanda) e, se houver, educação/fatores sociais; sem definir multiplicadores ou sistema educacional agora. F-036 concorrência segue aberta.

- **2026-11-07 — estudo de desenvolvimento e produtividade por consumo:** F-038 proposta como alternativa ao desenvolvimento medido por renda: consumo real efetivamente atendido por trabalhador pertencente à localidade, com variedade emergindo de utilidade marginal decrescente, sem pesos arbitrários por item. Discussão de normalização, categorias e circularidade da demanda; F-037 permanece ratificada e F-038 aberta.

- **2026-11-07 — ratificação de F-038 e exclusão de demanda artificial:** F-038 aprovada no princípio de desenvolvimento pelo consumo real atendido por trabalhador com diversidade endógena, sem fixar `Σ ln(1+c)` nem outros pesos; F-039 ratificada: sem estoques e sem demanda artificial para retenção, sem venda garantida do excedente. F-040 propõe registrar atendimento por produto/localidade a partir de vendas realmente liquidadas, sem dupla contagem, pendente de decisão do sujeito/financiamento da compra F-022.

- **2026-11-07 — correção do princípio de absorção e matemática de preços:** F-041 ratificada: toda a produção encontra mercado, venda 100%, demanda de referência 1 por trabalhador/produto (k futuro). F-039/F-040 corrigidas: nada de inventário ou parte não vendida. Identificada falha da função x^-2 quando oferta supera demanda: R=D²/Q decresce, contrariando objetivo. F-042 propõe P=2/(1+x) para receita total sempre crescente e ganho marginal decrescente, ainda não ratificada; financiamento F-022 pendente.

- **2026-11-07 — função única de preço aprovada e primeiro teste de clearing:** F-043 ratifica P=(Q/D)^(-1/2), R=√(QD), R'=P/2 e R''<0 para toda mercadoria/mercado; F-042 racional vira alternativa histórica. F-044 verifica alocação proporcional à demanda sob acesso igual e objetivo de maximizar receita agregada. F-045 abre a incompatibilidade potencial entre venda universal, receita calculada e saldos compradores finitos; impedância não monetária não pode ser subtraída arbitrariamente da receita monetária.

- **2026-11-07 — correção do perímetro financeiro e estudo da expectativa de infraestrutura:** F-046 ratificada: compradores não têm carteira, caixa nem restrição orçamentária; o poder de compra consumidor é indeterminado e pode realocar gastos em bens invisíveis. Toda venda gera receita ao tesouro produtor; a moeda do setor visível é aberta. F-022/F-023/F-040/F-043/F-045 revistas para não exigir conciliação de contas compradoras inexistentes. F-047 avalia congelar impedância por aresta durante o ciclo econômico usando volume esperado, com atualização entre ciclos, comparando a equilíbrio endógeno; alerta para oscilação temporal de rotas e necessidade de teste antes de ratificar.

- **2026-11-07 — tesouro civilizacional e clearing simultâneo:** F-048 ratificada: a receita das vendas pertence à civilização produtora, não ao trabalhador nem ao nó; investimento logístico é pago pelo tesouro da civilização, mantendo trabalhadores apenas como agentes físicos. F-049 ratificada: resolver simultaneamente carga por aresta, congestionamento e alocação por destino/item no ciclo; expectativa fixa defasada fica somente como alternativa futura se desempenho exigir. F-047 marcada como hipótese histórica superada. Formulação exata do solver, escala da impedância e tarifa permanecem abertas.

- **2026-11-07 — proposta F-050 de integração preço/impedância:** testada atratividade A=P/L, com L=Σ C_e e P=√(D/Q), sem subtrair moeda de pontos logísticos nem reintroduzir carteiras. Sem congestionamento: D_A=4,D_B=1,Q=10,L_A=1,L_B=2 ⇒ q_A≈9,4118,q_B≈0,5882; com C_A=1+(q_A/5)^4 e C_B=2[1+(q_B/10)^4] ⇒ q_A≈6,12565 e q_B≈3,87435. Modelo em estudo, não ratificado. F-051 abre a distinção entre alocação global de mercados e maximização da receita de cada civilização, assim como risco de fluxos mínimos para todos os mercados devido a P→∞ quando Q→0.

- **2026-11-07 — laboratório 2 civilizações/2 mercados e reformulação numérica:** F-052 registra dois experimentos com receitas dos tesouros separadas e gargalos compartilhados. No acesso simétrico com produção 6+4, demanda 4+1 e congestionamento convexo, o clearing candidato P/L alocou ≈6,247/3,753 por mercado; o desempate proporcional produziu receitas ≈4,162/2,774. No acesso assimétrico, origens se especializaram com receitas ≈4,899/2, mas uma civilização teria incentivo a alterar rotas se buscasse somente receita monetária bruta. F-053 propõe minimizar G=L/P=L√(Q/D), evitando a singularidade explícita de P em Q→0, e demonstra solução analítica Q_n∝D_n/L_n² sem congestionamento. F-050/051/053 e decisão de alocador global continuam EM ABERTO.

- **2026-11-07 — alocador global ratificado:** F-054 fecha a escolha por um mercado comercial automático global, com receitas de vendas creditadas exclusivamente às civilizações responsáveis, sem carteiras dos consumidores nem ordens de venda individuais. O critério particular P/L, o solver e sua forma de representação da rede ainda dependem de validação, especialmente para mercados no mesmo nó da produção.

- **2026-11-07 — estudo de escalabilidade por arcos e venda local:** F-055 propõe fluxo transitório em arestas por civilização/mercadoria, conservação nodal e agregação de congestionamento em ambos os sentidos, evitando enumerar todas as rotas. F-056 identifica a singularidade P/L no mercado do próprio nó (L=0) e propõe testar a atenuação P/(1+L), com referência comum e sem tarifa, porto ou custo de nó; ambos permanecem não ratificados.

- **2026-11-07 — ratificação de fluxos e mercado local; estudo do solver:** F-055 e F-056 foram aprovadas: fluxos comerciais transitórios em arcos por civilização/item, conservação nodal, e atratividade A=P/(1+L) com mercado local L=0 (normalização da impedância pendente). F-050 e F-053 passam a estudos históricos da hipótese P/L sem correção local. F-057 propõe contrato de desigualdade variacional e verificação por gap de equilíbrio, evitando a atualização gulosa oscilatória; caso escalar local/remoto resolvido por bisseção com q_remoto≈0,5881293 de 10, congestionamento ilustrativo p=4. Solver e normalização continuam não ratificados.

- **2026-11-07 — F-057 aprovada como contrato, F-058 laboratório e F-059 gate:** conservação da produção, clearing simultâneo, determinismo e gap de equilíbrio passam a contrato canônico, mas algoritmo/tolerâncias em aberto. F-058 executou protótipo Python/SciPy extragradiente em diamante A-B-C-D com duas civilizações e duas mercadorias, 20 unidades vendidas, cargas 1,33415/0,79494 em corredores, Q 6,93545/3,06455 por mercado/item, receita 6,20007 para cada civilização e gap ≈5,63e-10. O teste detectou **não convergência** com passo 0,04 a partir de início extremo superior e **não unicidade da decomposição** por produto/rota apesar dos mesmos resultados agregados. F-059 abre desempate canônico com relevância para futuras tarifas e métodos numéricos robustos. Nenhuma homologação de benchmark Goldberg ou solver de produção foi declarada.

- **2026-11-07 — F-059 ratificada e alternativa de tarifa F-060 estudada:** tarifas monetárias, quando existirem, integram o equilíbrio comercial ANTES do desempate; decomposições que alterem transferências entre tesouros não são empates meramente técnicos. F-060 documenta risco de tarifas nominais fixas por carga sob P=√(D/Q) e venda integral; propõe rateio da receita bruta V entre produtora e proprietários de arestas externas por pesos tarifários w_e≥0, com V_liquida=V/(1+Σw) e tarifa_e=V w_e/(1+Σw), garantindo parcelas não negativas e soma exatamente V. Para o diamante F-058, duas decomposições fisicamente equivalentes gerariam tarifas Beta→Alfa ≈0,05389 e ≈0,06989 se tarifa de 10% fosse adicionada APÓS o equilíbrio, exemplificando erro financeiro. Natureza/função das tarifas e regra final de desempate NÃO ratificadas.

- **2026-11-07 — tarifação proporcional ratificada, função de intensidade e direitos estudados:** F-060 ratificada no princípio de cobrança proporcional à receita da venda e no rateio V_produtora=V/(1+Σw), V_titular_e=Vw_e/(1+Σw), sem débito em carteira compradora; fórmula w(I) ainda aberta. F-061 confrontou a candidata direta w=1−1/U, que pode tornar uma rota menos atraente já no primeiro investimento, e propôs w=1/(1+c_livre)−1/(1+c_natural). Em aresta única, acesso de não titulares melhora ao mesmo tempo que cresce receita tarifária; em rotas longas o produto (1+L)(1+W) ainda pode piorar o acesso após uma melhoria (exemplo 13→14), com implicações para a fórmula de atratividade. F-062 abre a deliberação de titularidade e direito de investimento (primeiro investidor, copropriedade por capital ou autoridade territorial). Nenhum peso de tarifa ou regime de propriedade foi ratificado.

- **2026-11-07 — copropriedade proporcional e possibilidade de embargo seletivo:** F-062 ratificada: capital incorporado por civilização I[e,c] determina quota s[e,c]=I[e,c]/ΣI, e tarifas proporcionais F-060 são recebidas por outras coproprietárias segundo w_e s[e,j], sem cobrar a quota do próprio vendedor. A infraestrutura é fisicamente compartilhada, sem monopólio do primeiro investidor. F-063 ratifica a **capacidade futura de embargo comercial contra civilização específica em aresta compartilhada**, sem confundir quotas econômicas com voto de bloqueio; autoridade, quórum, direcionalidade, duração, embargo de coproprietários e caso sem mercado acessível D>0 (contradição com venda universal) permanecem abertos. Nenhuma modificação no código ou merge.
