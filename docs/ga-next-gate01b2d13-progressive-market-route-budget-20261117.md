# GA-Next — Gate 01B.2D-13: otimização econômica progressiva sob orçamento explícito

**17/11/2026 · novo componente C# experimental (opt-in) · PR Draft empilhado no #405 · sem merge ou Godot**

## Diretriz de produto

O objetivo NÃO é arredondar preços, criar degraus de preço, alterar a curva de demanda ou reduzir a precisão dos cálculos. O jogo continua capaz de utilizar valores monetários fracionários em micro-unidades e funções econômicas contínuas/inteiras determinísticas.

O objetivo é **separar precisão aritmética de exatidão da otimização**. O mercado pode encontrar uma solução válida e continuar aperfeiçoando-a gradualmente, em sucessivas chamadas de planejamento, desde que o ganho econômico justifique a operação. Nenhum ciclo precisa esgotar todas as possibilidades de melhoria antes de retornar um *candidato válido e auditado*.

## Contrato opt-in — primeiro estágio de continuidade

API: `FoundationPhysicalProgressiveMarketRouteCoordinator.BeginOrResume(..., previous)` e `Advance(state, maxProposalEvaluations)`. Sem ligação ao ciclo econômico liquidado, à UI ou ao host autoritativo.

**Início:** utiliza o planejamento global/multipath conservativo existente do #400 para construir um candidato inicial. Antes de retornar, valida oferta física total igual à alocação. O score é calculado pelo **mesmo** potencial econômico integrado `BigInteger` do #401.

**Continuidade:** salva em um objeto de estado privado e imutável (sem armazenamento permanente externo neste gate) a melhor distribuição auditada, potencial, histórico de ganhos aceitos, contadores de propostas/passagens e o índice do próximo bloco. Cada chamada `Advance` executa no máximo `maxProposalEvaluations` propostas completas; uma proposta pode ser de:

1. **Mercado + rotas:** reprifica custos físicos prospectivos pelas cargas auditadas, resolve globalmente mercados por mercadoria e reconstrói as rotas sob o mesmo custo físico livre original.
2. **Rotas com mercados fixos:** revisita o multipath da alocação aceita, sem trocar compradores ou quantidade.

Uma proposta só é aceita se conservar integralmente a oferta dos trabalhadores, passar na auditoria independente de frete, **melhorar estritamente** o potencial econômico total e atingir um limiar opcional de ganho: `scoreNovo − scoreAtual >= minimumAcceptedGainScaled`. Com mínimo `0`, a política reproduz a decisão estrita do coordenador de #401. Com limiar positivo, o sistema deixa de perseguir aperfeiçoamentos economicamente pequenos dentro da política de custo declarada. **A política não arredonda números, muda preços, impede melhorias futuras em uma nova entrada de dados nem obriga patamares de reajuste.**

Após as duas propostas, uma passagem está completa. Se nenhuma ultrapassar o limiar, retorna `StableUnderDeclaredGainFloor=true`, significando **estabilidade sob as duas propostas e o limiar declarados**, não ótimo contínuo ou certeza de equilíbrio comercial. Quando o orçamento termina no meio da passagem, a melhor solução auditada e todos os metadados ficam disponíveis como **candidato de planejamento NÃO LIQUIDADO**, mas o estado não se proclama estável. A próxima chamada continuará no bloco exato em que parou, sem refazer o progresso anterior.

**Limite de custo de execução:** `maxProposalEvaluations` é contador de **propostas completas**, não um orçamento de tempo real ou de operações de CPU. Cada proposta individual ainda executa o solver global e rotas, com os respectivos orçamentos explícitos de aumentos e lotes. Portanto a granularidade mínima ainda pode ser cara, e esta etapa sozinha NÃO certifica desempenho para milhares de mercados.

## Entrada alterada: política de segurança

`BeginOrResume` congela a lista de cotações, custos físicos e escalas de referência. Só reutiliza um `previous` quando forem os **mesmos objetos imutáveis de trabalhadores, topologia e políticas**, além de exatamente as mesmas cotações, custos, escalas, orçamentos e limiar, comparados em ordem canônica. Assim pode conservar esforço entre invocações de planejamento de um mesmo estado econômico, inclusive separadas por turnos que não alterem essa fotografia.

Se trabalhadores, infraestrutura, cotações, capacidade, políticas ou orçamentos mudarem, a função **reinicia com um novo candidato inicial totalmente auditado**. Não tenta transportar uma distribuição antiga para outro mundo, nem publica saldo ou frete incorreto. A reutilização inteligente da solução anterior **sob mudanças reais** será um gate separado, com auditoria e rebase estrutural. Este primeiro incremento NÃO armazena progresso no host, journal, arquivo persistente ou rede.

## Testes de aceite

- Goldberg real pequeno com produtor e dois mercados: chamadas de **uma proposta por vez** precisam reproduzir *exatamente* os resultados, quantidades, pontuação, histórico e número de passagens da busca monolítica #401 quando `minimumAcceptedGainScaled=0`.
- Após uma chamada de mercado potencialmente aceita, o planejamento continua da próxima proposta sem perder o ganho; todo estado intermediário deve conservar bens e manter `NoClosedCycle`, `StateHash` e snapshot imutáveis.
- Com limiar alto, nenhuma melhoria pequena é aceita e a política só se declara estável **após completar as duas propostas**, não depois de uma só. Nenhum preço é arredondado.
- Mudança real de cotação ou novo objeto de censo força reinício, mesmo quando há candidato previamente aceito. A reinicialização não publica entrega parcial.
- Orçamentos inválidos e ganho mínimo negativo são rejeitados imediatamente.
- Regressão adversarial com **8 produtores do mesmo grão, 104 mercados e 112 tiles Goldberg**: uma proposta por invocação deve resultar nas mesmas entregas pai/filho, compradores, rotas e potencial final do coordenador #401, sem liquidar o comércio.
- CI Windows e Ubuntu para suite C#, Kernel, StateHash, Godot/benchmark conforme disparo.

## Governança e limites

Não existe mecanismo de preço em saltos, tampouco precisão artificialmente reduzida; a matemática anterior serve como objetivo comparativo e o ganho computacional virá de **não reiniciar as passagens** desnecessariamente e de **limitar o esforço por invocação**, e não de fingir preços de mercado já liquidados.

Nenhum estado progressivo representa venda, imposto, saque, tesouro, tarifa cobrada, ciclo fechado ou rota executada. O melhor candidato de planejamento não deve atravessar o limite de publicação econômica até que critérios de negociação, aceitação e liquidação reais sejam construídos. Invariantes físicos e conservação são exatos.

**Próxima fase natural:** reutilização segura entre ciclos com alterações reais de trabalhadores/mercados; adaptação do orçamento à capacidade do host e persistência determinística/replay do estado de planejamento; posterior otimização interna para limitar o custo de **uma** proposta. Nenhum desses passos está implicitamente homologado neste gate.
