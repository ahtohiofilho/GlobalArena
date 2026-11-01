# GA-VS84 — Feedback de produção usando preço local congelado

**Issue:** #201. **ADR:** GA-ADR-ECON-007 R2 #188. **Base:** VS83 PR #200 SHA \`a6a42997ad80562ca9496543a342db65ad519ea5\` (49/49 CI, Draft, sem merge na main).

## Objetivo

Introduzir uma resposta econômica endógena mínima: o mercado da etapa **anterior previamente calculada** influencia a utilização da capacidade da atividade na hipótese seguinte, sem circularidade nem repetir arbitrariamente a quantidade de produção declarada. O protótipo **não** implica um turno real decorrido no motor de jogo. As duas projeções pertencem à **mesma sessão física corrente** para que se possa isolar e auditar o comportamento do controlador experimental.

## Proveniência dos preços

1. Obter \`CanonicalWorkforceRealizationProjection\` anterior certificado por \`VerifyIndependent\` e \`RequireCurrent\` (VS83), derivado dos moradores/produtores reais.
2. Ler SOMENTE os \`LocalMarkets\` de \`prior.Allocation\` (VS82), que são resultados da formação de preço local VS78, com oferta efetivamente produzida, não retida e não enviada via frete.
3. Confrontar novamente o fluxo físico do produtor e a pressão de demanda da população com o mesmo \`CanonicalPrimitiveSiteDiscovery\`. Se não houve oferta efetiva local para alguma atividade, falta sinal anterior verificável: **falhar fechado** (sem inventar preço neutro para 0% ou produto todo retido/remoto).
4. Agregar **por atividade**, sem comando individual para trabalhadores: \`weightedPrice = Σ(previousLocalPrice(site,commodity) * previousLocalAbsorption(source,commodity,activity)) / Σ(previousLocalAbsorption)\`. Diferentes fontes da mesma atividade compartilham a decisão macro, mas contribuem proporcionalmente à quantidade local comercializada na hipótese anterior. Não atribuir preço do mercado remoto à origem.

## Regra experimental

Preços de VS78 são **índices relativos, não unidades monetárias**. Para preço \`p\` no intervalo 0..1, usar ponto neutro experimental \`p=1/2\`, limite de resposta \`maxStep\` inteiro 1..250:

\`\`\`text
deltaPermille = truncate_toward_zero((2*p - 1) * maxStep)
nextUtilization = clamp(previousUtilization + deltaPermille, 0, 1000)
nextRetention = previousRetention
\`\`\`

A aritmética é racional exata com \`BigInteger\`; sem ponto flutuante e sem truncamentos aleatórios. Na faixa de preços altos, utilização aumenta até 1000; baixos, cai até 0. Na neutralidade não muda. Isso não é decisão final sobre curva de elasticidade nem preço de equilíbrio definitivo; ambas estão abertas a calibração.

A nova projeção passa **somente** as taxas derivadas para \`CanonicalWorkforceRealizationProjection.FromSites\` (VS83); esta deriva a produção dos trabalhadores reais, aplica \`CanonicalRealizedProductionAllocation\` (VS82) e as remessas autenticadas/congestionamento físico da VS81. A nova formação de preços que daí resulta NÃO retroage sobre a decisão deste mesmo passo. O cenário remoto seguinte pode ser 0–12 lotes autenticamente declarados; qualquer quantidade superior à produção derivada falha fechada.

## Limites explícitos

- Sem mudanças de mundo, turnos, workers, infraestrutura ou saves. Não chamar isto de um ciclo histórico avançado.
- Sem carteira \`B=40\`, \`H\` bancário, crédito, estoque involuntário, consumo real, SETTLE ou contratos.
- Sem converter índice de receita em moeda; frete nominal continua em seu próprio domínio. **Sem lucro monetário líquido**.
- Sem capacidade rígida de veículo/corredor atestada; a saturação compartilhada da VS81 continua curva de custo.
- Sem microgestão: uma resposta agregada por atividade com pesos da produção fisicamente realizada.
- O feedback **local** não é equivalente a otimização mundial, arbitragem ou decisão empresarial com preço entregue. Integração definitiva com economia/ciclo de jogo e logísticas dimensionalmente calibradas depende de novos gates.

## QA e promoção

Sete testes adversariais: neutralidade/direções/limites matemáticos, proveniência de mercado real, 0% e retenção total (ausência de preço), saturação dos limites, remessa remota acima do potencial realizado, permutação e replay, fraude de serviço e sessão restaurada. Benchmark 8/32/128 em sessão física real com preços congelados e duas atividades residentes. Gate GA-VS84 Linux (regressões GA-VS40–84, benchmark, Godot Mono Release), Windows (suíte integral, benchmark, Godot Mono Debug). **Homologação apenas se todos os workflows do mesmo SHA final passarem**, PR Draft empilhada sobre VS83, sem merge para main.

## Próximo risco a endereçar

O número \`p=1/2\` é só um ponto de referência comparativo, e o ganho de 100 por mil não pode ser promovido como economia canônica sem testes de estabilidade multi-etapas e, idealmente, uma política de preço entregue comparável ao custo de frete. Para uma verdadeira implementação econômica por ciclos, será necessário armazenar/recuperar um sinal de preço do ciclo efetivamente anterior sem misturar identidades temporais no save.
