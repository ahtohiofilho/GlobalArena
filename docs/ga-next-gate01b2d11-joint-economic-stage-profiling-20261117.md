# GA-Next — Gate 01B.2D-11: perfil de custo por etapa e crescimento Goldberg

**17/11/2026 · incremento de observabilidade C# opt-in · Draft sobre #403 · sem merge, Godot ou liquidação**

## Situação herdada

Os PRs #397–#403 isolaram a concorrência entre produtores e passaram de realocações sequenciais para planejamento global côncavo segmentado, roteamento multipath conservativo e uma política de potencial integrado com propostas estritamente vantajosas. O #403 homologou 8 produtores/104 mercados/112 tiles e 16 produtores/192 mercados/208 tiles. O caso severo de 8/104 concluiu **BLOCK_STATIONARY** em uma passagem, mas isto certifica apenas ausência de melhoria sob os dois operadores propostos — não ótimo econômico contínuo nem equilíbrio multipath global.

A próxima restrição de produto é a escala: o jogo pretende trabalhar com **milhares de nós**, diversas civilizações e tráfego econômico complexo. Não devemos remover complexidade econômica para obter números artificiais. Antes, é necessário descobrir qual fase domina **tempo** e **alocações gerenciadas**: formação de mercados globais, rotas com congestionamento, auditoria física ou coordenação conjunta.

## Metodologia reprodutível

O novo teste `IncreasingGoldbergMarketsSeparatesGlobalFlowMultipathAuditorAndJointCosts` reutiliza exatamente o fixture físico real de #402/#403, com demanda por trabalhador real, produtores todos do mesmo grão e conversão física não alterada. Compara:

| Caso | Goldberg | Tiles conectados | Produtores | Mercados | Limite de pares vendedor×mercado |
|---|---|---:|---:|---:|---:|
| Baseline | G(3,0) | 112 | 8 | 104 | 832 |
| Intermediário | G(5,0) | 208 | 16 | 192 | 3.072 |
| Sondagem ampliada | G(6,0) | 320 | 24 | 288 | 6.912 |

Os limites de pares são **combinatórios**, não alegações de existência de todos os arcos físicos ou preços concretos. O custo por aresta e a conversão continuam reais, com semântica de tarifa/titular inalterada. Os cenários são medidos em sequência, sob limites explícitos de 4 segmentos médios por mercado, até 4.096 aumentos de fluxo por proposta, até duas parcelas de rota por entrega e até 8.192 parcelas no agregado. A coordenação conjunta recebe **3 passagens** como limite de trabalho: estabilidade discreta e budget exaurido são resultados diferentes, com a falha tipada preservada.

### Quatro fases medidas

1. **GlobalSellerMarketFlow:** `FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate` — cálculo global de vendedores, beneficiários, rotas iniciais e auditoria interna. Registra quantidade real de augmentations.
2. **FixedMarketMultipathRouting:** `RouteFixedMarket` — mesma quantidade/comprador do passo 1, reconstituição de caminhos, divisão de cargas, custos integrados e auditoria interna. Registra número de lotes e partes de entrega.
3. **IndependentPhysicalFreightAudit:** uma chamada **adicional e independente** de `FoundationPhysicalDeliveryAllocationAuditor.Audit` para as partes de rota do passo 2. A reconstrução do grafo de propriedade é feita fora do cronômetro desta fase, sendo incluída internamente no passo 2. O teste confere mercadorias e frete por cruzamento.
4. **CompleteJointBlockSearch:** uma nova execução ponta a ponta do coordenador conjunto #401, que por construção **inclui internamente** alocação global, roteamento e auditoria várias vezes. Seu tempo não deve ser somado aos três tempos isolados.

Cronometragem: `Stopwatch.GetTimestamp / GetElapsedTime` por fase. Alocação: `GC.GetAllocatedBytesForCurrentThread`, uma **observação da quantidade de bytes gerenciados alocados na thread do teste**, não pico de RAM, heap retido, memória nativa ou máximo de processo. Não impomos SLA, limite arbitrário de tempo ou limiar de alocações em CI. A instrumentação adiciona overhead e o runner pode variar; os valores não são extrapolações determinísticas nem benchmark científico com aquecimento e múltiplas amostras.

### Integridade e saídas

Cada medição precisa preservar integralmente mercadorias por trabalhador e mercado, a auditoria real, `StateHash`, snapshot, ausência de ciclo econômico fechado e aceitação monotônica. Não se tolera orçamento insuficiente para o solver global ou roteamento sem certificado; a exceção tipada do coordenador conjunto é registrada como `BUDGET_EXHAUSTED` sem alocação provisória.

A variável opt-in `GA_JOINT_SCALE_REPORT_DIR` cria exatamente três relatórios JSON de schema `ga-next.joint-stage-profiling.v1`; sem ela, não grava arquivos na execução local. A rotina PowerShell `scripts/GA-Next-Joint-Stage-Profiling-Report.ps1` valida estrutura, dimensões, conservação, potencias, quatro fases e contadores não negativos. Qualquer arquivo ausente, incongruência ou relatório parcialmente escrito causa **falha de CI**. Em sucesso, publica tabela de durações e outra de alocações de thread no `GITHUB_STEP_SUMMARY` para Windows e Ubuntu.

## Fronteiras matemáticas e de desempenho

- `BLOCK_STATIONARY`: somente passagem completa sem melhoria nas duas propostas discretas; **não** equivale a ótimo contínuo, Wardrop ou equilíbrio tarifário/monetário.
- `BUDGET_EXHAUSTED`: o teste pode finalizar validando que o orçamento foi detectado e nenhuma distribuição parcial saiu; **não** significa que o cenário convergiu.
- A rotina de fases não distingue ainda custo de preparação de grafo versus Dijkstra dentro de uma etapa; isso exigirá profiler específico e só deverá ser implementado se houver justificativa no custo observado.
- O caso 24/288/320 é uma **amostra intermediária**, ainda longe de muitos milhares de trabalhadores. Crescimento superior ao linear e aumento de bytes devem orientar a próxima otimização. Não se deve reduzir artificialmente mercados, remover concorrentes, criar demanda ou mudar granularidade física para maquiar números.

Nenhuma alteração na lógica de mercado ou no host, cenas Godot, `main`, frete liquidado, preço, imposto, propriedade, tesouro ou regras econômicas. PR Draft empilhado sobre #403; sem merge. Gate só homologado após CI e publicação das métricas verificadas.
