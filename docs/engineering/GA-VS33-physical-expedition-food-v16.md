# GA-VS33 — Expedição V16: compra local e alimento físico próprio

Data: 31/10/2026. Base: GA-VS32/PR #136 (34/34 workflows aprovados). Diretriz: colonização sem proibição de distância (#131), sem bancos, empréstimos ou criação de bens invisíveis.

## Resultado de escopo
A VS33 **não ativa acampamentos reais, coleta física temporária ou modais novos**. Ela implementa a primeira ligação economicamente válida entre suprimento e movimento: expedições terrestres compram **à vista, de um produtor real**, exatamente o alimento previsto para toda a viagem e passam a consumi-lo do próprio estoque. Assim, escolhemos deliberadamente a primeira alternativa simples, sem compras intermediárias nem sobra de propriedade no destino.

A VS15/V29 conserva explicitamente suas ordens `depart_remote` antigas, com demanda alimentar ancorada na origem, inclusive após reabrir o save V15. A nova ordem `depart_remote_food` é uma opção separada e muda para **V16**. Não ativar retroativamente V16 em viagens legadas.

## Regras executáveis
1. A previsão `PhysicalJourneySupplyProjection.PreviewPlanned` usa a rota canônica da VS28, cronograma VS31 e os ciclos econômicos realmente esperados **estritamente antes da chegada**. A compra exige lote igual a `workers × ciclos × 0,5 alimento`.
2. O estoque de alimento comercial no tile de origem deve conter **todo o lote**. A falta de estoque interrompe a ordem atomicamente. Nenhum estoque gratuito é gerado.
3. O orçamento de compradores é limitado à parcela proporcional de dinheiro doméstico do grupo sobre a população ainda estabelecida na origem. Obtém-se o preço da resolução local de mercado GA-PROP-001, com valor monetário representado em inteiros; pagamentos zero de cargas não nulas são recusados. A liquidação debita conta doméstica e credita produtor da origem. Recusa se exceder o orçamento.
4. `PhysicalExpeditionFoodParcel(OrderId, RemainingRaw)` pertence exclusivamente à ordem. `PhysicalExpeditionFoodPurchase` permanece no ledger de auditoria após chegar, com quantidade, preço, pagamento e tile do vendedor. Isso separa vendas a expedições das transações do mercado residencial V12, que possui uma restrição histórica de somente um recibo por tile/ciclo e não aceita turnos de compra zero.
5. A cada liquidação enquanto a viagem V16 está ativa, o consumo é retirado **apenas** da parcela da expedição. Os viajantes provisionados não integram os compradores residenciais do tile de origem. O alimento efetivamente consumido entra no agregado `FoodConsumedRaw` do ciclo, uma vez. Em caso de saldo insuficiente, o motor recusa a transição, nunca permite comida negativa.
6. Na chegada, o lote adquirido foi dimensionado exatamente para o consumo necessário: o saldo deve ser zero. Não criamos implicitamente um segundo vendedor de alimento no tile de destino. O parcel ativo desaparece e o histórico da compra continua. A produção e a demanda residenciais voltam a ocorrer depois da chegada, conforme o calendário canônico.
7. Toda a operação de partida é um único evento `depart_remote_food`, com número bruto de alimentos vinculado ao hash da transição. Reexecutar a ordem faz as mesmas consultas, compras e lançamentos e exige correspondência integral do journal e do save.

## Proteção dos saves
- `ExpeditionFoodSaveFormat = 16` e `ExpeditionFoodFormatVersion = 16`; novo domínio de hash tag `96U` inclui saldos de carga e compras históricas. Não existe mudança na cadeia de hashes de V9–V15 sem usar a nova ordem.
- Uma vez acionado V16, continua V16 depois da chegada. A política de compra não é retroativa: V15 e V16 podem coexistir, com demanda comercial somente das viagens sem parcela.
- O novo botão de compra aparece nas ferramentas avançadas do Godot, separado da ordem V15. Arquivo `ga-vs33-expedition-food-v16.json` isolado e sidecar de câmera da VS18.
- Nenhuma alteração à duração do movimento, geografia, propriedades políticas, cidades emergentes ou cadência econômica.

## Critérios de aceitação
- Origem com estoque e caixa: compra lote exato, débito doméstico = crédito produtor, estoque comercial + parcela física conserva o alimento no momento da partida, dinheiro total não muda, demanda comercial local exclui apenas os viajantes que compraram provisões.
- Cada liquidação em trânsito: diminui a parcela física e registra o consumo agregado sem pagar duas vezes. Chegada: trabalhadores estabelecidos somente no destino final, saldo de carga zerado, compra auditável.
- Origem sem estoque suficiente: recusa íntegra, mesmo hash/snapshot/save/journal.
- Save/replay V16 durante a viagem e após a chegada, save V15 antigo byte a byte, viagem com e sem provisões simultaneamente; xUnit Windows/Linux, Godot E2E real e testes visuais.
- Suprimento permanece item físico excepcional para trabalhadores viajando; não implementamos ainda a futura abstração de `trabalho` como insumo.

## Limite e dívida arquitetural explícita
O inventário próprio de viajantes ainda **não** permite excedentes após chegada; a compra é dimensionada exatamente para a viagem. Também não implementamos posse portátil de outras mercadorias, acampamentos reais, obtenção de comida no caminho, serviços externos modernos, ajuste de tarifa por peso, dano por fome, escolha de preço/taxa, contratos nem stock privado dos residentes. O desenvolvimento seguinte poderá acrescentar reabastecimento e caça/coleta com trabalho efetivo, **mas precisará versionar a política de suprimento** antes de alterar cronogramas ou permitir pernoites que elevem o consumo.

PR draft empilhada sobre #136; sem merge isolado na `main`. Não exigir teste manual de QA quando o E2E real e os testes econômicos passam.
