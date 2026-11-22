# GA-Next R165 — progresso fracionário real nas arestas

## Trilha natural e regra de tempo

A R164 recebia frações de viagem prontas. A R165 produz essas frações a partir de um **estado explícito de progresso em uma aresta real**, utilizando o `PhysicalTacticalWorkerMoveEventExecutor.CheckEdge` que já valida a adjacência da malha Goldberg fina e as restrições da superfície física assinada.

O tempo de travessia é expresso em microunidades de turno, fornecido por uma política física verificável (integração posterior). A R165 **não fixa 1,4 turno como custo universal**; essa duração é apenas um teste de exemplo. As arestas continuam sendo referências discretas de distância e não introduzimos quilômetros ou medidas planetárias ocultas.

`FoundationPhysicalEdgeTravelProgress` registra trabalhador, dono, nó de partida, destino, aresta exata e tempo restante. A cada turno, `Advance`:
1. valida trabalhador, propriedade, origem, aresta canônica e tempo restante;
2. consome no máximo 1.000.000 microunidades de turno, atribuindo o período de viagem ao nó de partida;
3. se o percurso ainda não acabou, conserva a origem como posição e avança somente o contador de progresso;
4. se chega antes do fim do turno, disponibiliza a fração restante no nó de chegada para trabalho de ocupação produtiva compatível (ou atividade `Other` sem esforço produtivo);
5. devolve as **fatias da R164** prontas para cálculo de presença, demanda e futuros nascimentos, sem duplicar o trabalhador ou criar mercado em uma aresta.

Uma travessia de 1,4 turnos produz um primeiro turno de 100% de viagem e um segundo turno de 40% de viagem + 60% de tempo de trabalho no destino. No teste, os 60% também alimentam a demanda de destino via R164. Uma viagem de 0,3 turno produz 70% de tempo disponível no destino no próprio turno.

## Fora do escopo

Não implementamos outro executor de eventos: o projetor reutiliza o **verificador canônico existente**. Por enquanto a saída é **pré-commit**. O progresso ainda não é persistido no `WorldState` nem no diário, e não há ordens de destino múltiplo, encadeamento de várias arestas dentro do mesmo turno ou atualização automática de preços e receita.

O próximo passo é associar **durações explícitas derivadas de infraestrutura e políticas de deslocamento** ao progresso temporal e integrá-lo ao ciclo autoritativo, preservando replay e determinismo. A R164 continua responsável pela agregação econômica do tempo, e a R162/R163 não voltam a determinar os nascimentos pela cidade de residência.

## Gate

Testes garantem travessia assinada real, esforço e consumo de 40%/60%, uso direto da R164, conservação por turno, replay idêntico, rejeição de aresta forjada e ausência de mutação do estado original. Não é necessária abertura visual do Godot.
