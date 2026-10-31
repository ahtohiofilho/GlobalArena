# GA-VS42 — Concorrência e saturação de corredores econômicos compartilhados (laboratório)

**Data:** 31/10/2026. **Base empilhada:** GA-VS41 PR #147, head `085902f`, 8/8 workflows aprovados. **Política de integração:** PR draft, `main` intacta.

## Decisão em avaliação

Uma rota comercial pode ser individualmente barata, mas piorar a distribuição coletiva quando muitas remessas utilizam a mesma infraestrutura. A VS41 encontra o melhor percurso **com preço separável e carga fixa do serviço**; não resolve interdependência de capacidade/saturação entre serviços.

A VS42 adiciona uma **análise econômica hipotética**, não uma alteração no mercado real. Suas alternativas são serviços comerciais **declarados e validados** nos contratos VS40/VS41. Nenhum porto, terminal, ponte, estrada ou viagem extra pode ser inferido pelo otimizador.

## Modelo matemático (coeficientes só de laboratório)

Cada lote `i` tem carga física integral `q_i` em microgramas, uma origem/destino e serviços alternativos válidos. Cada rota percorre recursos **identificados por crossing fino Goldberg + ID da tecnologia + versão do catálogo**. Assim, ativos paralelos da mesma tecnologia numa mesma travessia compartilham a abstração de saturação; ferrovia e rodovia na mesma geometria são recursos independentes, salvo estudo futuro de restrição compartilhada.

Com `Q_e` a soma de `q_i` dos lotes atribuídos ao mesmo recurso `e`, a avaliação de um portfólio é

```
C(batch) = Σ_lot [ (q_i / 10^6) ×
                  (base_geografica_i / 10^6
                   + overhead_i / (q_i + smoothing_i)) ]
         + Σ_corridor [ congestionWeight × Q_e^3 /
                        (10^6 × congestionScale^2) ]
```

As unidades monetárias são as mesmas unidades brutas das cotações VS40; `q_i` e `Q_e` são microgramas e a divisão por `10^6` converte para gramas. **O componente de saturação individual da VS40 não é somado** ao componente compartilhado da VS42: o segundo substitui o primeiro no cenário alternativo, evitando dupla cobrança. A parcela de saturação por grama é progressiva em `Q_e²`, sem teto/limiar de capacidade, como já decidimos. Cálculos são feitos em `BigInteger` e `FoodTransportCostFraction`, sem ponto flutuante econômico.

Os custos de oferta, tecnologia, modalidade, desnível e distância vêm da função `ExperimentalAggregatedLandFreightCostFunction` VS40; os atributos da rede e o mapeamento comercial são validados com `ExperimentalSharedFreightOption.FromBuiltOffer`. Não se presume estoque físico de mercadorias nem veículos reais individuais.

## Três políticas lado a lado

1. **Escolha independente:** para cada lote, selecionar o custo monetário sem acoplamento, com empate por `ServiceId`. Serve como referência e NÃO é solução do congestionamento.
2. **Redistribuição coordenada:** em ordem canônica de `LotId`, tentar trocar a alternativa de um lote; aceitar apenas melhorias estritas do custo agregado. Repetir até estabilizar ou atingir `maxImprovementPasses`. Esta é uma **heurística local**, nunca declarar ótimo global.
3. **Enumeração exata de referência:** para portfólios com `∏ alternativas ≤ maxExactConfigurations`, comparar todas as atribuições e retornar o mínimo exato, com desempate lexicográfico. Em portfólios maiores, o campo `Exact` permanece **nulo**. Nunca alegar certificação em portfólios grandes.

A solução é uma **avaliação de remessas indivisíveis de volumes previstos e fixos**, não a distribuição competitiva simultânea de quantidades endógenas que o mercado futuramente terá de decidir. O alvo matemático usado no laboratório é gasto financeiro agregado, não bem-estar social completo, nem oportunidade de novos fluxos lucrativos. A liquidação econômica no turno segue intacta.

## Controles e limites de confiança

- `FromBuiltOffer` exige origem/destino canônicos, assets finos operacionais, catálogo/tecnologia únicos, trajeto contíguo e sem cruzamento aquático, com acesso econômico aberto. Não há transbordo implícito.
- O nome do recurso inclui resolução geográfica e tecnologia/catalogo. **Rede VS39 é versionada por snapshot**; nenhuma tarifa pode ser reutilizada automaticamente entre mudanças da infraestrutura ou ciclos econômicos.
- A avaliação não altera `WorldState`, save, hash, journal, compradores, produtores, movimentos ou cotações financeiras existentes.
- Um quote real `EconomicTurnFreightQuote` continua congelado durante o ciclo; o resultado VS42 é **previsão contrafactual** que não substitui silenciosamente a tarifa lockada.
- Conectividade física/tecnológica precisa ser comprovada antes do lote entrar no modelo, não é criada ao alocar lotes.
- Sem bancos, crédito ou dívida; sem novos atributos militares e sem mudanças na UI do jogador.

## Benchmarks e critério de avanço

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs42-shared-freight
```

Benchmark sintético com **8, 28 e 64 lotes**, alternativas por lote, corredores que se cruzam e previsão em microgramas, aquecimento e 3 amostras por cenário. Verifica conservação do resultado e sua estabilidade entre execuções; mede custo independente, custo coordenado, solução exata somente no caso pequeno, número de avaliações, tempo/bytes alocados. Não há threshold arbitrário de tempo ou promessa de FPS. **Não** representa uma partida real com centenas de cidades, ciclos econômicos, todas as mercadorias e termos de escolha conjunta: benchmarks integrados serão necessários.

QA: concorrência de duas remessas, exatidão por enumeração, modalidade rodoviária/ferroviária sem fusão artificial, saturação sem cliff, não duplicação do custo VS40, entradas inválidas, limite de escopo exato, ofertas com ativos reais, save/hash intactos, build Godot Windows/Linux e regressão integral Windows.

## Roadmap seguinte

Após a VS42, avaliar cenários **de múltiplos polos com mercadorias e volumes endógenos**, buscando um mecanismo simultâneo de decisão comercial com saturação e oferta/demanda. Comparar heurísticas escaláveis e limites aproximados antes de qualquer ativação no mercado. Uma eventual política compartilhada deve usar snapshot econômico, capacidade/serviços explícitos e manter preço financeiro travado durante a liquidação. O usuário deve continuar a dar ordens simples.
