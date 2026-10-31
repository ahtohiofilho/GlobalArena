# GA-VS46 — Auditoria independente de caixa, mercadorias e frete, sem crédito

**Data:** 31/10/2026. **Base:** GA-VS45 PR #151, head `af099c4e38f10c1271be2a4e891236db95c2975b`, 11/11 workflows aprovados. **Estado:** somente laboratório, PR draft empilhado.

## Objetivo e separação institucional

A VS43 propõe volumes/serviços; a VS44 verifica orçamentos e contabiliza fretes na exploração; a VS45 conecta a referência ao caixa V14 e à demanda proporcional `alpha`. A VS46 é a etapa **AUDIT independente** da GA-ADR-ECON-005, sem `SELECT/AUTHORIZE` nem `SETTLE`.

`ExperimentalIndependentCashSettlementAuditor.Audit` aceita a **proposta** `ExperimentalSimultaneousFreightAllocation`, as **ofertas originais** `EconomicProductionDemandResult`, catálogo de massa, linhas comerciais declaradas, caixa de abertura por comprador, preços postados congelados e política de saturação por corredor.

**Fronteira de confiança:** o auditor não executa o avaliador `ExperimentalBuyerCashSnapshot.Evaluate` e não confia em `CashLedger`, `SharedFreightCost` ou `CorridorLoads` que chegaram na proposta. Ele reconstrói os três a partir de ofertas, contas, quantidades, massa física e coeficientes previamente declarados; só depois os compara com o material submetido. Uma eventual alteração nas ofertas ou na política ainda deve ser detectada por hash/proveniência do snapshot em marco posterior. Neste momento, as linhas de serviço devem ter sido previamente verificadas pelos contratos de rede VS41/VS42.

## Escopo de auditoria

1. **Conservação física:** toda venda tem origem, destino, mercadoria e serviço declarado inequívoco; o total consumido por fonte/atividade/item não supera a oferta; o recebido por comprador/item não supera a demanda. Não aceita linhas duplicadas, remessas vazias ou serviço desconhecido.
2. **Carga por corredor:** cada remessa usa `rawQuantity × GramsPerFlowUnit` em microgramas. A soma é reconstruída independentemente por `PhysicalTacticalEdgeId + TechnologyTypeId + TechnologyCatalogVersion`; a carga declarada na proposta deve ser idêntica.
3. **Preço dos produtos:** preços ex-ante postados são obrigatórios por comprador/item. `sellerReceipt = postedUnitPrice × rawQuantity/1e6`. A função de valor marginal VS43 é **utilidade**, não dinheiro a creditar ao vendedor.
4. **Frete:** o auditor recompõe por remessa o componente geográfico/operacional VS40 (sem saturação individual) e sua contribuição exata `q_i × Q_e²` para a saturação compartilhada VS42. A soma de todos os fretes precisa ser exatamente igual ao total da proposta. Não existe dupla cobrança.
5. **Dupla entrada hipotética:** cada débito do comprador tem um crédito com mesmo valor ao **produtor do lote** (produto) ou ao **operador do serviço** (frete). Operadores são identificados por `source + destination + serviceId`, evitando misturar serviços homônimos de outras rotas. Carteiras com papéis distintos são separadas, ainda que o mesmo ponto econômico esteja dos dois lados.
6. **Sem crédito, sem dinheiro fictício:** o comprador é debitado **somente** do saldo de abertura e nunca do faturamento que eventualmente recebeu como produtor ou transportador no mesmo lote/ciclo. Nenhum comprador pode gastar `H`. Contas dos recebedores entram com saldo inicial **zero no subledger hipotético**, e sua receita é crédito, não fonte de novos fundos.
7. **Conciliação contábil:** exige exatidão de todos os saldos do `CashLedger` submetido (gross, H, disponível, compras, frete, compromisso, sobra), sem contas omitidas/duplicadas. `Σ saldos de abertura = Σ saldos de encerramento` em frações racionais exatas. Toda divergência falha fechado.

**Delimitação crítica:** os saldos `Opening` e `Closing` representam exclusivamente a **subcontabilidade hipotética das contas participantes**. Contas de produtores/transportadores começam com zero neste exercício, mesmo quando existir dinheiro de produtores em V14. Portanto o resultado **não** é uma contabilização global de `PhysicalFoodCashMarketState`, nem reconstrói todas as receitas históricas. A transformação desta auditoria em lançamentos reais exigirá mapeamento de titularidade, compatibilidade do numéraire, saldos iniciais de todos os papéis, regras fiscais e atomicidade em `SETTLE`.

## Resultado

`ExperimentalCashAuditResult` contém lançamentos de dupla entrada, saldos por conta com papéis diferenciados, visão dos compradores, total de bens, total de frete, total monetário inicial/final — sem modificar nenhum estado de jogo, hash, recibo ou arquivo de save.

A auditoria só afirma **consistência de um plano hipotético com o snapshot fornecido**. Não escolhe a melhor proposta, não calcula preço endógeno, não produz autorização, não cria infraestrutura, banco, crédito, empréstimo, garantia ou receita antecipada.

## Gates de QA

- Dois produtores, um comprador e dois serviços usando corredor comum: debitar 20 por bens e 82 por frete, creditando 10+10 aos produtores e 41+41 às operadoras, com total monetário de 102 em ambos os lados.
- Dois compradores em um corredor compartilhado: cada orçamento arca individualmente com seu custo de 51, sem transferência implícita entre famílias.
- H protegido: saldo final doméstico preserva a reserva mesmo no fechamento exato.
- Propostas com frete, carga compartilhada, quantidades, serviço ou `CashLedger` adulterados são rejeitadas sem gerar dinheiro.
- Fornecimento de caixa de abertura artificialmente baixo não é compensado por receita do mesmo lote; rejeitar.
- Independência de ordenação da entrada, uso de aritmética exata e não mutação do candidato.
- Benchmark sintético de 8/32/128 compradores com 2 débitos por comprador e corredores compartilhados; mediana 3 amostras após warmup, sem limiar de tempo.
- CI Linux/Windows, xUnit integral no Windows, Godot Mono Debug/Release, regressão de hash.

## Próximo marco potencial

`SELECT/AUTHORIZE` explícito por política institucional e `SETTLE` atômico/idempotente em contas canônicas, **somente depois** da definição de titularidade e saldo de abertura completo de todos os papéis, incluindo eventual conta estatal ou remuneração de transporte, sem mexer indevidamente nos saves existentes. Antes disso, permaneça opt-in.
