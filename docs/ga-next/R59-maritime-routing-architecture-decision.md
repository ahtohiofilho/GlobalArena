# GA-Next R59 — revisão conceitual da busca multimodal (ADR experimental)

**Status:** proposta de arquitetura; sem alteração no kernel de liquidação ou na `main`.
**Base:** R55–R58, branch empilhada sobre R58. Não confundir teste de laboratório com motor de comércio implantado.
**Decisão preliminar:** parar micro-otimizações da estratégia de enumeração de todos os pares de terminais e avaliar duas formulações estruturais, mantendo R55 como oráculo de exatidão.

## 1. O que foi observado

- R55: entre dois mercados terrestres reais (tiles 16 e 27), a alternativa natural terrestre custou **35.024** contra **60.985** pela opção de uma travessia marítima. Desenvolver todas as costas de forma extrema (I/B=256 em cada uma, ensaio de estresse) produziu alternativa marítima **30.679**. Não é parâmetro de balanceamento.
- R56: 657 Dijkstra marítimos → 329, sem alterar custo ótimo nem as duas travessias costeiras vencedoras; 1.187.199 tiles visitados → 594.503.
- R57: cache explícito da distância no mar, limitado a 4 milhões de entradas, passou a reutilizar resultados de 329 origens entre configurações de investimento terra/costa; consultas repetidas precisam de zero novos Dijkstra marítimos. Isso **não** elimina reconstrução de grafos nem enumeração dos pares.
- R58: medição do runner Linux: em **1.442 tiles**, consulta após investimento sem cache 59,098 ms e com cache quente 44,243 ms; em **3.242 tiles**, 166,096 ms contra 107,786 ms. Respectivamente ~25,1% e ~35,1% a menos na consulta quente. Testes preservam IDs dos dois terminais e custo ótimo. Cache retém cerca de 2,33 e 8,14 MiB de `ulong` apenas, respectivamente. O wall-clock isolado inclui JIT/ruído: não extrapolar linearmente nem inferir orçamento de um turno completo.

## 2. Premissas obrigatórias, não negociáveis

1. Grafo físico Goldberg e **uma única aresta** para cada travessia. Arestas terra–terra e terra–mar aceitam capital monetário individual e recebem o mesmo modelo de retorno decrescente; não criar objeto `port` separado.
2. Mar–mar é sempre natural: sem capital individual, título, tarifa ou congestionamento, mas **C0 positivo**. A escolha do trajeto pode ficar mais barata conforme a qualidade das **duas arestas costeiras usadas**, não dos portos próximos.
3. A tarifa de terceiros, a isenção do proprietário, o congestionamento de arestas finitas e as decisões de mercado devem continuar corretos. **Nenhuma comparação só de C0 físico é autorização para mexer na liquidação autoritativa.**
4. A qualidade de costa usa **somente o investimento já efetivamente debitado e congelado para o ciclo**. Uma venda ou volume calculado no mesmo turno não altera a qualidade e, portanto, não cria laço de otimização.
5. O benefício marítimo se aplica **uma vez à soma dos C0 mar–mar da perna**, entre embarque e desembarque. Coast C0 e congestionamento costeiro continuam cobrados uma vez cada; nenhuma multiplicação por número de tiles marinhos. Se não houver navegação mar–mar, não tratar o par de travessias como essa perna.
6. O cálculo tem de ser determinístico (fixed-point/BigInteger, critério de desempate estável), não aproximar custos ou descartar alternativas por número de portos de forma silenciosa. Diferenças de altitude entre nós são insumo físico existente; altitude absoluta, umidade e temperatura contínuas ficam para gate físico posterior, sem biomas discretos no custo.

## 3. Gargalo estrutural: o peso marítimo depende do PAR

Para a perna entre travessias costeiras reais `a` e `b`, com distância marítima natural `D(a,b)`:

`C_sea(a,b) = ceil(D(a,b) / (1 + β sqrt(sqrt((1+I_a/B_a)(1+I_b/B_b)) - 1)))`.

A fórmula acima é **apenas uma política experimental**; β atual não foi calibrado. O ponto estrutural é a dependência conjunta de `a` e `b`. Assim, não existe, em geral, um único custo aditivo fixo para cada aresta mar–mar que produza **exatamente** esse resultado para todos os pares. Inserir descontos marítimos em cada aresta oceânica do Dijkstra comum violaria o contrato.

O comparador atual monta subgrafos land/sea e considera todos os pares elegíveis para uma única perna. Reutilizar distâncias melhora o acesso a `D(a,b)`, mas deixa aproximadamente `O(P_a × P_b)` avaliações de pares *por consulta*, além da atualização da rede física e dos caminhos terrestres. Em milhares de mercados isso pode dominar tempo/CPU mesmo com cache quente.

## 4. Candidatos de arquitetura — comparar em vez de supor

### A — Supergrafo de travessias costeiras com distância marítima exata sob demanda

- Nós de transferência são as **arestas costeiras existentes** (IDs canônicos), não novos ativos construíveis; a infraestrutura e titularidade permanecem na aresta do jogo.
- A ligação virtual entre duas travessias armazena/consulta o menor `D(a,b)` no grafo mar–mar, usando mar aberto natural e positivo.
- Um planejador composto considera custo terrestre de chegada, custo físico e congestionamento/tarifa de embarque, `C_sea(a,b)` pelo par, custo de desembarque e custo terrestre até o destino.
- **Arestas virtuais não podem ser pré-materializadas ingenuamente para todos os pares**: haveria `O(P²)` memória e trabalho. Testar geração sob demanda com limites físicos claros e mecanismos exatos de descarte de candidatos quando houver *lower bounds* demonstráveis.
- Custo não separável por par e preferência dependente de proprietário tornam insuficiente um Dijkstra comum sobre nós de porto sem provar a aditividade do estado expandido.

### B — Busca por estados (tile, terminal de embarque) e fechamento da perna no desembarque

- Fora do mar, estado terrestre comum. Ao cruzar a aresta terra–mar, inicia-se um estado marítimo com **identidade do embarque e custo marítimo natural acumulado**.
- Em mar aberto, acumula-se `D` positivo sem desconto por aresta; ao desembarcar por aresta costeira `b`, aplica-se **uma única vez** a função exata com o par (`a`,`b`), além do custo costeiro. Não há dependência futura: o investimento está congelado.
- Precisa controlar dominância entre estados para evitar explosão `O(P × V_mar)`. Não é correto descartar arbitrariamente um terminal de embarque com custo acumulado maior: sua qualidade pode gerar um custo final menor. Uma poda só é válida se um limite inferior matemático provar que o estado descartado não pode vencer.
- Pode suportar mais de uma perna futuramente se o problema realmente exigir, mas **não** ampliar esse escopo no primeiro experimento.

### C — Referência: enumeração R55–R58

Conservar o comparador R55 como **oráculo exato de uma perna** nos mundos pequenos e médios. Não reescrever a referência para fazê-la coincidir com A ou B. Em toda divergência, bloquear a integração e auditar arredondamento, ordenação dos pares, isenção/tarifa e rotas disponíveis.

## 5. Desacoplamento por turnos e economia

`W_t`: estado físico + investimentos monetários já liquidados antes do ciclo.
`G_t = ProjectTransport(W_t)`: tipos de aresta, C0 efetivo, qualidade dos terminais e K; todos imutáveis durante o cálculo.
`MarketAndFreight(G_t, Loads_{t-1})`: resolve vendas, caminhos, congestionamento e tarifas sob o contrato existente. **Não** modifica `W_t` enquanto calcula.
`W_{t+1}`: recebe somente eventos devidamente liquidados e auditados. Melhorias feitas durante o turno tornam-se insumo conforme a ordem formal de fechamento, nunca por recursão no mesmo cálculo.
`Loads_{t-1}` é permitido apenas onde o algoritmo atual explicitamente usa cotação de congestionamento defasada. O sea-leg discount não deve depender dele.

## 6. Critérios quantitativos de go/no-go

1. **Exatidão:** custos, portos de embarque/desembarque e desempates iguais aos R55–R58 para a mesma topologia e política. Casos QA: natural, costa parcialmente melhorada, terra melhorada, ambos e sensibilidade extrema; rotas terrestres puras continuam legítimas.
2. **Escala:** comparar 1.442 / 3.242 tiles e mundos progressivamente maiores, até cenários com milhares de mercados/produção. Medir tempo *frio* e *quente*, alocação, memória máxima, número de estados/arestas virtuais explorados e `P_a × P_b` evitado comprovadamente.
3. **Economia:** quando houver integração, preservar venda/conservação física do trabalhador, demanda local, preços universais, frete, tarifas monetárias por proprietário, congestionamento e replay exato. Não inventar mercado de embarcações nem consumo de materiais.
4. **ROI:** uma otimização não basta por reduzir contadores de Dijkstra; exigir impacto **no tempo total** sob carga real e custo de memória aceitável. Se ganho adicional vier com crescimento grande da complexidade, **PARAR** e comparar modelo de custo/estrutura, conforme diretriz do jogador.
5. **Sem regressões:** kernel Win/Ubuntu/macOS, hash, Godot e benchmark existentes; PRs seguem Draft até homologação e decisão explícita de merge.

## 7. Decisão deste gate

**Não integrar o R57/R58 diretamente ao waterfilling.** O trabalho que resta não é encontrar outro cache, mas decidir se o custo pareado por terminais deve ser representado por (A) supergrafo sob demanda ou (B) estado expandido, com teste de prova exata e escala. O próximo artefato adequado é um *spike* comparativo com oráculo congelado, não mais uma alteração no kernel econômico.

O exercício R58 demonstrou benefício real do cache quente, mas também que o custo frio, reconstrução do grafo, enumeração de pares e memória precisam ser examinados juntos. Esta ADR não declara A ou B vencedores sem benchmark.
