# GA-FOUNDATION-F084A — Impedância e capacidade naturais por aresta lógica unitária

**Estado:** laboratório executável isolado (proposta de formulação, NÃO ratificação de coeficientes).  
**Data:** 2026-11-08.  
**Fundação:** F-081, F-082 e F-083 de `GA-FOUNDATION-000`; executa somente o primeiro gate de F-084.  
**Ramo base:** `docs/ga-foundation-zero-20261107` / PR #298.  
**Agricultura:** PRs #299–#301 são irmãos de desenho; nenhum código é importado deles.

## 1. Decisões preservadas

- **Cada aresta lógica tem distância d=1**, mesmo que o globo a desenhe com outro arco/comprimento.
- **Impedância C** é um índice físico-econômico adimensional somado ao longo das arestas da rota hipotética. **Não** representa tempo de trabalhador, dinheiro, tarifa ou frete físico movimentado.
- **Capacidade natural K** é uma taxa de tráfego comercial abstrato por período de referência; não é estoque ou dinheiro. Regra de congestão `B/K`, expoente e prazo temporal serão integrados somente depois.
- Aresta terra-terra, água-água ou terra-água é sempre **uma aresta**, sem entidade porto, navio, estrada ou taxa extra de nó. A interface costeira é da própria aresta.
- Diferentes valores físicos podem gerar impedâncias diferentes sem afetar distância lógica. Não exigir invariância ao aumentar número de nós LÓGICOS; exigir invariância ao mudar somente renderização/LOD.
- Melhorias, capital `I_e`, coparticipação, pedágio `w_e`, trabalhador, liquidação de mercado e solver não são implementados aqui.

## 2. Entradas físicas mínimas

`NaturalLogicalEdge` recebe:
- natureza de cada extremo (terra/água);
- elevação normalizada de cada nó, apenas para diferença de relevo em **terra-terra**;
- rugosidade local do trecho de terra ou do lado terrestre da costa.

Rugosidade é um **sinal físico abstrato** que um futuro worldgen lógico poderá fornecer; não é um bioma, cultivo, tipo de estrada, ou um coeficiente inferido de pixels da esfera. Se o mundo ainda não dispuser de rugosidade canônica, esse campo permanece input de laboratório — não criar um novo gerador de rugosidade nesta etapa.

Para água-água, **não** se interpreta variação da profundidade oceânica como aclive. Para terra-água, também não se toma a diferença de altitude entre o fundo marinho e o terreno como uma segunda taxa de desembarque.

Valores normalizados fixos de elevação em [-1,+1], rugosidade em [0,1]. O avaliador usa somente inteiros com escala `S=1.000.000`, aritmética determinística e validação explícita.

## 3. Formulação candidata, não canonizada

Seja `M_e` a classe física da aresta (`land`, `water`, `coast`). A categoria determina índices base `c_M` e `k_M`. Por F-082, **somente** `c_land=1` em terreno neutro é ratificado.

Seja `r` a rugosidade normalizada e `g=min(|h_A-h_B|,1)` a diferença de relevo em uma aresta terra-terra (ou `g=0` em água/costa). Em água-água, `r=0` obrigatoriamente. Então o laboratório calcula:

```text
C_e^0 = c_M + a_r * r + a_g * g
K_e^0 = k_M / (1 + b_r * r + b_g * g)
d_e   = 1
```

Os coeficientes `a_r, a_g, b_r, b_g` e os valores costeiros/aquáticos `c_M, k_M` são **todas hipóteses a calibrar**, exceto `c_land=1` para solo neutro. A função positiva/racional de K é apenas candidata; não presume relação monetária entre C e K.

| Valor | Candidato A | Autoridade |
| --- | ---: | --- |
| c_land neutro | 1,00 | **F-082 ratificado** |
| c_water | 0,80 | laboratório |
| c_coast | 1,50 | laboratório |
| a_r | 0,60 | laboratório |
| a_g | 0,40 | laboratório |
| k_land | 1,00 | laboratório (escala de taxa) |
| k_water | 1,50 | laboratório |
| k_coast | 0,35 | laboratório |
| b_r | 0,60 | laboratório |
| b_g | 0,40 | laboratório |

Para terra-terra com `r=g=1`, `C=2`; assim reproduz-se o cenário difícil de F-083, enquanto uma ligação neutra permanece C=1. Isso verifica coerência do exemplo **sem ratificar** valores climáticos/geográficos, números de obras ou tarifas.

## 4. Invariantes e QA

1. Duas arestas neutras custam 2 unidades; três custam 3.
2. Relevo/rugosidade aumentam C e diminuem K em relação ao trecho neutro do mesmo tipo. Todas as capacidades naturais são estritamente positivas sob calibrações válidas.
3. Inverter a direção de uma aresta não altera os custos/capacidades neste **modelo simétrico inicial**; efeito direcional de subida/corrente exige contrato próprio.
4. Mudar somente a altitude absoluta dos dois nós, preservando diferença e rugosidade, não altera C. Não multiplicar por extensão geométrica, latitude, raio ou grau da malha.
5. Terra-mar cobra categoria de costa **na aresta uma única vez**, sem taxas automáticas de origem/destino, porto/embarcação. Profundidade oceânica não vira aclive.
6. Variações pequenas dos campos contínuos produzem mudanças pequenas dos resultados, salvo quantização fixa inevitável; não há classificação por bioma.
7. C é separado de K, tempo, preço, tarifa, dinheiro, movimento executado e investimento.
8. Repetição produz índices idênticos independentemente da plataforma, da ordem de chamada e de LOD visual.
9. Referência F-083: rota difícil de dois saltos C_total=4 pode perder para três saltos neutros C_total=3. A atualização por obras será outro gate.

## 5. Compilação e verificação

```powershell
dotnet test .\GlobalArena.Foundation.Circulation.Tests\GlobalArena.Foundation.Circulation.Tests.csproj -c Release
dotnet run --project .\GlobalArena.Foundation.Circulation.Benchmark\GlobalArena.Foundation.Circulation.Benchmark.csproj -c Release
```

Workflow específico `GA Foundation F084A Natural Edge Lab` executa Linux/Windows/macOS e benchmark observacional de 10 mil e 100 mil arestas. Não reivindicar PASS antes de logs reais. O benchmark inclui apenas custo computacional da fórmula, não valida qualidade econômica nem throughput real de simulação.

## 6. Próxima decisão F-084B

Somente após validar os ensaios:
- comparar **formas** aditiva, multiplicativa ou outra para C/K, observando especialização geográfica, caminhos alternativos e variação de escalas;
- calibrar **tempo real de deslocamento de trabalhador por salto** como regra própria e independente de C, com continuidade de trabalho e turno F-013/F-015;
- posteriormente trabalhar investimento e congestionamento, F-073, rastreio de destinos F-071, solver global e tarifas F-060/F-062.

**Não promover os coeficientes do Candidato A ao cânone sem deliberação explícita e mais testes de gameplay.**
