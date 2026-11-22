# GA-Next R158 — Índice marginal de oportunidades e contrato de alocação

## Decisão arquitetural aprovada

A interface deve poder indicar **onde estão os resultados marginais menores e as melhores oportunidades já avaliadas**, sem necessariamente recomendar uma transferência individual. Só construiremos sugestões concretas de origem-destino se o custo adicional da busca e da avaliação justificar o benefício para a jogabilidade.

Isso estabelece **dois módulos independentes**, não dois cálculos de rentabilidade:

1. **Avaliador econômico marginal:** executa mudanças contrafactuais autorizadas usando o mesmo mercado real, considerando ofertas, demandas, despesas e geografia. Sua implementação evoluirá das R154–R157 e gerará evidências com um baseline físico e econômico único.
2. **Indexador marginal R158:** recebe as evidências já calculadas, ordena retornos baixos e oportunidades altas de forma determinística, devolvendo os melhores `k` itens por tipo de alteração. Nenhuma simulação, criação de pessoa, migração ou pareamento de todos os candidatos é realizada pelo indexador.

A R158 entrega o **segundo módulo**, com contratos para o primeiro. Não afirma ainda identificar a melhor profissão entre todas as cidades, pois tal avaliação completa não existe e não deve ser inventada.

## Trabalho existente e população nova: dois experimentos incompatíveis

**ExistingWorkerTransfer**: parte de um censo fixo, retira o indivíduo de sua função atual e avalia o efeito líquido de alocá-lo a outra função, no mesmo nó ou após mudança física legal. A observação deve registrar o `ExistingWorkerId`, nó de origem, nó de destino, ocupação candidata e delta monetário da economia versus o estado anterior. O custo e o tempo da viagem precisam entrar na avaliação quando se tratar de migração; **a R158 ainda não calcula viagens**.

**NewWorkerAddition**: nasce um indivíduo real em um nó autorizado, acrescentando **esforço e demanda de consumo**, e o coloca em uma atividade inicial. Não existe origem ou trabalhador anterior para retirar; observações de nascimento têm `ExistingWorkerId=null` e `SourceTile=null`. Nenhum nascimento é simulado ou aplicado pela R158.

As duas modalidades **nunca** são misturadas no mesmo ranking. O indexador também exige `BaselineIdentity` exatamente igual em todas as observações do mesmo relatório. Por isso, não é permitido comparar diretamente avaliações feitas depois de cada turno de forma independente ou reinterpretar a transferência como nascimento.

## Escala e desempenho

Para `n` observações que já foram avaliadas, a R158 faz ordenação determinística `O(n log n)`, com memória `O(n)` e retorno limitado aos `k` menores/maiores. Não existe busca `O(trabalhadores × cidades × profissões)` dentro do indexador.

A **etapa dispendiosa** é gerar avaliações contrafactuais para cada atividade e local elegível, pois o mercado é acoplado globalmente. A arquitetura não assume que avaliação e ordenação têm o mesmo custo. Futuramente poderemos amortizar a primeira etapa por atividades economicamente equivalentes em um nó, por estimativas e por recomputação parcial; não anteciparemos micro-otimizações sem medição.

## O que o ranking realmente significa

O campo `CashDeltaScaled` de uma observação de **transferência** é o *saldo econômico estimado daquela decisão candidata*, não o retorno isolado do trabalhador na ocupação atual. Portanto, o menor saldo de transferências candidatas **não equivale automaticamente ao trabalhador de menor produtividade atual**. Para exibir precisamente esse último diagnóstico, o futuro avaliador terá de calcular separadamente a perda contrafactual por retirada da atividade atual, sem pressupor destino. Sem essas evidências, a UI deve exibir somente *melhores/piores mudanças avaliadas*, nunca inventar o diagnóstico de produtividade atual.

Também não se deve misturar rentabilidade agregada de todas as civilizações com o benefício para uma única civilização: o avaliador futuro deverá especificar o beneficiário e contabilizar suas próprias receitas e transferências corretamente. A R158 não presume valores locais, nem transforma receitas em moeda liquidada.

## Decisões deliberadamente pendentes

- Criar cenários de adição legítima de trabalhadores e seus efeitos simultâneos sobre oferta e demanda;
- Calcular a produtividade marginal das **atividades atuais** de maneira comparável entre nós e civilizações;
- Avaliar tempo, caminho e custo de migração pela malha física, conservando o trabalhador durante o percurso;
- Definir o momento e a cadência de diagnóstico por cidade, além da exibição consultiva na interface da civilização;
- Decidir se o jogador habilitará execução semiautomática ou automática; o indexador nunca emite ordens;
- Implantar a demografia proposta (começo com um indivíduo por civilização e sorteios de crescimento ponderados pela população das cidades) em contrato separado, sem confundí-la com os cenários experimentais.

## Gate R158

Testar ordenação estável entre plataformas, desempates, top-k, duplicações, rejeição de baselines incompatíveis e fronteira rígida entre transferência e crescimento. Este é um **gate de infraestrutura de diagnóstico**, sem alegar novos resultados numéricos nem alterações de preço, frete, turnos, diário ou interface.
