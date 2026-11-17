# GA-Next — Gate 01B.2D-15: custo observável de propostas econômicas progressivas

**17/11/2026 · PR Draft empilhado sobre #407 · instrumentação de teste/CI apenas · sem merge e sem liquidação econômica**

## Decisão de produto

A matemática de preço, demanda, frete quártico e tarifas permanece intocada. A busca pode preservar um candidato fisicamente auditado e prosseguir em propostas posteriores; o limite de N propostas não é um limite em milissegundos. Antes de propor interrupção *dentro* de um fluxo/roteamento, este gate mede onde estão os custos reais da execução. **Nenhum SLA de CPU/memória é declarado nesta etapa.**

## Recorte de benchmark

O teste ProgressiveCostProfileSeparatesProposalsWarmRebasesAndColdSolves reutiliza a fixture da suíte adversarial com **112 tiles Goldberg conectados, 8 produtores físicos, 104 mercados demandantes reais e 8.000.000 micro-unidades de bens produzidos**. Um mesmo censo, topologia e políticas de demanda/carga é empregado durante as etapas. Cada observação utiliza Stopwatch.GetTimestamp/GetElapsedTime e GC.GetAllocatedBytesForCurrentThread na thread da operação medida.

As nove operações observadas, com registro separado, são:

1. **initial-cold-solve:** primeiro planejamento global sem estado anterior.
2. **market-proposal:** avanço exato de uma proposta do bloco de mercado.
3. **route-proposal:** avanço exato de uma proposta do bloco de rotas.
4. **quote-warm-rebase:** nova cotação preservando candidato físico revalidado.
5. **quote-cold-solve:** planejamento integral com as mesmas novas cotações do warm.
6. **post-quote-market-proposal:** proposta após reprecificação; ganho de potencial comparado apenas dentro da mesma época.
7. **road-warm-reroute:** muda custo, titular, tarifa e capacidade de arestas existentes e reaudita rotas do mesmo mercado.
8. **road-cold-solve:** planejamento integral com as mesmas novas estradas, tarifas e cotações do warm.
9. **post-road-market-proposal:** proposta no novo contexto físico; ganho medido na mesma época.

Todos os estágios precisam conservar os bens disponíveis/alocados e as duas civilizações permanecem sem LastClosedCycle. O StateHash e o objeto de snapshot autoritativo não mudam. Há verificações explícitas do reuso de candidato de cotação, reuso de alocação na mudança de frete e proveniência SeedKind; o relatório contém somente planos não liquidados.

## Saídas

No Windows e no Ubuntu, o workflow ga-foundation-physical-worker-cycle.yml executa exclusivamente a regressão de custo e salva progressive-cost-104.json como artefato por runner, com schema **ga-next.progressive-cost-profile.v1**. O script PowerShell GA-Next-Progressive-Cost-Report.ps1 valida obrigatoriamente o schema, as nove fases distintas, os quantitativos físicos, os limites de publicação e observações finitas/não negativas. O GitHub Step Summary apresenta cada tempo, alocação e ganho de potencial calculado para propostas da mesma época.

As medições **não** exigem que a solução warm seja igual à solução cold: são custos para gerar dois candidatos válidos sob o mesmo input, não provas de ótimo nem de equivalência das alocações. O possível benefício de reutilização será avaliado com esses números e com a eventual diferença de qualidade econômica do warm.

## Significado e limitações

- **Tempo:** duração de parede observada, não tempo puro de CPU. Há variação de escalonamento, JIT e ruído do runner.
- **Memória:** bytes gerenciados acumulados na thread, não RAM máxima, heap retido ou memória nativa.
- **Amostragem:** uma observação por estágio e runner, sem significância estatística. Não declarar aceleração, ganho marginal por CPU ou latência garantida; nenhuma regressão falha por ser mais lenta.
- **Ganho econômico:** o potencial BigInteger é convertido para texto, preservando pontuação exata; diferença de pontuação é informada somente entre Advance e o estado imediatamente anterior da mesma época, nunca entre cotações/estradas diferentes.
- **Escala:** a fixture tem 112 tiles, NÃO milhares. Os testes anteriores já incluíram cenários maiores, mas não cabe inferir um orçamento para mundos grandes deste recorte.
- **Motor:** instrumentação no teste, não no ciclo de produção. Nenhum main, Godot, simulação, regra tarifária, liquidação ou contrato de estado foi modificado.

## Gate de decisão seguinte

Depois de obter artefatos em ambos os sistemas: revisar a etapa dominante em tempo/alocação, distinguir ruído do runner de custo intrínseco, repetir medições em tamanho maior sob orçamento supervisionado, e só então priorizar cache físico, cortes de busca ou granulação subproposta. Preservar determinismo, justiça econômica e conservação. A meta é interromper computação de pouco retorno, **não arredondar preços ou mudar a matemática econômica**.

## Governança

Draft sobre #407, sem merge automático. Aprovação do gate requer CI verde para o HEAD e relatórios presentes no GitHub Step Summary e artefatos do run.
