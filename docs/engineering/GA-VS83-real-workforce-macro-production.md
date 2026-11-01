# GA-VS83 — Produção macroeconômica derivada dos trabalhadores reais

**Referências:** issue VS83 #199; ADR-ECON-007 R2 #188; base homologada VS82 PR #198 SHA \`0ce8a0adf2e52deec575eadf30d1ac54e6a6a7b0\` (48/48 PASS). PR Draft, sem merge na \`main\`.

## Motivação e escopo

A VS82 estabeleceu que \`potential\` não é \`produced\`, mas ainda recebe declarações ad hoc de quantidade. A VS83 usa a produtividade física efetivamente registrada **dos trabalhadores residentes por atividade e tile tático**, evitando atribuir recursos a produtores hipotéticos. Os mesmos trabalhadores continuam consumidores.

**Escolha de desenho:** evitar microgestão: o chamador declara UMA taxa experimental de utilização e uma taxa de retenção **por atividade**, uniformemente aplicada a todos os povoamentos reais com essa atividade, não a cada trabalhador. Essas taxas físicas são hipóteses de calibração, e NÃO moeda ou comportamento definitivo de agente econômico.

## Fórmulas por fluxo produtivo existente

A partir do snapshot \`CanonicalPrimitiveSiteDiscovery\`, cada \`EconomicSupplyFlow\` autêntico precisa de \`ActivityWorkers > 0\` na mesma origem e atividade. Para \`u,r\` inteiros no intervalo 0..1000:

\`\`\`text
realizedRaw            = floor(potentialRaw * u / 1000)
retainedRaw            = floor(realizedRaw * r / 1000)
potentialNotExtracted  = potentialRaw - realizedRaw
\`\`\`

A multiplicação é \`BigInteger\` exata; não há dupla contagem quando milhares de trabalhadores ou fontes estão envolvidos. Um fluxo com 0% de utilização continua potencial, sem vender, armazenar ou gerar receita. Se toda a produção for zero, o relatório VS82 é ausente (\`Allocation=null\`), em vez de inventar um produto mínimo. Com toda a produção zero, remessas remotas não são válidas.

Um fluxo com realização positiva cria exatamente uma \`CanonicalRealizedProductionDeclaration\` para a VS82; esta aplica a conservação física \`produced = retained + remote + local\` e os fretes autenticados VS81. Ofertas e preços locais continuam em função da pressão de demanda real e da quantidade oferecida (não limite de consumo).

### Invariantes

- Não inventar trabalhadores, cidades, fontes, tecnologia, renda, comprador ou rota. Política não pode omitir atividade com fonte produtiva ou inventar outra atividade não presente.
- Não gerar inventário involuntário: \`potentialNotExtracted\` é capacidade ainda não usada, não matéria armazenada.
- Não ativar uma instalação primitiva com custo fictício; o mundo já identifica a atividade real do trabalhador.
- Não trocar identidade da sessão, workers revision ou estado de rede no meio da auditoria. \`RequireCurrent\` e \`VerifyIndependent\` reconstruem tudo na mesma sessão; save restaurado não compartilha recibo.
- Retenção deliberada não equivale a inventário persistente. A absorção local é hipótese macroeconômica de oferta efetiva.
- Sem carteira privada, gastos \`B=40\`, teto de consumo por demanda, conta de banco, crédito, salários/obrigações financeiras ou SETTLE.
- Preço/receita VS78 são índices relativos; frete VS42/VS81 é custo nominal separado. NÃO subtrair para inventar lucro financeiro.
- Capacidade de veículo/corredor não é \`hard throughput\` certificado; congestionamento conjunto apenas modifica custo de transporte.
- O efeito econômico real de preços sobre a taxa de utilização **ainda não é implementado**. VS83 é laboratório comparativo com taxas declaradas, não decisão automática de produção rentável.

## QA

Seis testes adversariais: utilização 100% e zero, 50%/25% com arredondamento exato, preservação da origem/força de trabalho por atividade, remessa maior que produção realizada falha, política inexistente ou duplicada falha, determinismo de políticas/lotes e restauração/save. Benchmark 8/32/128 alimentado com serviços autênticos e duas commodities. Gate dedicado GA-VS40–83/Linux, suíte completa Windows, Godot Mono Release/Debug, 49 workflows previstos no SHA final. Somente homologar após 100% \`success\`.

## Próxima decisão aberta

Em fase futura, a calibração da **intensidade econômica endógena** de produção deve vir do próprio preço/mercado e tecnologia, sem devolver a carga de microgestão ao jogador. É importante evitar circularidade instável entre preço e oferta; a decisão deve olhar preço da rodada anterior/snapshot e ser determinística e auditável. Não escolher sozinho uma função de lucratividade enquanto índices de preço relativo e custo nominal do frete não forem dimensionalmente compatíveis.
