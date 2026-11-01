# GA-VS74 — Preflight físico de uso local e frete fixo

**Status:** implementação de laboratório estritamente *read-only*. Base: VS73 PR #180, SHA \`d398d120640b1e09e89a7283ccd47932a33db074\`, homologado 39/39. PR VS74 empilhada sobre VS73 em Draft; sem merge na main.

## Motivação e escopo

GA-ADR-ECON-006 (proposta, issue #181) contém duas decisões econômicas ainda ABERTAS:

- D1: a autoprovisão local consome orçamento nominal/abstrato, ou existe provisão primitiva sem compra?
- D2: usar a pontuação do laboratório VS66 como critério social definitivo, ou desenvolver política específica após validação?

A VS74 **não responde a D1/D2 por implementação**: adiciona somente um preflight de conservação física, sem preços ou arbitragem de ótimo. Não cria um "mercado local", um concorrente para o seletor VS66, nem atribui prioridade ao abastecimento local.

## Funcionamento

Ponto de entrada: \`CanonicalPrimitiveSourceReservationDiagnostics.PreviewJointPhysicalUse(scenarioId, proposedLocalUses)\`.

\`scenarioId\` deve ser ID autêntico do VS71 (baseline ou regional-productivity). O frete selecionado, com suas autorizações, custos de corredores e H protegido, é obtido **somente** do recibo de seleção VS71/VS73 e permanece fixo como hipótese; não é fornecido pelo chamador.

Cada \`CanonicalPrimitiveLocalUseProposal\` identifica um povoamento/tile existente \`Point\`, uma \`Commodity\`, uma \`Activity\` e \`RawQuantity\` positiva. O produtor local e o comprador são o MESMO povoamento. O inventário físico é a descoberta VS62/VS72, sem fundar cidade nem exigir infraestrutura arcaica.

A VS74 audita a soma hipotética:

\`\`\`text
Para cada (origem, commodity, atividade):
   saída_frete_selecionada + atendimento_local_proposto
        <= produção_física_real_do_ciclo

Para cada (comprador, commodity):
   entrada_frete_selecionada + atendimento_local_proposto
        <= demanda_física_do_ciclo
\`\`\`

Estes limites são independentes entre produtos (sem somar madeira, pedra e alimento) e preservam a dimensão por atividade na fonte. Valores inválidos são rejeitados; não há quantidades negativas nem truncamento de excesso. O máximo de 12 propostas locais mantém o domínio limitado; a API permite proposta vazia, para auditar o cenário só com frete.

Recibo \`CanonicalPrimitiveJointPhysicalUsePreview\` inclui linhas \`Sources\` (oferta física, saída por frete, local proposto, saldo físico hipotético) e \`Buyers\` (demanda física, entrada por frete, local proposto e demanda física não atendida). Todos os valores são **por mercadoria**, por atividade onde aplicável. \`ProposedLocalUses\` é copiado e ordenado canonicamente.

Validações: \`RequireCurrent()\` rejeita outro objeto de sessão, restore, mudança de rede/trabalhadores e demais recibos obsoletos; \`VerifyIndependent()\` reexecuta a verificação das dependências e reconstrói as contas físicas exatas, sem confiar na apresentação anterior.

## Limitações explícitas

- \`IsPhysicallyFeasible=true\` **significa só viabilidade física** da proposta e frete fixo auditado.
- \`IsFreightBudgetIndependentlyAudited=true\` significa que a seleção de frete herdada já preserva H/limites de preço/corredores; **não** que o atendimento local caiba no orçamento.
- \`IsLocalPurchaseBudgetEvaluated=false\` SEM EXCEÇÃO. Nenhuma hipótese D1 implícita: não há "local grátis" nem cobrança nominal.
- \`CanAuthorizeRealSettlement=false\`, \`IsActualConsumption=false\`, \`IsCommitted=false\`, \`IsPersistentReservation=false\`.
- O preflight **não é o seletor simultâneo final**: em particular, não reotimiza fretes quando o usuário altera a proposta local, não decide que uso vence e não calcula uma medida social de demanda satisfeita.
- As hipóteses baseline e regional continuam mutuamente excludentes.

## QA e desempenho

Seis testes: recibo vazio preserva exatamente o frete original; local elegível com frete economicamente inviável; rejeição de violação de capacidade/demanda e entradas falsas; determinismo/imutabilidade; múltiplos corredores; invalidação de recibos após restauração ou atualização de rede.

Benchmark \`--vs74-joint-physical\` repete 8/32/128 consultas de preflight **vazio** sobre uma malha pequena e fixa, com verificação completa, sem alegar performance em mapas grandes. O teste de uso local positivo é coberto pela suíte xUnit.

Gate CI Linux/Windows, regressões VS40–VS74, Godot Mono. Homologar somente todos os workflows do SHA final em \`completed/success\`.

## Limites de escopo e próximo gate

Proibido introduzir uma carteira nova, preço de autoprovisão não autorizado, prioridade local/exportação, critério social definitivo, crédito, dívida, estoque persistente, contratos, novas rotas, nova cidade, UI, alteração de turnos ou SETTLE.

**Próximo gate conceitual:** D1 e D2 da issue #181. Até deliberação, o preflight VS74 não deve ser promovido a consumo real nem convertido em otimizador econômico.
