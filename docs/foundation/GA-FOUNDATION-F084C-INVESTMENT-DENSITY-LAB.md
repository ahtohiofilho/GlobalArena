# GA-FOUNDATION-F084C — Densidade lógica e investimento físico da aresta

**Estado:** laboratório executável e testes, NÃO ratificação de fórmula/coeficientes/ritmo.  
**Data:** 2026-11-08.  
**Base técnica:** PR #303 (F-084B, dependente de #302/F-084A e PR #298/fundação).  
**Fronteira agrícola:** PRs #299–#301 permanecem preservados, independentes, sem alteração.  
**Decisões-base:** F-024/F-025/F-030/F-031/F-034/F-035/F-060/F-062/F-081/F-082/F-084.

## 1. Objetivo e escopo

Este gate testa duas hipóteses distintas:
1. **Densidade lógica e cadência de trabalhadores**: mundos com quantidades diferentes de nós/arestas são mundos logicamente distintos; podem requerer números distintos de saltos e turnos para cruzar frações espaciais comparáveis. Trocar **somente LOD visual**, mantendo grafo lógico, não altera a viagem.
2. **Melhoria física de uma aresta já investida**: dado o estado natural da aresta e uma quantidade **hipotética de capital já incorporado**, avaliar impedância em fluxo livre e capacidade natural melhorada, sem simular recebimento/pagamento, decisão da obra, trabalhador construindo, titularidade tarifária, preço, volume ou solver econômico.

**Não** converter capital em tempo de deslocamento de trabalhador por efeito implícito. A F-084B continua separada. Investimentos futuros podem afetar trabalhador, mas isso exige decisão própria.

## 2. Contrato puro de melhoria

A função `EdgeInvestmentLaboratory.Evaluate(natural, incorporatedCapital, referenceCapital)` recebe:
- `natural.ImpedanceRaw=C_0` e `natural.NaturalCapacityRateRaw=K_0` resultantes da F-084A, **ambos positivos**;
- `I ≥ 0`: capital total efetivamente incorporado, já verificado externamente no caso real;
- `I0 > 0`: capital de referência da melhoria, em **mesmas unidades de I**.

A soma `SumCapital([H_e,c])` agrega participações positivas ou nulas, de modo determinístico e com proteção contra overflow. Essa soma é um cálculo físico para a aresta inteira, **não** crédito ou débito de tesouro nem contrato de copropriedade/tarifa. A identidade civilizatória, os direitos e o rateio pertencem às F-060/F-062, ainda não implementadas neste laboratório.

**Candidato matemático, não canônico:**
```text
L(I) = log2(1 + I/I0)
U(I) = 1 + L(I)
C_livre(I) = C0 / U(I)
K(I) = K0 * U(I)
```

Para determinismo inteiro, o código usa uma **aproximação linear por segmentos diádicos** de `log2(1+I/I0)`, exata nos pontos `I/I0 = 0, 1, 3, 7, 15, 31, ...`, com declive menor a cada segmento. É contínua antes da quantização fixed-point; **não** é logaritmo exato fora desses marcos. Não há um teto artificial de investimento: U segue crescendo, com rendimentos marginais decrescentes; a impedância permanece positiva e a capacidade permanece maior que zero.

Referência de laboratório: aresta terrestre neutra `C0=1, K0=1`, `I0=1` unidade abstrata de capital:

| I | U | C_livre | K |
| ---: | ---: | ---: | ---: |
| 0 | 1 | 1 | 1 |
| 1 | 2 | 0,5 | 2 |
| 3 | 3 | 0,333333 | 3 |
| 7 | 4 | 0,25 | 4 |
| 15 | 5 | 0,2 | 5 |

**Somente C0=1 de terra neutra é uma normalização ratificada.** O capital I0, U, forma do log, K0 e elasticidades permanecem hipóteses. Não apresentar C=0,5 em I=I0 como regra estabelecida do jogo.

## 3. Limites de governança

Este módulo **não** incorpora capital; recebe um montante cujo pagamento e trabalho efetivo teriam de ser autorizados e contabilizados em outro sistema. Portanto:
- não credita infraestrutura por intenção ou expectativa de investimento;
- não debita tesouro, não transfere receita e não estabelece direitos societários;
- não inventa itinerário logístico persistente, porto no nó, modo de transporte ou objeto frota;
- a mesma melhoria física afeta qualquer usuário da aresta; eventual pedágio de não titular segue F-024/F-060/F-062/F-073;
- `C_livre` é impedância **sem carga/congestionamento**; uso de `B_e/K_e` e função de saturação fortemente convexa será outro laboratório;
- não substitui a regra de tempo real `WorkerEdgeTravel` nem altera sua eficiência/cadência;
- não atribui preço por destino F-071, nem resolverá automaticamente o clearing global.

A linha de desenvolvimento continua separada da V1/V2. Nada migra automaticamente para o motor canônico.

## 4. Ensaios e critérios de QA

- Investimento zero reproduz exatamente impedância e capacidade natural nas três classes de aresta.
- Marcos diádicos fixos produzem efeitos de referência reproduzíveis; para `I≥0`, maior capital não pode piorar C ou K.
- Retornos marginais de U são não crescentes em intervalos de capital iguais, sem um teto de capital.
- Fluxo livre e capacidade positivos com toda entrada válida; entradas negativas, I0 inválido e soma com overflow falham explicitamente.
- Fracionar I entre três civilizações ou concentrar em uma dá a **mesma melhoria física**, sem calcular tarifa ou direitos aqui.
- Investimento feito em uma aresta não altera as outras; o esforço físico do trabalhador não muda por efeito indireto da calibração comercial.
- Cenário F-083: antes da obra, dois saltos difíceis (C total 4) perdem para três neutros (C total 3). Ao aplicar capital de referência em **cada uma das duas** arestas difíceis, elas passam a C total 2 nesse candidato, podendo vencer a rota neutra. As distâncias continuam 2 e 3.
- Anéis lógicos sintéticos com 12 e 120 nós, separados por um quarto do anel, requerem respectivamente 3 e 30 saltos neutros — mundos lógicos diferentes, não falha de invariância por LOD.
- Testes determinísticos em aritmética inteira e checksum de referência, além das regressões F-084A/F-084B.
- Benchmark observacional em 10 mil e 100 mil arestas, com cálculo do estado natural fora da região cronometrada.

Esses anéis são **fixtures de laboratório**, não malhas Goldberg geradas, e não demonstram ainda funcionamento sobre mundos procedurais reais. A amostragem não é homologação de gameplay ou convergência econômica.

## 5. Gate seguinte recomendado: F-084D

Executar **congestionamento F-031** na aresta melhorada, usando uma única taxa de carga `B_e` por unidade de tempo e `K_e` compatível, sem preço nem coeficiente por produto. Comparar expoentes globais candidatos, especialmente `p=3/4/5`, preservar infraestrutura natural com investimento zero e verificar sobrecarga severa em costa de baixa capacidade.

Somente depois testar a tarifa F-073 ligada à melhora física em referência de carga fixa, cotitularidade, F-071 por destino e equilíbrio global. Em paralelo, ritmo de trabalhadores e parâmetros de terreno precisam ser calibrados em mapas lógicos reais; **não** tomar números dos benchmarks isolados como balanço final.

**Trilha longa preservada:** agricultura PRs #299–#301, nova fundação #298, circulação #302/F-084A e #303/F-084B, seguidos deste laboratório F-084C.
