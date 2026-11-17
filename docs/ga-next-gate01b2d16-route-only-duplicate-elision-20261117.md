# GA-Next — Gate 01B.2D-16: eliminação auditável de recálculo de proposta de rota idêntica

**17/11/2026 · Draft empilhado sobre #408 · implementação C# opt-in · sem merge, Godot ou liquidação econômica**

## Hipótese e evidência anterior

O [Gate 01B.2D-15](ga-next-gate01b2d15-progressive-cost-observability-20261117.md) mostrou que a avaliação independente de **route-proposal** levou ~279 ms no Windows e ~370 ms no Ubuntu no cenário de **112 tiles Goldberg conectados, oito trabalhadores produtores e 104 mercados reais**, sem ganho aceito de potencial. O programa executava `RouteFixedMarket` duas vezes com o **mesmo mercado aceito, censo, topologia, políticas, estradas, escalas e budgets**. Como esse método é determinístico, a segunda chamada não deveria mudar a distribuição física nem melhorar a função objetivo.

A hipótese não autoriza arredondar preços, alterar o frete quártico nem ignorar uma proposta com inputs distintos. É uma eliminação de chamada **redundante** com prova de equivalência, não um novo critério econômico.

## Contrato da mudança

No `FoundationPhysicalProgressiveMarketRouteCoordinator.Advance`, o bloco de rota com índice 1 reutiliza diretamente o candidato imutável e auditado da época corrente. É seguro nesta arquitetura porque os três caminhos que criam um candidato nesse coordenador passam por `RouteFixedMarket` sob os inputs congelados: inicialização `Plan`, proposta de mercado aceita e rebase auditado de cotações ou remessa recalcada em infraestrutura alterada.

A proposta idêntica **continua consumindo uma posição no orçamento de propostas**, é contabilizada como rejeitada e não acrescenta entrada ao histórico de melhora. A passagem e o certificado `StableUnderDeclaredGainFloor` preservam a semântica original. A pontuação é reusada sem repetir sua avaliação matemática, pois o candidato e as cotações são idênticos. **Não se suprime** um solver global para mudanças no mercado: o bloco 0 executa o mesmo `Coordinate` e novo `RouteFixedMarket`, sem alteração. A primeira rota do novo ambiente físico também continua reprocessada e reaudita integralmente via `BeginOrResume`.

`AdvanceWithOriginalRoutingForTests` é um ponto de referência interno, exclusivamente para comparar a versão original que **recalcula sempre a rota do bloco 1**, sem expor outra política na interface pública. O coordenador integral também não foi alterado.

## Critérios de aceite

- A/B **passo a passo com 1 proposta por chamada** no Goldberg pequeno: mesma pontuação exata, melhora aceita, rejeições, índice do próximo bloco, número de propostas, passes, certificado de estabilidade, histórico monotônico, quantidades e trajetos físicos e custo total de frete.
- Mesmo A/B em **limiar de ganho positivo**, reprecificação sob mercados idênticos e mudanças auditadas de custo/tarifa/titularidade/escala de aresta.
- A/B no Goldberg maior com **8 produtores, 104 mercados reais e 8.000.000 micro-unidades**: entrega de rota do método original comparada à otimizada; mesmo candidato econômico, mesmas rotas e cargas, `StateHash` intacto e `NoClosedCycle` para as duas civilizações.
- Benchmark `route-elision-104.json` com schema `ga-next.route-elision-ab.v1`, tempo de parede e alocação gerenciada por thread da chamada original e otimizada. Script PowerShell valida a prova, publica resumo e o workflow guarda os artefatos por plataforma.
- Os testes históricos de regressão Windows e Ubuntu precisam passar; manter o PR em Draft e a `main` intocada.

## Gestão dos riscos

A equivalência depende da propriedade explícita de **determinismo de `RouteFixedMarket` para dados imutáveis**. Não generalizar a otimização se esse método vier a ganhar busca estocástica, heurísticas dependentes de histórico, tarifas mutáveis ou outros efeitos colaterais. Em futuras alterações, revalidar os testes A/B e, se necessário, passar a sinalizar proveniência por candidato.

**Medição:** Stopwatch mede tempo de parede, não CPU exclusiva; `GC.GetAllocatedBytesForCurrentThread` mede alocações cumulativas gerenciadas, não RAM máxima. Amostra única por plataforma; não declarar SLA ou estatística generalizável. Não há compromisso com ganhos em milhares de nós neste gate.

## Seguimento

Usar os resultados para avaliar se o próximo gargalo é a própria **proposta global de mercados** e seu reroteamento integral. Qualquer nova interrupção subproposta deve preservar a conservação dos bens e a admissibilidade econômica de planos intermediários. O trabalho aqui é um corte de duplicação comprovável, não uma mudança na matemática.

**Governança:** PR Draft empilhado sobre #408, sem merge automático e sem liquidar ciclos de mercado.
