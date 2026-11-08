# GA-FOUNDATION-F084B — Movimento físico de trabalhador por aresta lógica

**Data:** 2026-11-08  
**Estado:** laboratório executável independente, sem ratificação de coeficientes ou promoção ao motor.  
**Base:** GA-FOUNDATION-000 F-009/F-012/F-013/F-015/F-017/F-018/F-019/F-081/F-082/F-084; F-084A no PR #302.  
**Dependência técnica:** `GlobalArena.Foundation.Circulation` da F-084A; ramificação direta do PR #302.  
**Fronteira independente:** agricultura #299–#301 permanece preservada, sem importação ou dependência.

## 1. Separações canônicas preservadas

1. **Distância lógica** por aresta: `d_e=1`, sem comprimento da corda/esfera, LOD, raio ou coordenadas visuais.
2. **Impedância econômica** `C_e`: componente adimensional de circulação comercial, **não** tempo de travessia do trabalhador.
3. **Tempo de trabalhador**: quantidade de **um orçamento temporal universal** usada para atravessar a aresta; compete temporalmente com produção e construção.
4. **Capacidade de tráfego comercial** `K_e`: taxa física do fluxo comercial abstrato, não carteira nem tempo de ação.
5. **Localização física**: um trabalhador que ainda atravessa a aresta não está disponível no nó de destino; a chegada ocorre somente no marco de conclusão.
6. **Progresso F-015**: frações válidas de tempo comprometidas permanecem registradas entre turnos; não há teleporte, término automático, dupla cobrança ou reset.
7. **Sequencialidade F-017**: ao concluir uma travessia, a parcela não usada do período pode iniciar outra travessia ou ação autorizada após a chegada, sem criar saldo de movimento separado.

## 2. Modelo de laboratório

O `WorkerEdgeTravel.RequiredTimeRaw` recebe:
- o mesmo `NaturalLogicalEdge` **físico** da F-084A: meios de origem/destino, elevações e rugosidade;
- eficiência **do trabalhador**, um escalar positivo sem valor normativo de era;
- parâmetros de `WorkerTravelCalibration` **inteiramente independentes** dos parâmetros de preço/capacidade de `NaturalEdgeCalibration`.

A dimensão interna `Scale=1.000.000` é uma resolução *fixed-point* de **um período de referência**. É representação numérica, não compromisso de que o trabalhador se mova exatamente um tile por turno.

Cálculo experimental:
```text
medium = land | water | coast
r = rugosidade local (0 para água-água)
g = min(|elevationA - elevationB|, 1) para terra-terra;
    g = 0 nas arestas de água ou costa
effort = baseEffort[medium] + weightR * r + weightG * g
timeRequired = max(1 microperíodo,
                   ceil(effort * Scale / workerEfficiency))
```

Os números abaixo são **hipóteses de laboratório, não normas do jogo**:

| Parâmetro | Candidato B |
|---|---:|
| Terra neutra, trabalhador padrão | 1,00 período por aresta |
| Água-água | 0,65 período |
| Costa (aresta única) | 1,40 período |
| Peso de rugosidade | 0,70 |
| Peso de diferença altimétrica terrestre | 0,50 |
| Eficiência de referência | 1,00 |

As escolhas de ritmo físico serão posteriormente comparadas com experiências de jogo e distintas densidades de grafo **lógico**; não derivar dos números históricos nem fixá-los por conveniência do código. A travessia marítima continua implicitamente possível neste laboratório, sem obrigatoriedade de objetos navio/porto. Sua elegibilidade tecnológica/política, quando existir, depende de contrato posterior.

A fórmula é inicialmente simétrica na inversão da aresta. Não presume que subida, descida, correnteza, trilha ou terreno tenham sempre efeitos simétricos no projeto final.

## 3. Estado físico mínimo e tempo compartilhado

- `WorkerEdgeTransit.Begin(originId,destinationId,timeRequired)` mantém uma única travessia imutável por trabalhador.
- `ElapsedTimeRaw` é progresso da ação. `CurrentNodeId` permanece **null** enquanto estiver na aresta.
- `WorkerActionTimeBudget` contém `TotalRaw`, `SpentRaw` e `RemainingRaw`; não existe segundo saldo de pontos de deslocamento.
- `WorkerTravelProgress.Advance(transit,budget,requestedTime)` gasta no máximo o tempo efetivamente necessário para terminar a aresta, retorna o novo estado e orçamento, e preserva o saldo para ações sequenciais.
- Uma chamada com `requestedTime=0` não gera progresso. Tentativas de ultrapassar orçamento, atravessar nó para si mesmo ou avançar travessia já concluída são rejeitadas.
- O tempo de travessia requerido é **fixado ao iniciar a aresta**, para que o progresso seja reconstituível mesmo após virada de turno. Se terreno/eficiência mudar durante o trânsito, replanejar ou reavaliar o saldo exigirá decisão adicional — não reprecificar silenciosamente trechos já executados.

Exemplo de laboratório: o trabalhador tem 1,00 período. Consome 0,30 em outra ação, depois 0,60 na travessia, e preserva 0,60 de progresso na aresta. No mesmo período ainda pode aplicar 0,10 de tempo válido, atingindo 0,70 de progresso, sem chegar. No período seguinte, necessita apenas 0,30 para completar uma travessia de 1,00. O saldo de 0,70 do **novo período** pode ser usado em produção ou novo movimento. O saldo do período anterior não foi transportado.

## 4. Validação, limites e QA

Testes de laboratório verificam:
1. tempo físico distinto de C e K para a mesma aresta e nenhuma interferência bidirecional entre calibrações;
2. continuidade exata ao dividir um intervalo de viagem em subintervalos;
3. viagem que exige mais de um turno, chegada apenas após conclusão e reutilização do saldo do período;
4. duas arestas sequenciais no mesmo turno após chegada quando a eficiência permitir;
5. ausência de penalidade geodésica, por altitude absoluta ou pelo fundo oceânico;
6. reversão simétrica somente na lei proposta, coerência dos campos físicos e sem exclusão por bioma;
7. reação contínua às mudanças pequenas de atributos, aritmética inteira sem ponto flutuante;
8. validação de índices/tempos/IDs, proibição de gasto acima do orçamento, continuidade determinística;
9. checksum fixo de 4.096 travessias com variedades de meio/atributos/eficiência;
10. regressão F-084A mantida no mesmo projeto de testes e benchmark 10 mil/100 mil para duração/progresso, em Linux/Windows/macOS.

**Contrato de alcance deste laboratório:** o orçamento é recebido/devolvido como valor imutável. O **motor autoritativo**, ainda não integrado, deverá guardar o novo estado e rejeitar aplicações duplicadas do mesmo comando/journal. Não foi implementado o escalonador cronológico global F-018, arbitragem real entre trabalhadores F-019, comprovação da adjacência na topologia externa, comandos do jogador, equipamentos, suprimentos, consumo, colisões, persistência de save ou mudança física dinâmica no meio da aresta.

## 5. Execução e integração

```powershell
dotnet test .\GlobalArena.Foundation.Circulation.Tests\GlobalArena.Foundation.Circulation.Tests.csproj -c Release --nologo
dotnet run --project .\GlobalArena.Foundation.Circulation.Benchmark\GlobalArena.Foundation.Circulation.Benchmark.csproj -c Release
```

O workflow `GA Foundation F084A-B Circulation Lab` executa três sistemas operacionais e benchmark Linux. Somente logs reais dão PASS, jamais presumido pelo commit.

**Próximo gate sugerido F-084C:** cenários de calibração de ritmo físico em múltiplas densidades de grafo lógico, separando ações do trabalhador de congestionamento/capacidade comercial, e avaliação de investimento `I_e`, saturação, tarifas F-073 e conciliação F-071. Evitar mistura prematura da cadência com o solver econômico e preservar o compromisso de não criar itinerários logísticos persistentes.
