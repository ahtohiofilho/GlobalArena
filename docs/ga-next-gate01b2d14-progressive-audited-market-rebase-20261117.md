# GA-Next — Gate 01B.2D-14: rebase auditado do mercado progressivo entre mudanças moderadas

**17/11/2026 · PR Draft sobre #406 · implementação opt-in C# · sem merge, Godot ou economia liquidada**

## Justificativa e princípios

O Gate 01B.2D-13 implementou avanço incremental **por proposta completa** e um limiar de ganho econômico sem arredondar preços, mas só retomava a pesquisa quando todos os parâmetros permaneciam idênticos. Mesmo uma atualização pequena na cotação de um mercado reiniciava o solucionador global inteiro. Este gate reduz esse desperdício **sem arredondar preços, simplificar curvas ou exigir ótimo global**.

O preço/cotação não passa a dar saltos: todos os cálculos e pontuações continuam com a precisão `BigInteger` e as micro-unidades inteiras canonizadas. A tolerância diz respeito a **quanto esforço de busca realizar**, não à precisão aritmética.

## Contratos de rebase por categoria

A entrada `BeginOrResume(..., previous)` continua congelando cotações, custos de aresta e escalas de congestionamento exatamente uma vez. Para todos os caminhos, os dados físicos de trabalhadores, a topologia Goldberg e as políticas de demanda e carga **precisam ser as mesmas instâncias imutáveis e autoritativas**. Orçamentos e limiar de aceitação também precisam coincidir; caso contrário, é executado o caminho integral original.

**1. Entrada idêntica — continuidade exata.** Quando listas, políticas e limites permanecem iguais, retorna a mesma instância `previous`, inclusive a proposta pendente, contadores e histórico do mesmo período. Não refaz o solver.

**2. Somente cotação alterada — `AuditedQuoteRevaluation`.** Exige a mesma quantidade e **ordem de mercados reais**, e igualdade de `BuyerTile`, `Buyer` e `Commodity` em cada entrada. Produção, demanda, infraestrutura, custos e escalas físicas devem continuar idênticos. Uma **auditoria física independente** revisita todas as partes de entrega do último candidato, reconcilia quantidades e cargas em arestas reais com o mapa de titularidade e preserva o candidato anterior se a prova passar. O potencial de mercado é então **recalculado com os novos valores exatos das cotações**, sem realizar a operação cara de alocação global ou gerar novas rotas.

**3. Custos físicos, tarifas, titularidade ou escalas de congestionamento alterados — `ReauditedFixedMarketRouting`.** Exige **a mesma lista física de arestas** com as mesmas identidades Goldberg, além do mesmo conjunto de mercados. A alocação anterior por trabalhador/comprador é mantida, mas `RouteFixedMarket` reconstrói caminhos sobre as **novas** estradas, custos, titulares e escalas, reaudita cargas/frete e exige conservação integral. Essa modalidade evita **o solver global** mas não evita o trabalho de roteamento. A nova pontuação usa o novo frete e as cotações atuais. Se o rebase não puder ser validado, falha fechado: não publica rotas inválidas.

**4. Mudanças estruturais — `FullGlobalSolve`.** Outro censo (inclusive mudanças de indivíduos, quantidades e posições), outra topologia, outra política, alteração da identidade/ordem dos mercados, quantidade diferente de arestas, novos IDs de aresta ou mudanças nos orçamentos exigem o planejamento global integral original. Não existe projeção fictícia de demanda nem compradores inventados.

**Importante:** o campo interno `MarketAllocation` de um candidato aquecido mantém os metadados **históricos de otimização segmentada** da época em que essa alocação foi encontrada. Eles não devem ser apresentados como ótimo do novo conjunto de cotações. A grandeza comparável ao mercado corrente é **`BestPotentialScaled`**, recalculada com os preços e custos físicos atuais. O enum `SeedKind` identifica a origem para que ninguém confunda uma resposta aquecida com uma nova otimização global.

## Separação das épocas e parada progressiva

Ao mudar cotações, tarifa ou escala, **zera-se** o orçamento consumido, contadores de propostas aceitas/rejeitadas, índice do próximo bloco, estado de convergência e histórico de pontuação; o novo histórico contém somente a pontuação do candidato revalidado naquele ambiente. Pontuações calculadas com cotações diferentes **nunca** são comparadas como se pertencessem à mesma otimização.

Depois desse início aquecido, `Advance(state, maxProposalEvaluations)` mantém a mesma sequência de propostas de mercado/rotas, aceitando somente aumentos com ganho mínimo configurado e conservação integral. Ainda é um limite de **número de propostas completas**, não SLA em milissegundos. A parada `StableUnderDeclaredGainFloor` certifica somente ausência de ganho nas duas propostas declaradas sob aquela nova fotografia e o limiar escolhido.

## Critérios de aceite

- Cenário Goldberg com produtor/duas demandas reais: cotação aumentada reusa a **mesma instância do candidato físico** e refaz a pontuação, sem solver global; recomeça a contagem em zero e não herda afirmação de estabilidade.
- Alteração física de custo + titularidade e tarifa de terceiro + escala de referência, mantendo a mesma aresta, reconstrói rotas para mercados anteriores e reaudita frete/carga; não reaproveita rota com custo desatualizado.
- Alteração de identidade/ordem de compradores ou novo objeto de censo força planejamento integral e histórico novo.
- Stress com **8 vendedores do mesmo grão, 104 mercados, 112 tiles conectados**: somente uma cotação de mercado alterada deve resultar em `AuditedQuoteRevaluation`, quantidades conservadas de `8_000_000`, frete revalidado e avanço monotônico sob uma proposta.
- Toda chamada mantém `StateHash` e snapshot autoritativo inalterados, `NoClosedCycle` nas civilizações, sem publicar vendas, tesouro ou tarifa liquidada. Testes explícitos e suíte histórica Windows/Ubuntu devem passar.

## Limites e próximos gates

A otimização aquecida não garante igualdade com o resultado de um novo planejamento integral: **a razão para continuar progressivamente é justamente não refazer toda a busca**. O novo candidato inicial deve ser válido e auditado, e o mercado pode continuar melhorando a partir dele; preço/volume ótimo e equilíbrio contínuo não são prometidos.

Esta etapa não permite reaproveitar automaticamente trabalhos de pessoas que mudaram de atividade ou tile, novos compradores, arestas abertas/fechadas ou políticas de demanda alteradas. Também não persiste o estado entre processos, não integra journal, não liquida produção nem transforma planejamento em gameplay. Para ampliar reuso entre mudanças reais de força de trabalho, precisaremos projetar uma transferência exata de quantidades, uma nova auditoria e critérios claros de fallback, em gate separado.

**Governança:** PR Draft empilhado sobre #406, sem merge nem alteração em `main` ou Godot.
