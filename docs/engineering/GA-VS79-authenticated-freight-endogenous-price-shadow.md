# GA-VS79 — Preço endógeno versus frete autenticado, sem unidade monetária fictícia

**Contrato:** [GA-ADR-ECON-007 R2 #188](https://github.com/ahtohiofilho/GlobalArena/issues/188). **Escopo:** [VS79 #191](https://github.com/ahtohiofilho/GlobalArena/issues/191). **Base homologada:** PR #190 VS78, SHA \`c1c8a0f0fa54327ea60b06f64423455edaefadef\`, 44/44 workflows; base mantida Draft, não merge.

## O que mudou

A VS78 obteve preços e receitas **relativas** em mercado local a partir de oferta e pressão de demanda do povoamento, sem dotação de créditos ou teto de consumo. A VS79 compara uma oportunidade **real de transporte remoto autenticado** com a absorção local da mesma mercadoria:

- **Uma origem** \`(source,commodity,activity)\`, **um destinatário remoto habitado**, **um único serviço** que integra o inventário autenticado de candidatos VS63–VS65. A identidade do candidato precisa ser **o mesmo objeto** emitido pelo portfolio; um record reconstruído não é elegível.
- **Quantidade positiva \`q\`**: só se faz a comparação se \`q ≤ potencial_físico_origem\` e \`q ≤ capacidade_da_alternativa_comercial_autorizada\`. Não se verifica \`q≤demanda_física_basal_destino\` porque aquela demanda é pressão de valorização, não limite quantitativo do mercado final.
- **Preço relativo** calculado pela mesma curva experimental de VS78 (\`k=1\` e \`k=2\`) duas vezes: preço na origem usando demanda residente da origem e preço no destino usando demanda residente do destino. Não existe preço postado de usuário ou limite de créditos no cálculo.
- **Frete nominal físico** calculado pelas massas definidas no catálogo e pelo mesmo \`ExperimentalSharedFreightBatchCost.Evaluate\` utilizado no pré-flight VS65, com opção de serviço autenticado, direito/infraestrutura e política de congestionamento existente. O custo é produzido **uma vez para o lote**, não há teletransporte. Preserva o domínio de uma remessa; ainda não modela concorrência entre serviços ou lotes.
- **Saídas separadas:** índices de preço/receita na origem e no destino, custo nominal do frete, quantidade \`q\`, fontes/destino/serviço e demanda econômica das duas localidades.

## Limitação do inventário legado — CRÍTICA

A autenticação `CanonicalEligibleFreightOfferDiscovery` (VS63/VS64) atualmente computa a quantidade máxima da alternativa como `min(oferta física da fonte, demanda física do comprador)`. Portanto, apesar de a **função de preço VS78** não impor teto de consumo, o **candidato comercial autenticado ainda herda um teto quantitativo do antigo modelo**. A VS79 é estritamente um comparador de lotes dentro daquele subconjunto físico autorizado, **não um mercado remoto com absorção ilimitada**.

Esse comportamento é exposto na API por `UsesLegacyFreightCandidateQuantityBound=true` e `SupportsUnboundedRemoteConsumption=false`; `AddsAnIndependentConsumerDemandCap=false` apenas indica que o comparador não adiciona outra restrição depois da autenticação.

**Gate de arquitetura posterior obrigatório:** separar `autenticação institucional de serviço/rota/capacidade` da antiga `quantidade de demanda física máxima`, mantendo o limite por produção e infraestrutura real. Não contornar o teto criando serviço novo, multiplicando candidatos ou editando o limite sem auditoria. Isso será um incremento próprio antes de alegar consumo remoto ilimitado.

## Resguardo de coerência dimensional — CRÍTICO

A curva VS78 retorna um **índice relativo adimensional**. O motor de frete retorna custo na **unidade nominal de transporte atualmente calibrada**. Não subtrair \`freteNominal\` diretamente do \`índiceDeReceita\`, nem chamar isso de lucro, receita líquida ou arbitragem ótima. **A conversão econômica entre unidades não foi ratificada nem implementada.**

A VS79 valida se o sistema consegue trazer os dois sinais sem falsificar nenhum. A formação de um **único preço comercial líquido** exige próximo gate de coerência dimensional e definição de custo logístico na função de preços, nunca reintroduzindo orçamento \`B=40\`.

## Contratos invariantes

- \`CanAuthorizeRealSettlement=false\`; \`CanCalculateMonetaryNetRevenue=false\`; \`IsNominalFreightConvertedIntoPriceIndex=false\`; sem atualização de turnos, workers, estoques, vendas, dinheiro, H, saves ou rotas.
- Serviço exigido mesmo para benchmark, não criado artificialmente. A alternativa local VS78 funciona sem frete, mas este laboratório compara **apenas lotes remotos viáveis**.
- Auditoria \`VerifyIndependent()\` reconsulta produção, demandas reais, serviço, massa física, custo e curvas com mesmo snapshot; \`RequireCurrent\` invalida recibos em outro save ou se mundo/rede/trabalhadores mudaram.
- Candidato remoto comparável não equivale a lote alocado pelo jogo; nenhuma demanda adicional cria automaticamente reserva ou quantidade comprada.
- A função dyadic de VS78 permanece **experimental**, não canonizada. Não modifica a classificação histórico-laboratorial do H das VS65–77.

## Gates QA

1. Escolha de serviço/fonte autenticada em sessão real; preços da origem e destino podem divergir pela pressão de demanda de residentes.
2. Oferta maior reduz preço marginal relativo (mesma demanda) sem impor limite consumidor monetário.
3. Fraude de identidade de candidato, quantidade zero ou acima da capacidade autenticada: falha fechada.
4. Custo frete real não negativo e **igual nos dois cenários de curva** (não altera capacidade física).
5. Nenhuma receita líquida fictícia, orçamento numérico, saldo ou custo de transporte criado arbitrariamente.
6. Determinismo de mesmo lote, save imutável, restauração invalida documento.
7. Benchmark 8/32/128, Linux/Windows, regressões, Godot Mono Release e Debug.

**Gate de homologação:** 100% dos workflows do HEAD SHA em \`completed/success\`, PR Draft empilhada sobre VS78 e sem merge automático. Homologação de relatório read-only NÃO implica prontidão para alocação econômica geral ou SETTLE.
