# GA-VS54 — Reconciliação do frete físico compartilhado com poder de compra abstrato

**Base:** GA-VS53 PR #160, head `bb255c534aa20074f3edfd92f094363d447fd67b`, 19/19 workflows aprovados; PR **draft empilhado**. `main` não modificada.

## Objetivo

Comparar uma seleção hipotética de entregas comerciais com custos **efetivamente calculados pela função de frete da VS42**, já incluindo geografia, custo operacional e saturação agregada de corredores, convertendo o total para a **mesma unidade abstrata** de poder de compra definida na VS53. A palavra efetivo refere-se ao *custo calculado da configuração selecionada*, **não** a desembolso, liquidação, deslocamento de carga, rota construída validada nesta classe ou preço de equilíbrio.

O projeto conserva população como produtores e consumidores, poder de compra agregado sem carteiras privadas, `H` como proteção de consumo invisível e logística sem titular financeiro presumido.

## Implementação

`ExperimentalAbstractPhysicalFreightReconciler.Project` recebe projeção física VS52, ofertas/demandas, catálogo de massa por mercadoria, preços e estimativas ex ante VS53, política de conversão versionada, unidade nominal declarada do custo VS40–VS42, **ofertas integrais com objetos de serviço** já verificados na cadeia VS41–VS42, entregas selecionadas, política de congestionamento compartilhado.

Validação estrita:
- unidade de frete nominal declarada precisa ser igual ao `NominalQuoteUnitId` da política VS53; o próprio modelo VS40 **ainda não traz um campo monetário nativo**, portanto este ID é uma atestação **do chamador**, não prova criptográfica da escala;
- VS53 é **recomputada a partir dos dados originais** em vez de confiar em um resultado parcial;
- ofertas físicas integrais devem casar com as identidades `Source/Destination/ServiceId` expostas na VS52, sem serviços extras, faltantes ou repetidos;
- **o serviço selecionado tem de ser a mesma instância do objeto da oferta** para a mesma origem, destino, mercadoria e atividade; reutilizar o mesmo `ServiceId` com tarifação ou caminho diferentes é recusado;
- lotes com IDs distintos, quantidades positivas, oferta e demanda preexistentes; nenhum fornecedor nem consumidor pode ultrapassar seu volume declarado;
- massa física é **derivada do catálogo**, via `RawQuantity × GramsPerFlowUnit` em microgramas, sem carga fabricada pelo chamador nem confusão com pontos militares de movimento.

**Origem canônica da cobrança:** `ExperimentalSharedFreightBatchCost.Evaluate` da VS42, para o conjunto inteiro de lotes escolhidos. A repartição por lote usa `q_i/Q × C(Q)`, com frações exatas, isto é, a penalidade `weight × q_i × Q² / (1e6 × scale²)`. A **saturação standalone da VS40 não é adicionada**: preserva-se apenas a parcela sem congestionamento da VS40 e a penalidade conjunta da VS42 **uma vez por corredor**.

A soma das parcelas logísticas nominais deve igualar exatamente o custo total VS42; a mesma igualdade é revalidada depois de multiplicar **todos** os fretes pelo fator único da VS53. Mercadorias também utilizam a escala da VS53. Para cada comprador, computa-se despesa estimada das entregas selecionadas, despesa física recalculada, total por bem/frete, cobertura hipotética com `H` protegido e saldo **fictício apenas de projeção**.

`CanAuthorizeRealSettlement` é **sempre false**, inclusive se o lote for vazio ou se a despesa puder ser coberta. Nenhum `SETTLE` real, crédito, transferência ao vendedor/transportador, emissão monetária, alteração de mundo, save ou UI.

## Exemplo de referência

Duas mercadorias, um povoamento com oito trabalhadores, um comprador, duas cargas de 1 grama, mesmo corredor com escala de saturação 1 g e peso 1, custo geográfico/operacional nominal de 1 por carga, preço nominal por bem de 1, previsão de frete nominal de 1 por carga:

| Grandeza | Valor nominal, fator VS53 = 1 |
| --- | ---: |
| Carga total do corredor | 2 g |
| Frete base (1 + 1) | 2 |
| Saturação compartilhada **uma única vez** | 8 |
| Frete físico recalculado | 10 |
| Bens selecionados | 2 |
| Custo entregue conjunto | 12 |
| Poder de compra abstrato bruto | 40 |
| H | 5 |
| Disponível | 35 |
| Consumo restante hipotético | 23 |

Se as cargas passarem **por corredores distintos**, o mesmo teste gera frete nominal 4, não 10. Se o fator de conversão VS53 for 4, o frete da configuração compartilhada sobe para 40 unidades abstratas, bens para 8, despesa entregue para 48 e o comprador **não** consegue cobrir seus 35 disponíveis. A operação continua uma hipótese explicitamente **não autorizada**, sem truncamento financeiro silencioso.

Com dois povoamentos, 6 e 2 trabalhadores físicos, as parcelas de frete compartilhado são atribuídas individualmente e o comprador com orçamento menor pode falhar enquanto o outro permanece solvente, sem compensação de caixas entre localidades.

## Escopo e limitações

- Esta fase **não encontra** a configuração ótima nem escolhe quais lotes aceitar: avalia um **lote já selecionado** pelo chamador, com os serviços oferecidos e as quantidades finitas.
- Não substitui a validação de construção/continuidade/acesso de `ExperimentalSharedFreightOption.FromBuiltOffer`. As opções `Synthetic` existem exclusivamente para testes e benchmarks. A classe não autentica que um objeto de serviço é construído.
- Os custos são da VS42 para a carga agregada do lote; futuras decisões de alocação **podem mudar Q e as tarifas**. Uma nova alocação exige nova projeção/auditoria.
- O custo projetado não equivale a uma receita logística, e o serviço segue sem um proprietário institucional estabelecido. Nenhum débito/receita financeiros é criado.
- Nem `H` nem poder de compra abstrato são saldos bancários ou dinheiro persistente.
- Nenhum modelo de salário, crédito, juros, imposto ou acumulação privada é introduzido.
- O benchmark repete 8/32/128 avaliações sobre **um comprador, dois lotes e um corredor**, não 128 mercados nem 128 compradores.

## Testes de aceite

- compartilha saturação e não duplica componente VS40;
- corredores independentes custam menos quando a carga está dividida;
- quantidades fracionárias conservam massa e custos com racional exato;
- dois compradores têm orçamentos e lançamentos distintos;
- elevação da escala pode produzir inviabilidade sem liquidação;
- rejeita falta de cobertura, serviço desconhecido, serviço de mesmo ID com outro objeto/caminho, sobreoferta, overdemand, unidade de frete incompatível, lotes duplicados;
- ordenação determinística e lote vazio somente leitura;
- hash/save/caixa V14 íntegros;
- CI dirigida Linux, suíte integral Windows, benchmark e Godot Debug/Release.

## Próximo gate

Atribuir identidade econômica **institucional e auditável** aos serviços e aos beneficiários do custo logístico, sem criar dinheiro privado individual. Antes de qualquer `SETTLE`, é necessário auditar a oferta construída, a seleção simultânea, as unidades do custo real e a repartição da receita, vinculando os respectivos snapshots à mesma revisão do mundo.
