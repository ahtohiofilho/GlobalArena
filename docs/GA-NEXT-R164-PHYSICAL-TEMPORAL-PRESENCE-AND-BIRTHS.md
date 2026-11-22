# GA-Next R164 — tempo físico, esforço, demanda e nascimento por presença

## Contrato demográfico atualizado

A **cidade de origem/natal** e a posição física atual são fatos distintos. O histórico de origem não determina o local de consumo nem o peso dos nascimentos. A R163 mantém, por enquanto, seu ledger de cidades e vínculos de residência para compatibilidade histórica, mas **não deve mais ser a fonte autoritativa dos pesos de nascimento** após a integração da R164. A futura persistência do nascimento deverá registrar permanentemente o **nó natal** e, somente se existir uma cidade formal naquele nó e daquela civilização no instante do evento, também o **ID da cidade natal**. Trabalhadores podem nascer fora de cidades.

O número mundial de nascimentos por turno continua sendo exatamente o número de civilizações sobreviventes. Cada sorteio escolhe um par **civilização+nó** proporcional ao tempo de presença dos trabalhadores naquele nó, incluindo trabalhadores em deslocamento ou ocupação temporária. Após um nascimento, o peso do local selecionado aumenta em uma unidade equivalente de presença para o sorteio seguinte. O crescimento não é reservado por civilização.

## Contrato temporal

Cada trabalhador fornece exatamente **1.000.000 unidades de tempo por turno**, distribuídas em frações inteiras entre nós. Estando numa aresta, a fração de viagem é atribuída ao nó de partida; quando chega ao próximo nó, a fração restante pertence ao destino.

No exemplo de travessia que dura 1,4 turnos: no primeiro turno há 100% viagem (só no nó de partida); no segundo, **400.000 micro de viagem** no nó de partida e **600.000 micro no destino**, com **até 600.000 de esforço produtivo** no destino. A demanda acompanha esses tempos: parte no nó anterior e parte no destino. Não há mercado físico dentro de arestas.

O tipo de atividade em cada fatia define o perfil de demanda explícito (viagem vs. produção, etc.). Para cada pessoa/nó/mercadoria, as intensidades são acumuladas com inteiros de alta precisão antes do arredondamento final para microunidades; evitamos duplicar a pessoa ou demandar bens fictícios. A conservação temporal é rígida; a quantização monetária não acrescenta demanda artificial. Os resultados são **projeções ainda não liquidadas**, não novos lançamentos comerciais.

## Implementação R164

`FoundationPhysicalPresenceDemographicProjector` oferece:
- `ArrivalWithinTurn`: forma explícita de dividir uma chegada dentro do turno entre viagem e esforço disponível;
- `Project`: recebe **fatias temporais verificáveis** e trabalhadores reais de um mundo vinculado, valida a conservação integral de tempo por trabalhador, agrega presença e esforço por par civilização+nó, e distribui demanda por mercadoria e atividade;
- `PlanBirths`: sorteia os novos trabalhadores em nós com presença real, inclusive sem cidade, usando SHA-256 determinístico com hash do mundo, resumo canônico das fatias, turno e ordem do sorteio. Entrega `NatalNode` e `NatalCity?` (sem inventar cidade).

As avaliações não alteram o censo real, o ledger R163, os impostos, o caixa, os fretes ou os turnos. A R162 e seu planejador histórico de ponderação por residência ficam preservados apenas para regressão e compatibilidade até substituir os pontos de integração pela nova regra.

## Limites deliberados

A R164 ainda não implementa **movimentação contínua autorizada por aresta**, passagens por múltiplas arestas no mesmo turno, custos/tempo de viagem a partir da infraestrutura, nem a divisão real da produção e demanda liquidada pelo mercado. A origem das fatias temporais deve vir do futuro integrador físico que verificará rotas, adjacência, progresso e duração; **o helper de chegada não verifica adjacência nem duração de aresta**, apenas divide um tempo que outro sistema já validou. Da mesma forma, ainda não há persistência de `NatalNode` e `NatalCity` por indivíduo — os fatos estão definidos nos planos de nascimento, não no `WorldState` dos trabalhadores existentes.

A integração natural seguinte é adicionar o executor físico do tempo em movimento e conectar suas fatias à economia e aos nascimentos reais. Não devemos mudar prematuramente os mecanismos autoritativos de preço, logística, turno ou journal.
