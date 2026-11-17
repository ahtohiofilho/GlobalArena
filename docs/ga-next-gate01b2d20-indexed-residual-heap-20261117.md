# GA-Next — Gate 01B.2D-20: fila indexada exata para o caminho residual

**17/11/2026 · Pull Request Draft sobre Gate 19 / PR #412 · sem merge na main · economia não liquidada**

## Diagnóstico que motiva a mudança

O Gate 19 mediu, no cenário de 416 tiles conectados, 32 produtores reais e 384 mercados, aproximadamente 2,28 s (Ubuntu) / 2,75 s (Windows) dentro da busca residual. As alocações gerenciadas cumulativas da thread superaram 2,5 GiB nessa fase. Essas são medições de parede/fluxo de alocações, não tempo CPU exclusivo ou heap residente. Os custos de preparação e auditoria física foram pequenos em comparação.

A busca residual original usa uma PriorityQueue da BCL com pares (distância BigInteger, ID do nó). Cada relaxação estritamente melhor enfileira mais uma entrada; as entradas antigas do mesmo nó permanecem até serem retiradas e descartadas por teste de distância obsoleta. À medida que a rede residual fica densa, entradas inúteis provocam mais operações e crescimento da fila.

## Implementação experimental

Foi criada IndexedResidualMinHeap, uma fila de mínimo indexada por vértice, com vetores de nós e posições reutilizados dentro de **um** Solve. Uma relaxação estritamente melhor atualiza a distância BigInteger exata e reduz a chave do mesmo nó **in-place**. A fila mantém no máximo uma entrada viva por vértice, em vez de enfileirar versões obsoletas. Comparação de prioridade: (menor distância exata, menor ID do vértice), reproduzindo o desempate anterior. O algoritmo não descarta propostas econômicas e não modifica a decisão de alocação, rotas ou preços.

O parâmetro internal useIndexedHeap (padrão true) ativa o novo mecanismo. A implementação antiga continua disponível com useIndexedHeap:false como oráculo A/B de regressão. O indicador reuseSearchWorkspace mantém sua semântica independente. O caminho público chama a versão exata indexada, sem expor opção ao jogador.

Os custos das arestas, o potencial residual, as capacidades, o sentido das arestas de retorno, a política de empate em relaxação estrita, os cálculos BigInteger, o orçamento de augmentations e a segmentação de demanda permanecem idênticos. Se o orçamento acaba, não se devolve candidato parcial. Nenhum estoque fictício, carteira, preço liquidado ou ciclo autoritativo é criado.

## Matriz de verificação

A regressão menor exercita realocação de fornecedor via arcos reversos, custos empatados, 1/3/9 segmentos, variantes com e sem workspace reutilizado, e rejeição fechada de mercado inalcançável e orçamento insuficiente.

O teste Goldberg A/B mede as duas filas e compara exatamente:

| Tiles conectados | Produtores físicos | Mercados reais | Pares possíveis |
|---:|---:|---:|---:|
| 112 | 8 | 104 | 832 |
| 208 | 16 | 192 | 3.072 |
| 320 | 24 | 288 | 6.912 |
| 416 | 32 | 384 | 12.288 |

Todos os cenários usam a mesma malha física, o mesmo conjunto de trabalhadores e a mesma demanda por atividade, com 4 segmentos por mercado. Exigências obrigatórias: número de augmentations, mercado de cada comprador, quantidades exatas por entrega, rota ordenada, cargas reais por aresta, potencial BigInteger segmentado e integrado, total de bens conservados, StateHash e NoClosedCycle **idênticos** à versão BCL. Nenhuma regra de avaliação econômica muda.

A CI executa os testes em Ubuntu e Windows, arquiva quatro relatórios JSON por runner com schema ga-next.indexed-residual-heap-ab.v1, valida as métricas e os fatos econômicos, e publica resumo de tempo e memória acumulada. Suítes completas e outros quatro workflows são necessários para homologação.

## Interpretação de resultados

Os tempos são amostras individuais de **wall clock**; alocação é soma de bytes gerenciados produzidos na thread, não RAM máxima. A comparação interna pode ser afetada por JIT, GC e agendamento; não oferece prova estatística de SLA de turno ou de escalabilidade em milhares de nós.

O Gate 20 será considerado tecnicamente equivalente apenas se a prova A/B passar. Se a nova fila não reduzir o custo residual com consistência, a evidência será documentada e a implementação não deverá ser considerada uma melhoria de throughput; pode-se manter a fila original como padrão até a próxima hipótese. É proibido inventar salto de preços, reduzir artificialmente demanda ou eliminar auditorias para obter uma porcentagem melhor.

**Governança: manter Draft, sem merge; melhoria do núcleo sujeita a CI do HEAD.**
