# GA-VS69 — Choques locais de preços com prova de independência física/econômica

**Baseline:** VS68 PR #175, head `8585354043d3d68b04d2d86790ec682ce233f5b6`, CI 34/34 aprovada. VS69 é PR Draft empilhado, sem merge em `main`.

## Decisão de produto

O laboratório da VS68 permite comparar globalmente preços, poder de compra H e congestionamento para até seis hipóteses. A VS69 limita, por desenho, os cenários de **preço local** a **um comprador e mercadoria por hipótese**. Assim podemos aferir o que acontece quando apenas uma região muda o preço de madeira ou alimentos, sem inserir desequilíbrios imaginários nos demais mercados.

A pergunta não é 'um mercado é completamente isolado?', mas 'quais mercados são comprovadamente desacoplados?'. A VS67 detecta grupos conexos por comprador/H, origem+commodity+atividade, serviço institucional, ativo e corredor físico/tecnologia. O choque local **não altera esse grafo**: dentro de um componente conectado, seus efeitos podem propagar-se via orçamento ou custo de congestionamento e a solução inteira deve ser recalculada. Fora do componente, qualquer diferença de carga, quantidade, preço pago ou pontuação sem mudança do próprio contexto é uma regressão.

## API

`CanonicalPrimitiveFreightPortfolio.CompareLocalizedBuyerPrices(candidates, baselinePrices, purchasingPolicy, congestionPolicy, shocks, rawQuantum, maximumQuantaPerCandidate, maximumConfigurationsPerComponent)`

Cada `CanonicalPrimitiveLocalPriceShock` declara `ScenarioId`, `Buyer`, `Commodity` e `NewPricePerWholeFlowUnit`.

- 1 a 5 choques nomeados (além da baseline `baseline`, reservada), IDs únicos de até 64 caracteres, preço não negativo racional exato.
- Choque deve visar **um preço comprador/produto existente na demanda física atual**. As demais entradas permanecem bit a bit equivalentes; não se aceitam compradores inexistentes, duplicação de preços, preços ausentes, nomes repetidos, candidatos forjados ou domínios fora dos limites.
- Todos os cenários usam a **mesma** política versionada de compra e H e a **mesma** função de congestionamento. Portanto as diferenças são atribuíveis ao preço alterado, não à política global ou ao mundo.
- A VS68 congela inputs e reexecuta VS67/VS66 para todos os cenários. O plano de independência de VS67 nunca é calculado com rotas sintéticas e não exige que as mercadorias sejam do mesmo tipo.
- Para cada choque, a VS69 certifica `CoupledComponentIndices`: todo grupo com qualquer candidato atendendo o comprador afetado. Isto é **conservador**: alterar um item não comercializado desse comprador pode não mudar suas compras, mas não se pode alegar independência porque seu H é único.
- Componentes **fora** do conjunto acoplado devem preservar rigorosamente as quantidades dos mesmos candidatos, a pontuação de demanda, o comprometimento monetário e as cargas por corredor. Cada componente acoplado é recalculado normalmente, sem prometer ausência de spillover interno.
- A auditoria `VerifyLocalizedImpacts` comprova que cada cenário modificou somente uma chave de preço, reexecuta integralmente a auditoria VS68/VS67/VS66 e coteja o certificado por componente. Saves restaurados ou mudanças na rede anulam toda a comparação.

### Cenários de regressão

1. Duas estradas de pedra operacionais, dois serviços autorizados, dois compradores distintos e dois produtos: custo alto de alimento no comprador A remove apenas o lote A, enquanto o lote de madeira no comprador B permanece. O choque inverso produz efeito simétrico. Para dois blocos de 9 configurações cada, somente 9+9 são efetivamente avaliadas por cenário (versus 81 no domínio monolítico).
2. Preço de pedra alterado em uma origem que não possui candidatos destinatários: nenhum grupo atingido, e nenhuma alteração espúria do plano.
3. Três mercadorias e um único comprador/H e estrada: qualquer choque naquela região mantém **um componente conectado**, jamais divide o orçamento por produto.
4. Ordenação inversa de ofertas/choques, mutação das listas originais após o snapshot, falsificações, base incompleta, limites excessivos e expiração de rede/save.

Benchmark `--vs69-local-prices`: cenário fixo **conectado** com um comprador e três produtos, baseline e dois choques de preço, 8/32/128 consultas. Confirma estabilidade e custo de auditoria, **não** simula 128 mercados nem comprova ganho de escala do caso independente. O teste com duas vias prova a fatorização de modo separado.

Gate GA-VS69 em Linux (regressão VS40–69, benchmark Release, Godot Release) e Windows (regressão integral, benchmark Release, Godot Debug). A homologação requer todos os workflows do **head final** aprovados.

## Governança

A política de poder de compra abstrato permanece uma diretriz de modelagem para etapa futura; valores monetários numéricos aqui são parâmetros explícitos de laboratório, não uma mudança do conceito. Trabalhadores produzem e geram demanda; povoamentos emergem dos tiles ocupados, sem 'cidade' formal. Não existe comércio real, estoque comprometido, novo serviço de transporte, banco, crédito, dívida, renda futura, caixa negativo, despacho ou SETTLE. `CanAuthorizeRealSettlement=false`; `IsCommitted=false`. Comparações de preço **não** estabelecem preferência de bem-estar ou política ótima.
