# GA-VS44 — Orçamentos de consumidores e compra à vista, referência experimental

**Data:** 31/10/2026. **Base empilhada:** GA-VS43 PR #149, `0d9ab99`, 9/9 workflows aprovados. **Governança:** branch e PR em draft; `main` e saves intactos.

## Objetivo e decisão arquitetural

Extender a referência de quantidades e serviços VS43 para que a **disponibilidade monetária dos compradores** limite uma operação mesmo que existam oferta física, demanda e rotas viáveis. O jogo mantém **comandos simples**; contas e logística ficam no motor. Não importar uma nova rede de bancos/crédito, não ampliar microgerenciamento militar e não alterar a economia em produção.

`ExperimentalBuyerCashSnapshot` é um **snapshot opt-in**, explicitamente fornecido a `ExperimentalSimultaneousFreightReference.Compare(..., cashSnapshot: ...)`. Sem esse argumento, **os resultados e contratos VS43 permanecem inalterados**.

## Contratos monetários ex-ante

- `ExperimentalBuyerCashBudget` — um orçamento monetário bruto **congelado** por `EconomicPointId`, e uma reserva **H protegida** que não pode ser usada nas compras explícitas. O saldo efetivamente disponível é `grossCash - protectedResidual`, com racional exato e saldo nunca negativo. H é consumo residual/numéraire, **não** conta bancária.
- `ExperimentalBuyerCommodityPrice` — preço monetário **postado** de cada item para cada destino, também ex-ante, em unidades por **unidade integral de fluxo** (quantidade `RawQuantity/1e6`); não é o mesmo contrato que `EconomicTurnCommodityValueOffer`, que representa valor marginal integrado **na função-objetivo**, não preço de compra.
- `ExperimentalBuyerCashDelivery` — remessa planejada e já associada a uma oferta física existente, com quantidade comercial, carga física em microgramas e serviço. Não é uma unidade de transporte em tempo real.
- `ExperimentalBuyerCashLedger` — relatório informativo, sem mutação de mundo, com orçamento bruto, H, gasto com mercadoria, frete alocado, compromisso total e saldo de caixa disponível. A ledger detalhada fica em `ExperimentalSimultaneousFreightAllocation.CashLedger`.

A política de **incidência do frete** adotada no laboratório é explícita e provisória: o **comprador paga preço do item + frete integral entregue**. Podemos estudar incidências diferentes antes de canonizar; não somar gastos de ambos os lados nem supor que receita de produtores financie a compra no mesmo ciclo.

## Frete compartilhado, sem dupla cobrança

A VS43 já calcula o frete global VS42 com saturação em cada corredor como `weight × Q_e³ /(1e6 × scale²)`. A VS44 **não cria outro frete**: apenas reparte exatamente esse mesmo gasto aos compradores que o provocaram, proporcionalmente às respectivas cargas físicas em cada corredor:

```
frete_entrega_i =
  frete_geografico_operacional_i
  + Σ_{e usado por i} weight × q_i × Q_e² / (1e6 × scale²)

Σ_i frete_entrega_i == frete_global_VS42
```

As igualdades são racionais exatas com `BigInteger`, mesmo quando vários produtos e compradores compartilham o mesmo corredor. O custo individual de saturação VS40 **continua excluído**, como na VS42/VS43. Não acrescentar capacidade rígida ou taxa separada de movimento militar.

## Restrições

Para comprador `b`, e cada hipótese inteira de quantidades/serviços:

```
Σ_(entregas a b) [ postedPrice_(b,item) × rawQty / 1e6
                    + frete_entrega ] <= grossCash_b - H_b
```

O orçamento é **compartilhado entre produtos**, não um orçamento separado para cada alimento/minério. Se uma hipótese não couber, toda aquela configuração é inviável — inclusive na busca independente, na heurística coordenada e na enumeração exata. **Não há saldo devedor, empréstimo, receita antecipada, transferência implícita nem crédito de outro turno**.

A heurística local continua sem prova de ótimo global. A enumeração exata fornece auditoria independente apenas abaixo do limite explícito `maxExactConfigurations`. O objetivo da VS43 (valor integrado menos custo de produção e frete compartilhado) permanece separado dos **preços postados que limitam caixa**. Essa separação evita confundir utilidade/demanda com transferência monetária de compras, porém **não resolve ainda** clearing simultâneo completo de preços endógenos.

## QA

1. Orçamento que paga 1 unidade mas não 2; compras exatamente no limite são permitidas, sem saldo negativo.
2. Reserva H protegida, independentemente de mercado; zero caixa implica nenhuma compra mesmo que a mercadoria tenha grande valor marginal.
3. Duas mercadorias concorrem pelo mesmo orçamento total, inclusive quando chegam de fontes diferentes.
4. Frete entregue soma exatamente ao frete VS42, inclusive saturação compartilhada por compradores distintos e pela mesma infraestrutura.
5. Ausência de budget snapshot mantém paridade integral com VS43; identidades, preços duplicados ou ausentes falham fechado.
6. Resultado determinístico sob inversão de ordem de fornecedores/mercadorias.
7. Regressões xUnit Linux/Windows, Godot Mono Debug/Release, benchmark sintético com múltiplos compradores, documentação de escopo e nenhum efeito sobre save/hash/journal.

## Benchmark

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs44-cash-budget
```

Usa 4, 8 e 12 linhas, quatro compradores, ofertas fixas, preço mercantil e orçamento por comprador, corredores compartilhados e alternativas viárias. Mede mediana de tempo/alocações e número de alternativas examinadas após aquecimento, sem thresholds artificiais. A solução exata é limitada ao cenário pequeno. Não alegar ganhos percentuais na economia de uma partida sem benchmarks sobre WorldGen 01C e produção populosa reais.

## Escopo futuro explicitamente separado

- **Calibração canônica do dinheiro:** ligar a referência financeira a orçamentos de renda/produtividade por trabalhador e ao caixa local versionado do sistema V14; evitar misturar microcréditos persistidos com numéraire de laboratório sem taxa/escala explícita.
- **Preços endógenos e elasticidade:** evoluir curva de preços e demanda, incluindo `alpha` para preservar H quando múltiplos bens são introduzidos, sem confundir a restrição finita de orçamento com escolha dinâmica final de consumo.
- **Resolução simultânea sem crédito:** atender GA-ADR-ECON-005 por `SNAPSHOT → EXPLORE → AUDIT → SELECT/AUTHORIZE → SETTLE` atômico; este PR é apenas `EXPLORE/AUDIT`, não liquidação.
- **Infraestrutura, modalidades, estado social e serviços financeiros:** não habilitar transbordo, bancos, dívida, contratos futuros nem controle manual de cada remessa. Preservar ponto de decisão do jogador: unidade/destino e infraestrutura.
