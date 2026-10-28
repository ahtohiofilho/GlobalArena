# GA / ECON-005 — Gate C2: reclearing de cenários logísticos sem efeitos no mundo

**Estado:** PR experimental empilhado sobre o PR #40, ainda sem merge/homologação produtiva.  
**Escopo de lançamento:** reavaliar mercados hipotéticos, reconstruir fluxos físicos, não movimentar dinheiro ou estoques, não introduzir crédito.

## Problema concreto

ECON-004B mostra que ativar uma rota inicialmente vazia exige avaliar lotes finitos; ECON-005 Gate C1 fornece custos físicos reais com economias de densidade (e intervalo numérico certificado). Porém a carga inicialmente proposta **não é** necessariamente a carga que o mercado voltará a negociar quando o frete/preço mudar. Comparar o custo de seis unidades com um mercado que negociou três seria incorreto.

## Integração executada

`EconomicReferenceScenarioReclearingProbe.Evaluate` executa **duas resoluções independentes e determinísticas** usando o `EconomicTurnLockedFreightMarketResolver` já presente:

1. A mesma oferta, demanda, preferências marginais de mercado de referência, preços de reserva do produtor e período são usados em ambos os cenários.
2. **Cada cenário** tem cotações de frete explicitamente fornecidas e congeladas durante seu próprio clearing, respeitando a semântica de turn-locked quotes. Nenhuma cotação é automaticamente deduzida do custo operacional da rede: a instituição tarifária permanece uma entrada separada.
3. O auditor independente reconstrói consumo de oferta e limites de demanda, valor comercial incremental pelo integral da curva, frete cobrado, reserva do produtor e retorno líquido por tranche.
4. As quantidades **efetivamente selecionadas** são convertidas pela massa do catálogo em carga de cada recurso físico; compartilham recursos quando os itinerários declarados contêm o mesmo identificador. Recursos sem carga são zero.
5. Com as cargas reais dos dois cenários, avalia o custo operacional agregado `C(q)=a*q+2*b*sqrt(q)+c*q^3/(3*K^2)` já presente no PR #40. A diferença possui limites racionais conservadores para todo recurso que muda de carga; recursos inalterados cancelam.
6. O retorno é somente comparativo, contém volumes, preços marginais por tranche, carga física, custos reais e sinal certificado/indeterminado da diferença. Não existe método de aprovação, liquidação, publicação de eventos ou alteração de `WorldState`.

### Exemplo de teste

O produtor A distribui alimento entre E e F. A rota A→E original usa o recurso compartilhado A–D, o segmento D–E e um recurso de transferência em D; A→F usa A–D e D–F. A alternativa terrestre A→E usa um recurso distinto. O teste altera a tarifa de A→E entre os cenários, mantém A→F e verifica que **o próprio mercado reequilibra as quantidades**. Os custos operacionais são recalculados sobre esses fluxos concretos, não sobre um lote antecipado de referência.

## Identidade dos recursos compartilhados

O custo físico é indexado por `EconomicPhysicalResourceId` (introduzido no PR #40), e **não** por `StrategicEdgeId`. Um segmento construído, um acesso local e um transbordo em D podem ocupar registros separados, sem declarar novas arestas topológicas. A correspondência entre itinerários declarados, ativos/serviços efetivamente acessíveis e o grafo multimodal ainda precisa ser certificada independentemente antes de qualquer settlement.

## Limites de admissibilidade fundamentais

- O **mercado C# existente é um alocador determinístico de referência**: calcula valores marginais da mercadoria em função do consumo acumulado no destino, sob preços de reserva do produtor e cotações de frete previamente fornecidos. Não resolve os preços de origem endógenos nem o potencial completo do consumidor de GA-PROP-001; não deve ser identificado como equilíbrio competitivo.
- Há um **itinerário por par origem–destino em cada cenário**: a diferença entre alternativas é do cenário inteiro, sem competição simultânea de múltiplos serviços para o mesmo par. Precisaremos de um contrato de múltiplos serviços antes de homologar repartição de fluxo entre ambos.
- Identidades de recurso e itinerário são declaradas pelo chamador; essa camada ainda **não comprova reachability geográfica, acesso político ou transferência modal verdadeira**. A auditoria desses vínculos deve vir antes da efetivação.
- As curvas de valor precisam cumprir o contrato existente de não crescimento marginal no intervalo; a nova camada não prova optimalidade global.
- `RealCostSign` é somente comparação de **custos reais**, não retorno financeiro, utilidade nem autorização de adoção. Frete cobrado != custo real da infraestrutura necessariamente.
- Não existe aqui conta bancária, crédito, dívida, garantia, juros ou settlement. Não se grava nada no estado do jogo.
- Os perfis ilustrativos de custo não são calibração histórica universal.

## Próximo limite de integração após CI

1. Verificar o PR #41 e integrar primeiro #38 → #39 → #40, mantendo #41 empilhado até a cadeia estar aprovada para merge.
2. Vincular rotas candidatas ao grafo lógico multimodal persistente e auditoria de acesso/transferências por serviço físico.
3. Substituir o alocador de referência, para o caso econômico definitivo, por um clearing compatível com GA-PROP-001, preços de origem endógenos, orçamentos e regras de participação. Essa integração é exigida **antes de qualquer seleção** de cenários economicamente distintos.
4. Só então integrar a seleção explícita e liquidação **à vista** às fronteiras autoritativas do turno; nenhuma frente bancária.

### Comandos de revisão

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicReferenceScenarioReclearingProbeTests`

É necessária CI completa de kernel, hash e benchmark nos três sistemas operacionais no mesmo commit. Nenhuma aprovação pode ser presumida antes desses resultados.
