# GA — Gate D4: pré-validação estrutural de conclusão de instalações

**Status:** proposta experimental opt-in, empilhada no PR #50 (que depende do #49). **Não é ação de construção, conclusão ou ativação do turno.** Não altera `WorldState` e não autoriza settlement.

## Por que esta fronteira é necessária

Os Gates D2 e D3 permitem representar instalações logísticas operacionais e exigir seus atributos no snapshot do mundo com hash canônico. Mas `WorldState.WithTransportFacilities` é apenas uma substituição imutável de estado: **não é prova de que materiais, serviço de construção e pagamentos foram satisfeitos**. Alterar `IsOperational=false` para `true` sem um contrato adicional criaria uma obra gratuita ou um efeito estratégico sem causa econômica.

Este Gate D4 implementa **apenas a metade estrutural** do protocolo de conclusão: `EconomicFacilityCommissioningPreflight.Inspect`.

## O que o avaliador exige

- Turno positivo, mundo vinculado, hash v9 do estado **exatamente** igual à identidade esperada pelo avaliador.
- Proposta para uma ou mais instalações **já registradas** no `WorldState`; não cria `FacilityId` novo.
- Registro ainda **inoperante**; não pode duplicar a conclusão de uma instalação já ativa.
- `PhysicalTacticalTileId` exato: posição e resolução Goldberg M/N devem corresponder à instalação registrada e ao grafo de malha fina fornecido. O ID da célula precisa existir no grafo.
- Conjunto de capacidades de transferência **idêntico**, sem adicionar, eliminar, inverter ou alterar tecnologias ou a versão do catálogo.
- Um ID por proposta e ordenação determinística da resposta, independentemente da ordem das entradas. Uma proposta inválida rejeita o **lote inteiro**.

O retorno `EconomicFacilityCommissioningPreflightResult` inclui somente turno, revisão, hash e IDs validados. **Não contém** `WorldState` futuro, evento executado, autorização financeira, função de ativação nem comando de settlement. Seu resultado não concede operacionalidade ao D3/D2: a instalação continua inoperante e a projeção comercial permanece bloqueada. Teste adversarial verifica isso diretamente.

## O que continua pendente — e quando decidiremos itens

**Ainda não é hora de enumerar os itens canônicos.** A validação estrutural deste gate não precisa dessa decisão. Entretanto, **ANTES da implementação da conclusão autoritativa real da obra**, que consumirá recursos/caixa e produzirá o evento de ativação, será indispensável fechar um contrato enxuto para:

1. **Categorias estratégicas de itens**: quais insumos físicos merecem existência no jogo e `CommodityId` estáveis; itens intermediários só se produzirem escolhas estratégicas relevantes. Nada é canonizado apenas por menção. **Armaduras não são etapa obrigatória nem item canônico**.
2. **Dinâmica agregada**: requisitos de construção/produção representados por poucos insumos, eventualmente transformações secundárias relevantes; manutenção simples e automática, sem movimentar peças, ferramentas, armaduras ou cada remessa manualmente.
3. **Contabilidade à vista**: como validar estoque e saldo **não negativos**, origem física, custo de entrega do depósito ao local da obra, debitando uma única vez sem crédito, bancos, empréstimos ou dívidas. O ponto de consumo será certificado antes do estado operacional mudar.
4. **Evento de conclusão**: revalidar posse e permissões, custos/insumos e versão do estado num único turno fechado; ativar a instalação apenas quando tudo for comprovado, sem meia construção ou liquidação parcial.

**Esse é o gatilho explícito para convidar o usuário a decidir o catálogo e suas regras**, antes de implementar o consumo real. A fase atual NÃO decide materiais, preços, tamanhos de estoque, receitas de estruturas nem quais objetos poderão ser construídos. Não autorizar automaticamente o fluxo por fornecer um `bool ready` ou um suposto recibo facilmente criado pelo chamador.

## Riscos e limites de autoridade

O grafo fino fornecido possui geometria verificável e resolução exata, mas **ainda não há vínculo criptográfico entre sua derivação e a assinatura do mundo estratégico**. Logo, coincidência de `M,N` e tile válido não prova que ele deriva do `WorldBinding`; essa relação deverá ser certificada antes do gameplay. Analogamente, D3 não registra propriedade, permissões políticas ou histórico de construção. Não inferir estes dados dos IDs.

Este gate é **somente leitura**. O `TurnResolutionEngine`, `AuthoritativeTurnCommitGate` e o mecanismo econômico permanecem inalterados. `PhasedTurnResolutionEngine` já exige eventos executados para publicar mudança de estado, e nenhuma fase de conclusão será registrada aqui.

## Testes e aceite

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicFacilityCommissioningPreflightTests`

O conjunto cobre oito cenários: suporte inoperante sem ativação; rejeição D2 após pré-validação; inexistência/ativação prévia/mundo não vinculado; hash obsoleto e outro mundo; localização e capacidades inválidas; lotes vazios/duplicados; normalização de ordens; imutabilidade do registro. Exigir CI completa de kernel, hash e benchmark em Windows, Ubuntu e macOS no SHA final. **Nenhum merge sem autorização explícita**.
