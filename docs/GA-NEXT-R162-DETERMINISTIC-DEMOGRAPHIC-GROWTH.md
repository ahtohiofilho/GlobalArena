# GA-Next R162 — planejamento demográfico determinístico

## Contrato econômico-demográfico aprovado

O jogo deverá começar com **um trabalhador por civilização**. Em cada turno, a quantidade **mundial** de novos trabalhadores será igual ao número de civilizações sobreviventes (não um nascimento garantido dentro de cada uma). Cada trabalhador novo será sorteado individualmente entre **todas as cidades das civilizações sobreviventes**, proporcionalmente à população atual da cidade. Assim, cidades de uma civilização podem receber múltiplos novos habitantes em um turno, enquanto outras civilizações não recebem nenhum.

O sorteio é reponderado imediatamente após cada nascimento do mesmo turno: se a cidade foi escolhida, sua população para o próximo sorteio cresce em uma unidade. O custo é proporcional ao número de cidades vezes o número de civilizações sobreviventes neste primeiro protótipo; não há necessidade atual de estruturas probabilísticas mais sofisticadas.

## Implementação entregue na R162

`FoundationDemographicGrowthPlanner.Plan` produz apenas um **plano imutável**, com identidade canônica do mundo, turno, sequência exata de cidades escolhidas e incremento agregado de cada cidade. O sorteio usa SHA-256 a partir do hash canônico, do turno, do índice de sorteio e do contador de rejeição; usa leitura inteira fixa e rejeição para evitar viés de módulo. Os dados de entrada são ordenados por identificador de cidade e civilização, e toda execução com o mesmo estado e turno é reproduzível.

Os locais são identificados explicitamente por `CityId`, `Owner` e `PhysicalTacticalTileId`; **CityId não é substituído por um StrategicCellId ou pelo ID de tile**. A população é informada explicitamente, com verificação provisória de correspondência com trabalhadores co-localizados na cidade. Essa verificação funciona para a abertura concentrada atual e é intencionalmente conservadora: **não há ainda ledger separado de residência** para trabalhadores viajando ou vivendo temporariamente fora de uma cidade. Uma fase posterior deverá tratar residência e localização sem presumir equivalência. Não existe neste momento entidade persistida de cidade autorizada, portanto esse catálogo é uma entrada de simulação, **não uma materialização automática de cidades**.

O número inicial de 1 trabalhador é validado nos testes usando o parâmetro existente de fundação, sem modificar prematuramente o padrão da apresentação experimental que sustenta os testes atuais. A regra de crescimento é proposta de núcleo, não foi conectada à transição real de turno nem ao journal. O planejador exclui de sorteios e orçamento as civilizações fora do conjunto explícito de sobreviventes e rejeita localidades com população nula, IDs duplicados, atributos incompatíveis e contagens populacionais incorretas.

## Próximo gate natural

Após homologar a R162, o passo seguinte é constituir a **fonte autoritativa de cidades e residência**, com integração posterior ao nascimento real e ao journal. Isso permitirá crescimento em cidades de uma mesma civilização sem que a presença temporária de tropas ou viajantes altere os pesos demográficos. A decisão econômica da primeira ocupação do novo trabalhador permanece uma responsabilidade distinta, apoiada nas avaliações da R161. Não há crescimento artificial exponencial, correção de tamanho de cidade por penalidade imposta, nem redistribuição obrigatória por civilização.
