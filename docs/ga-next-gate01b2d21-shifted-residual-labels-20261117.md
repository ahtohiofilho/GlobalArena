# GA-Next — Gate 01B.2D-21: rótulos exatos deslocados no solver residual

**17/11/2026 · implementação C# experimental · PR Draft empilhado sobre #413 · sem merge na main**

## Objetivo de desempenho

Os Gates 19 e 20 confirmaram que a busca de caminhos do fluxo residual é o gargalo de tempo e alocação gerenciada nos mercados competitivos. O Gate 20 introduziu a fila indexada exata, que reduziu tempo de busca em quatro escalas, mas não reduziu significativamente as alocações cumulativas de BigInteger.

O Gate 21 altera **apenas a representação matemática intermediária** durante as relaxações de Dijkstra, sem modificar as regras da economia nem aceitar perda de precisão.

## Identidade algébrica obrigatória

O algoritmo residual usa potencial de nó \(p[v]\), distância reduzida \(d[v]\) e custo de aresta \(c(u,v)\). Na referência, a relaxação computa o custo reduzido \(r=c+p[u]-p[v]\), exige \(r\geq 0\), e compara \(d[u]+r\) com \(d[v]\).

Na variante, guarda simultaneamente \(h[v]=d[v]+p[v]\). Para arestas positivas da rede residual, exige **a mesma** invariante \(p[u]+c\geq p[v]\) usando BigInteger exato. Em seguida avalia \(h[u]+c\) e compara com \(h[v]\). Apenas quando o novo candidato é **estritamente menor**, converte para a chave reduzida original com \(d[v]=h[v]-p[v]\). Assim, a fila indexada recebe exatamente os mesmos nós, as mesmas prioridades e a mesma ordem de desempate.

Em particular, não há arredondamento, ponto flutuante, conversão para `Int128`, interrupção precoce ou teto da demanda. A verificação da invariante de custo reduzido é feita também nas arestas que não melhoram a distância. As distâncias deslocadas são zeradas a cada augmentation e nunca são partilhadas entre commodities, turnos ou civilizações.

`useShiftedLabels:true` ativa o novo mecanismo no solver experimental; `false` conserva a expressão original como oráculo interno A/B. O parâmetro é `internal`, não afeta a API do jogo. `reuseSearchWorkspace` e `useIndexedHeap` mantêm seus contratos independentes.

## Matriz de testes

1. Teste unitário com custos de transporte exatos maiores que 240 bits, empates, inacessibilidade, arcos residuais reversos, três níveis de segmentação e quatro combinações dos modos de fila e workspace. Todo o resultado deve coincidir com a referência original, e falhas de orçamento/inacessibilidade continuam fechadas.
2. Teste Goldberg com quatro escalas físicas reais de produtores e compradores: 112 tiles/8 produtores/104 mercados; 208/16/192; 320/24/288; 416/32/384. Cada produtor real comercializa um milhão de micro-bens e gera demanda como trabalhador; sem mercados, estoque ou compradores fictícios.
3. A/B calcula a mesma alocação com fila indexada em ambos os caminhos (referência usa `useShiftedLabels:false`). Verifica resultados idênticos na ordem de entregas, rotas, mercado comprador, bens disponíveis/alocados, frete, cargas por aresta, potenciais integrados e segmentados BigInteger, número de augmentations, StateHash e NoClosedCycle.
4. Perfis opcionais de `Stopwatch` e `GC.GetAllocatedBytesForCurrentThread` no fluxo residual capturam tempo de parede e alocação gerenciada cumulativa. Windows e Ubuntu publicam quatro arquivos JSON cada, resumos via PowerShell e artifacts; CI de cinco workflows precisa estar verde.

## Interpretação e critérios

Cada cenário e runner fornece **uma observação**, não uma média com intervalo de confiança, CPU exclusiva, pico de memória, throughput garantido ou prova de mundos com milhares de nós. O benchmark pode evidenciar regressão: a prova A/B de correção é obrigatória e independente de ganho. Se o custo de duas adições por aresta mais manutenção da matriz deslocada não diminuir as alocações ou o tempo real, **não** defender esta variante como otimização comprovada. As regras físicas e econômicas permanecerão intactas em qualquer caso.

**Governança:** PR experimental Draft sobre #413; nenhum merge ou publicação de ciclo fechado. A adoção como caminho padrão requer CI completa e observação consistente. A via anterior ficará disponível para comparação.
