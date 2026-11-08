# GA-FOUNDATION-F084D — Saturação convexa da circulação por aresta lógica

**Data:** 2026-11-08.  
**Estado:** laboratório executável, CI por comprovar; **nenhum expoente ou coeficiente adicional canonizado**.  
**Base:** PR #304 (F-084C), descendente dos PRs #303/#302 e da nova fundação #298.  
**Escopo:** F-026 (carga equivalente), F-030 (infraestrutura natural), F-031 (saturação), F-081/F-082 (grafo/impedância unitária), F-084(3) e pré-requisito futuro de F-073.

## 1. Uma função, três expoentes de laboratório

A aresta lógica tem `d_e=1`, qualquer que seja a geografia visual. A F-084C fornece, em um **snapshot** de investimento já incorporado, a impedância em fluxo livre `C_livre,e(I)` e a capacidade de referência `K_e(I)>0`. A F-084D propõe, em caráter **não ratificado**:

```text
B_e = carga comercial EQUIVALENTE por unidade de tempo
rho_e = B_e / K_e(I)
C_e(B_e,I) = C_livre,e(I) * [1 + rho_e^p]
p ∈ {3,4,5} no laboratório; um único p global por cenário
```

A taxa de carga `B_e` não equivale a valor monetário, não exige transporte de mercadoria física nem carteira compradora. É uma única carga compartilhada, **sem multiplicadores arbitrários por produto**. O expoente `p` é global por cenário do futuro solver: não é personalizável por item, civilização, tipo de terreno ou aresta. A rotina pura recebe `p` explicitamente para testes; cabe ao orquestrador futuro aplicar o **mesmo valor** a todas as arestas de uma mesma avaliação.

`K_e` é referência de **congestionamento**, não uma barreira máxima; sobrecarga severa aumenta muito a impedância, mas não bloqueia automaticamente a passagem. Saturação natural de costa é consequência de `K_natural` possivelmente reduzido, sem nova estrutura de porto ou operação adicional no nó.

| Ocupação B/K | p=3 | p=4 | p=5 |
| ---: | ---: | ---: | ---: |
| 0 | 1,000000 | 1,000000 | 1,000000 |
| 0,5 | 1,125000 | 1,062500 | 1,031250 |
| 1 | 2,000000 | 2,000000 | 2,000000 |
| 2 | 9,000000 | 17,000000 | 33,000000 |
| 3 | 28,000000 | 82,000000 | 244,000000 |

A tabela expressa o multiplicador sobre `C_livre`. Todos os valores são uma comparação de candidatos; **não ratifica p=4** nem qualquer escala natural de capacidade. Maior expoente reduz a penalidade em `0<rho<1`, mas aumenta a penalidade acima da capacidade.

## 2. Contrato temporal e numérico

`EdgeCongestionLaboratory.RatePerTurn(volumePerPeriodRaw, periodDurationTurnsRaw)` transforma volume equivalente por **janela econômica** em **taxa por turno** antes de comparar com a capacidade. Exemplo: seis unidades por dois turnos e doze unidades por quatro turnos fornecem a mesma taxa B=3 e o mesmo congestionamento. Um ciclo mais longo não pode produzir saturação fictícia somente porque acumulou mais volume.

Todos os índices usam fixed-point `Scale=1.000.000`, divisões inteiras determinísticas, potência calculada apenas com aritmética `Int128`. A discretização introduz degraus numéricos subunitários, não fronteiras de biomas. Entradas negativas, capacidade nula, expoente fora de 3/4/5 e valores além da faixa representável **falham explicitamente**, sem wrap ou clamp oculto. O overflow numérico não é um limite físico de capacidade; eventual expansão da representação do solver será uma decisão de engenharia.

As entradas `NaturalEdgeEvaluation` (sem capital) e `FreeFlowInvestedEdge` (com capital) são aceitas pela **mesma fórmula**. A função apenas retorna impedância efetiva e estatísticas do snapshot. Não muta aresta, trabalhador, tesouro, caminho, demanda nem preço.

## 3. Invariantes de negócio e QA

1. Carga zero implica `C_efetivo=C_livre` em todos os expoentes; `B=K` implica multiplicador exatamente 2.
2. Para cada expoente, custo não decresce quando a carga aumenta; superar K é válido e acarreta forte aumento de C, sem cap arbitrário em `B/K=1`.
3. P=3/4/5 gera efeitos comparáveis e reproduzíveis: `rho=2` produz multiplicadores 9,17,33.
4. Costa natural de capacidade baixa sofre congestionamento severo sob carga alta, mas uma obra hipotética **na própria aresta** eleva K e diminui C mantendo a mesma carga.
5. Investimento zero na F-084C e ausência de melhoria equivalem à infraestrutura natural da F-084A sob a mesma carga.
6. Quando quantidade e duração do ciclo são proporcionais, a taxa B não muda; carregar preço, imposto, tarifa ou quantidade de compradores nesta fórmula seria violação do contrato.
7. Dividir a mesma taxa de equivalentes logísticos entre produtos distintos não altera o resultado; não existe coeficiente de perecibilidade/raridade por mercadoria.
8. A carga comercial não altera duração física F-084B de movimento do trabalhador. A distância permanece uma aresta lógica; não há repetição de carga por nós de transbordo.
9. Não confundir a variação local de C com liquidação de tarifas F-060/F-073 ou receita por destino F-071.
10. Potências e resultados têm checksum de referência em 4.096 amostras fixas para verificação cruzada de sistemas operacionais.

## 4. Benchmark e homologação

O benchmark isolado observa **10 mil e 100 mil arestas**, com `p=3,4,5`, três repetições cada. A preparação das condições naturais, do capital hipotético e do vetor de carga fica **fora da medição**; apenas `Evaluate` é cronometrado. Assim, tempo observado não é custo de geração procedural, execução de investimentos, solver de fluxo global ou renderização Godot. Não há meta de latência ratificada nem extrapolação de desempenho da simulação completa.

```powershell
dotnet test .\GlobalArena.Foundation.Circulation.Tests\GlobalArena.Foundation.Circulation.Tests.csproj -c Release --nologo
dotnet run --project .\GlobalArena.Foundation.Circulation.Benchmark\GlobalArena.Foundation.Circulation.Benchmark.csproj -c Release
```

O workflow `GA Foundation F084A-D Circulation Lab` deve confirmar regressões F-084A/B/C/D em Ubuntu, Windows e macOS e o benchmark observacional Linux. **Validar HEAD exato da branch antes de declarar homologação.**

## 5. Gaps futuros e decisão de produto

Esta etapa **não resolve o ciclo endógeno** em que alocação de mercadorias define `B_e`, `B_e` altera C e os caminhos comerciais podem mudar. O modelo de equilíbrio global e sua convergência devem ser testados depois. F-073 utiliza, se aprovado, uma **carga natural de referência fixa** para identificar ganho de obra e calcular peso tarifário, não a carga atual do clearing. Nenhum peso monetário, repartição de dividendos ou preço é calculado aqui.

**Próxima frente sugerida:** F-084E, cenário controlado de F-073 sobre `C(B=K_natural, I=0)` versus `C(B=K_natural,I>0)`, com titulares/cotitulares e não titulares, primeiro só no nível de peso adimensional e invariantes econômicos; **não** lançar receitas antes de ratificar o mecanismo F-060/F-071. Depois: teste de preço/destino F-071 e solver de mercado com carga endógena.

**Trilha preservada:** fundamento #298, agricultura #299–#301, arestas naturais #302, trabalhadores #303, investimento #304, saturação F-084D.
