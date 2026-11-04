# GA-ECON-REBASE-03 — Reconciliação do destino físico final (pré-validação)

**Base:** GA-ECON-REBASE-02 / PR #250; pacote GlobalArena.Economy.
**Estado:** contrato de pré-validação, sem atestação canônica e sem regra dinâmica de N ativada.

## Decisão arquitetural

O RealPurchasingPowerDerivation requer produção FINAL realmente destinada por mercadoria. Os relatórios VS82/VS95 são hipóteses; os agregados PhysicalEconomicCycle.ProducedRaw não distinguem origem, commodity, insumo intermediário ou destino final. Os preços VS78/VS86 são cotações hipotéticas. Recibos FoodCash V12 não constituem base para todas as mercadorias e reintroduziriam contabilidade monetária legada. Nenhuma dessas fontes pode ser promovida silenciosamente à prova de N.

## Pré-validação determinística

PhysicalFinalOutputEvidenceAdapter.ReconcileClaims recebe **alegações ainda não autenticadas** sobre produção e destino. Cada alegação tem ID, ciclo, origem e mercadoria; os destinos são classificados como uso final local, uso final remoto, insumo intermediário ou retenção explícita.

A soma exata de todos os destinos deve igualar a produção de cada recibo de origem. Assim, ficam bloqueados estoque invisível, dupla alocação, saída sem origem, bem estranho, ciclos diferentes e recibos duplicados. O uso final local e remoto conta uma vez, na origem; mercadoria retida e bem intermediário não geram um segundo produto final. Transbordo não é produção nova.

O universo das mercadorias e a escala física por bem são entradas explícitas; o reconciliador não inventa unidade, preço ou peso. Acumula quantidades brutas em BigInteger, converte em unidade física ao final e ordena os resultados. Não calcula demanda por pessoa, preço, dinheiro nem aloca estoques.

## Fronteira de confiança

O resultado contém CandidateFinalOutput, mas declara HasCanonicalLedgerAttestation=false, AuthenticatesBaseUnitPrices=false e CanAuthorizePurchasingPower=false. Isso não é uma observação canônica executada. Não há atualização do N nem método que publique automaticamente esse resultado no motor econômico. A fábrica de observações geral do REBASE-02 continua dependendo do chamador: sua utilização com dados sintéticos no gameplay permanece proibida.

Para promover o adaptador será necessário ligar os recibos reais de produção e destino ao journal/hash do ciclo fechado, autenticando origem, transporte entre ciclos, população da civilização, escalas e preços do ciclo-base. Requer também política explícita para novos bens e períodos com zero atividade, replay e versão de save. A ausência desses dados é uma pendência de produto, não licença para inventar quantidades.

## Testes e governança

GaVs112PhysicalFinalOutputPreflightTests valida conservação, local/remoto, retenção, insumos, escalas específicas, ordenação, imutabilidade, recusas de entrada inválida, identidade de recibo e ausência de N fictício.

PR Draft empilhada sobre #250. Nenhuma alteração em Godot, gameplay, save, quantidade por trabalhador, precificação f(S/D), dinheiro, impostos ou transporte real.
