# GA ECON-005 — Gate F0: primeiro canto econômico e corredor frio por lote finito

**Status:** camada opt-in de comparação de dois suportes de UMA origem/variedade, proposta sobre o PR #46; não existe autorização nem liquidação.

## Por que não alterar silenciosamente o solver E2

O E2/E3/E4 exige `q[i,j]>0`, `p[j]>0`, `H[i]>0` em todos os pares. Isso é uma hipótese matemática controlada e não significa que o jogo não possa ter mercados desconectados ou mercadorias sem venda. Uma rota nova não deve ser considerada ativa só porque aparece na matriz OD com fluxo artificialmente positivo.

O Gate F0 aproveita **o resolvedor de preço endógeno de uma única variedade do PR #43**, que já permite:

- comprador ausente da rede de serviços (sem acesso e com `q=0`, `H=B`);
- comprador conectado mas com **demanda ótima zero** quando frete/preço for alto;
- oferta não vendida e preço de origem zero quando demanda total é insuficiente;
- preços endógenos de origem quando surge concorrência pela mesma oferta;
- condições de canto `WTP(0,B)<=p+t` e limite de oferta respeitados.

## Contrato de comparação

`EconomicSingleVarietyCornerSupportProbe.CompareOpening` recebe uma única origem, mesma oferta e todos os compradores nos dois suportes, os mesmos perfis físicos de custo real e um par de conjuntos de serviços declarados: antes (comprador-alvo **sem serviço**) e depois (comprador-alvo **com serviço físico explícito**). Os serviços possuem tarifa por unidade de mercadoria exógena **congelada**, que não é inferida da função de custos reais. Mínimo de lote em **microgramas** é um filtro explicitamente fornecido, não contrato de aquisição.

O probe resolve separadamente cada mercado com o solver do PR #43, reinicia do zero, reconstrói as quantidades por comprador e o residual monetário `H`, audita utilidades e preços, conserva oferta e reconcilia separadamente a receita bruta do produtor e o frete cobrado. Depois converte **as quantidades efetivamente vendidas** à massa física (escala `gramsPerWholeUnit`, conversão determinística a microgramas), soma a carga em cada `EconomicPhysicalResourceId` e avalia uma única vez o custo total de cada recurso com o intervalo racional de ECON-004B.

Para a diferença física de dois regimes, recursos com a mesma carga cancelam exatamente. Para recursos que mudam:

```text
delta_lower += C_after.lower - C_before.upper
delta_upper += C_after.upper - C_before.lower
```

A classificação de sinal exige todo intervalo acima ou abaixo de zero. O probe expõe ainda a utilidade de **cada comprador**, inclusive os desconectados, as receitas da origem, quais recursos antes frios agora carregam massa, a carga realizada do comprador que ganhou acesso e se essa carga alcançou o lote mínimo solicitado. Um `true` para o filtro **NÃO** aprova a abertura da rota.

## Caso adversarial relevante

Uma rota inicialmente inexistente para um comprador recebe uma tarifa ex ante razoável: após a abertura, a nova demanda concorre pela oferta e altera o preço de origem e a quantidade do comprador previamente atendido. O mesmo suporte proposto, com frete proibitivo, gera **exatamente zero** para o novo comprador e a rota continua fria. O resultado não cria `C(0)/0` nem infere o custo marginal infinito `C'(0)` como proibição definitiva de ativação.

Se ambos os consumidores tiverem baixa disponibilidade monetária e fretes proibitivos, o solver informa **preço zero, vendas zero e estoque ofertado não vendido**. Nunca cria empréstimo ou venda fictícia para forçar a igualdade.

## Limites expressos

1. **NÃO** resolve canto multivariedade GA-PROP-001. Serve como caso de referência n=1 com comportamento econômico real de zero fluxo. Não transportar as hipóteses de monotonicidade para n>=2.
2. A oportunidade é de **acesso**: comprador sem serviço -> serviço oferecido. Troca de caminho com serviço já existente permanece no domínio de comparações de suportes do E4 e dos lotes finitos de C1.
3. Tarifa por remessa é uma entrada fixa por cenário, não a política de tarifa média de E3. Não há ponto fixo de tarifa com entrada coletiva; custos físicos continuam distintos do frete recebido.
4. Um `EconomicReclearingQuotedRoute` é evidência de incidência **declarada**, não de geografia, permissões ou terminal construído. Antes da execução, exigir prova independente via Gate D1 e catálogo autoritativo de instalações.
5. Os valores monetários são `double` de referência, sem permissão para liquidação. A carga física é arredondada deterministicamente a microgramas; compras positivas abaixo da resolução são rejeitadas, não introduzidas como massa zero.
6. Apenas um suporte é considerado por vez, com um serviço escolhido por comprador. Não existe escolha simultânea entre transportadores nem avaliação institucional de abertura de corredores. O filtro de lote não força consumo.
7. Não modela novos tipos de itens. Itens citados em conversas, inclusive armadura, não são automaticamente canônicos. Recrutamento e manutenção podem ter requisitos agregados, sem operações manuais de remessa.

## Testes e revisão

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicSingleVarietyCornerSupportProbeTests`

Aceite condicionado à CI completa de kernel, hash e benchmark nas três plataformas no SHA do PR. Depende da cadeia #39 → #40 → #41 → #42 → #43 → #44 → #45 → #46; #38 é o auditor anterior independente. **Não fazer merge na main sem revisão da cadeia.**

Próximo marco: condições de complementaridade de `n>=2` com fluxos nulos e preços de origem zero, auditoria separada de segunda ordem/participação e busca finita entre regimes. Resolver com prioridades de jogabilidade, não pela obrigação de reproduzir detalhes industriais irrelevantes.
