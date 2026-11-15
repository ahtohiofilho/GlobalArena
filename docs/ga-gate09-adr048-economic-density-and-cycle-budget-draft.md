# Global Arena — ADR-048: densidade econômica e orçamento reservado por ciclo

**Situação:** incremento experimental, gate condicionado à CI.  
**Data:** 15/11/2026.  
**Base:** ADR-047 PR #336, ADR-046 PR #335, ADR-042 verificador independente.  
**Governança:** PR Draft empilhado, sem merge, sem alteração do motor C#/Godot e sem ratificação F-059.

## Objetivo de produto

O Global Arena terá **muitos milhares de nós e comércio complexo**. A validação de 2.562 nós no ADR-047 demonstrou possibilidade de equilíbrio para **48 produtores e 24 mercados**, mas não estabeleceu escalabilidade quando o número de agentes e alternativas comerciais crescer.

**Preservar** a formulação econômica original: preço de mercado P=sqrt(D/Q), custos físicos c_free×(1+(B/K)^4), tarifas específicas do vendedor, produto vendido integralmente, F084F e tolerância comercial F072 global E pior grupo <=1e-4. Não reduzir o expoente de congestionamento como atalho. Quando a engenharia de desempenho e os budgets reais demonstrarem uma restrição, avaliar ajustes econômicos somente por decisão específica posterior.

O jogo terá outras camadas com orçamento de CPU: movimentação, militar, política, infraestrutura e apresentação. O cálculo de equilíbrio **não precisa coincidir com cada frame**. Contudo, este PR **não implementa threads, cancelamento, corrotinas, continuação de Newton ou scheduling Godot/C#**. Ele estabelece um primeiro **contrato de admissão de orçamento e medição**, para orientar essa implementação futura sem alegar resultados não medidos.

## Matriz de densidade — não confundir nós com empresas

Todas as fixtures derivam do gerador determinístico original R13, com rotas válidas sobre malha Goldberg e preços e tarifas originais. Varia-se a quantidade de produtos, grupos produtores e mercados; não a lei de oferta/demanda, tarifa, curva de custo ou coeficientes de congestionamento.

| Identificação | Goldberg | Nós | Arestas | Produtos | Grupos físicos | Mercados | Candidatas grupo×destino |
|---|---:|---:|---:|---:|---:|---:|---:|
| Baseline ADR-047 | m=16 | 2.562 | 7.680 | 4 | 48 | 24 | 288 |
| **Densidade I (CI)** | m=16 | **2.562** | **7.680** | 4 | **96** | **32** | 768 |
| **Densidade II (extended)** | m=16 | **2.562** | **7.680** | 6 | **144** | **48** | 1.152 |
| Geografia extra (perfil opcional) | m=24 | 5.762 | 17.280 | 6 | 144 | 48 | 1.152 |

As candidatas são possibilidades canônicas por produtor e mercado de sua mercadoria, NÃO um total de rotas simultaneamente financiadas. O conjunto ativo do ADR-047 começa com rotas financiadas e duas candidatas baratas por grupo, acrescentando caminhos economicamente dominantes por separação global.

**Limitação assumida:** 144 produtores em 2.562/5.762 nós ainda NÃO é um mundo com milhares de empresas. O gerador R13 congelado limita o catálogo sintético a 240 grupos e 120 mercados; expandi-lo seria trabalho separado com suas próprias garantias.

## Método implementado

Novo runner somente laboratório: experiments/gate09/adr048_density_budget/ci/run_adr048.py

- Usa DIRETAMENTE o solver ADR-047 como está, com exatamente a mesma VI de H, pré-condicionamento por bloco de produtor, operação Jv/Jtv e auditoria completa ADR-042. Nenhuma heurística nova de preço, estoque, demanda ou transporte.
- Recria cada cenário de forma independente via R13, registra fingerprint de tarifas, custos, capacidades, produção, mercados e grafo e confirma que nem fluxos nem regras econômicas são mutados.
- Mede tempo observado de construção da fixture, tempo do solver em Python, avaliações H, rotas, Krylov, CG, blocos, número de colunas ativas e orçamento esgotado. Uma CI verde no experimento NÃO transforma automaticamente uma fixture NO-GO em equilíbrio.
- **PASS de economia exclusivamente após certificação independente ADR-042**: preços finitos de todos os mercados alcançáveis, gaps F072 <=1e-4 GLOBAL e pior grupo, rotas globais verificadas, R13/R14, F084F e oferta conservada.
- Um estado provisório eventualmente examinado pelo ADR-042 nunca retorna como fluxo final: erro global e erro do pior grupo provisórios são campos explicitamente separados. Não inventar valor numérico para erro final se a solução não foi encontrada.
- Todos os limites de trabalho são declarados antes do teste em unidades separadas: H completo, scans por caminho, certificados originais, colunas, gerações, passos Newton, produtos Krylov, iterações CG e coeficientes de blocos.
- Um NO-GO de densidade é resultado operacional **publicado** mesmo quando testes de contrato passam; o relatório terá as chaves static_original_F072_go e numerical_status inequívocas.

### Budgets de pesquisa — não limites canonizados do jogo

| Unidade | 96 grupos / 32 mercados | 144 grupos / 48 mercados |
|---|---:|---:|
| H completas | 740 | 820 |
| Scans | 2.600 | 4.300 |
| ADR-042 completas | 6 | 6 |
| Colunas ativas | 800 | 1.150 |
| Gerações globais | 6 | 6 |
| Newton aceitos | 165 | 175 |
| Produtos Krylov | 48.000 | 56.000 |
| CG por direção | 140 | 160 |
| Coeficientes de bloco | 220.000 | 245.000 |

O maior budget não significa SLA de computador pessoal e não garante convergência. A matriz de experimentos registra cada limite consumido.

## Reserva de CPU para outros sistemas — apenas contrato de planejamento

O contrato CycleReservation recebe dois parâmetros EXPLÍCITOS do caller: tempo total nominal disponível ao ciclo econômico + outras camadas e tempo reservado para as demais camadas. O orçamento computacional nominal da economia é a diferença.

O verificador model_cycle_reservation, SEM executar o jogo, produz três classes:
- ADMISSIBLE_FOR_HYPOTHETICAL_PERIODIC_CYCLE apenas se o estado já foi aprovado pelo ADR-042 e o tempo observado ficou dentro do orçamento **hipotético**;
- DEFER_CERTIFIED_SNAPSHOT_AS_TOO_COSTLY_FOR_PLAN quando a solução é economicamente correta, mas excede a margem nominal;
- BLOCK_UNCERTIFIED_ECONOMIC_SNAPSHOT quando falta certificado, mesmo que o processamento tenha sido rápido.

Os exemplos de 10/60/120/300 segundos no relatório servem apenas como **análise de sensibilidade** do tempo do executor Python. Não canonizam uma taxa de turnos, CPU máxima, cadência de frames, capacidade do aparelho do jogador ou compromisso de desempenho.

**Não existe preempção nesse código.** O solver ainda roda até seu teto interno; o gate de tempo decide ex post se a solução caberia em um orçamento hipotético. Uma implementação real de limite de CPU teria que aceitar unidades cooperativas de trabalho interrompíveis e retomar o mesmo estado da VI em ciclos, com consistência de snapshot e replay, sem jamais liberar estado parcialmente solucionado.

A interface gráfica Godot deve permanecer independente desse trabalho; a CPU reservada para visual e próximas camadas deve ser validada em perfil de execução C# futuro, não presumida pelos tempos do CI Python.

## Plano de aceitação deste incremento

**Contrato obrigatório:** 17 testes cobrindo parametrização real R13, fingerprint físico, SHA16 da linha-base, tolerância, orçamento numérico finito, reserva para outras camadas, proibição de falso PASS e distinção de nós versus agentes.

**Escala I obrigatória na CI:** 2.562 nós, 96 grupos e 32 mercados. CI preserva o resultado F072 GO/NO-GO em JSON/ZIP e mostra a contabilidade de trabalho. Por ser a primeira execução de densidade, o NO-GO não invalida a integridade do runner, mas é um bloqueio técnico para produção; não anunciar CI verde como escalabilidade aprovada.

**Escala II e geografia ampliada:** perfis extended/geography separados. Realizar apenas se o custo e as evidências do primeiro cenário justificarem, com tetos explícitos. Não criar cenários mais densos por inferência estatística a partir de uma topologia com poucas empresas.

**Próximas etapas possíveis:** evolução esparsa da gestão de rotas, filtros de oportunidades com prova de separação, warm-start entre ciclos econômicos e execução cooperativa por orçamento, sempre exigindo auditoria comercial original. Antes do C#/Godot, medir densidade realista, variância de tempo, pressão de memória e replay determinístico.

## Governança

O PR não modifica R13, ADR-042/043/044/045/046/047, regras F072/F084F, tarifação, F059, C#/Godot ou main. Permanece em Draft e empilhado sobre #336. Nenhum merge é autorizado por uma CI verde.
