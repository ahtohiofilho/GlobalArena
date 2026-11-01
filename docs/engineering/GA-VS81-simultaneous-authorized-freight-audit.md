# GA-VS81 — Auditoria read-only de lotes simultâneos e custo conjunto de congestionamento

**Contrato:** [GA-ADR-ECON-007 R2 #188](https://github.com/ahtohiofilho/GlobalArena/issues/188). **Planejamento:** [#195](https://github.com/ahtohiofilho/GlobalArena/issues/195). **Base homologada:** VS80 PR #194 SHA \`1a18405856f6ae6d638e3d7439fc5aaf30c62f25\`, 46/46 workflows, Draft.

## Separação lógica

Os lotes de comércio hipotético são declarados contra **serviços autênticos descobertos pela VS80**. O lote precisa referenciar, por **identidade de objeto** e não por reconstrução de record, a origem, o bem, a atividade, o destino e o serviço efetivamente existentes. Os direitos e trajetos continuam sendo auditados por \`CanonicalEligibleFreightOfferDiscovery.FromSessionSupplyBound\`, incluindo aprovações institucionais, infraestrutura física, catálogo de massa, acesso e prova de rede.

Para cada pool físico \`(source,commodity,activity)\`, \`Σ remessas + retenção_explícita <= produção_potencial_atual\`; a quantidade não enviada continua apenas **potencial não alocado no ensaio**, não é estoque involuntário e não prova extração material. Remessas hipotéticas de múltiplos serviços competem pelo mesmo pool. A retenção é declaração explícita de quantidade no laboratório, não persistência do save.

A demanda do comprador é extraída da população real como **sinal de valorização**, sem corte \`q <= demanda\`, orçamento \`B=40\` ou carteira monetária. Por \`(buyer,commodity)\`, somamos o volume dos lotes para gerar UM preço médio e UMA receita agregada **por curva experimental de VS78**. O mesmo comprador não ganha orçamento por número de lotes. Curvas são índices relativos adimensionais **não ratificados**.

O frete é calculado com UMA chamada a \`ExperimentalSharedFreightBatchCost.Evaluate\` com todos os lotes e alternativas autenticadas em ordem determinística. Isso soma as massas em corredores compartilhados e cobra a saturação física **uma única vez por corredor**, não lote a lote. Preservamos as unidades monetárias nominais do frete SEPARADAS dos índices de receita. **Não existe receita líquida ou lucro monetário neste gate**, pois a conversão dimensional ainda não foi escolhida.

## Salvaguardas

- **Não provar capacidade rígida de caminhão/trem/navio**: a VS42 fornece custo de congestionamento, não limite de throughput auditado. \`ProvesHardThroughput=false\`.
- **Não garantir compras reais, contratos ou liquidar**: \`CanAuthorizeRealSettlement=false\`. Lotes, retenções e demanda são cenário de leitura, não mutações físicas, financeiras ou de inventário.
- **Sem origem e serviço fictícios**: se uma fonte não existe, um item não pertence ao catálogo, uma oferta foi reconstruída, o serviço perdeu direito ou a rede mudou, a auditoria falha fechada.
- **Sem dupla contabilização**: os mesmos produtores da mesma atividade não podem vender para dois compradores ou por dois serviços além da capacidade física única. Competição por corredores incorpora todas as cargas, sem dividir em carteiras por item.
- \`VerifyIndependent\` recompõe pools, preços agregados e frete e compara frações exatas, saldos e corredores; \`RequireCurrent\` rejeita save restaurado, workers/rede/sessão obsoletos.
- \`MaximumLots=12\`, \`MaximumProfiles=12\`; ordenação por commodity/atividade/origem/destino/serviço/tecnologia antes da auditoria para replay determinístico.

## Gate de aceitação

Suíte com conservação de uma fonte frente a serviços alternativos; frete agregado estritamente superior à soma dos fretes isolados quando compartilhamento e saturação positiva; retenção exata; ausência de limite por demanda; fraude e duplicatas rejeitadas; permutação, restauração, imutabilidade. Benchmark 8/32/128 em sessão física real com pelo menos duas mercadorias no mesmo corredor; Linux/Windows Godot Mono e regressões econômicas GA-VS40–VS81; homologar apenas **100% dos workflows no mesmo HEAD SHA**. PR Draft sobre VS80, sem merge na main.

## Pendências futuras

Exigir simultaneidade de produção consumida local + remota (sem alocar toda capacidade potencial como produzida), regras efetivas de capacidade de veículos/serviços caso necessárias, calibrar função comercial que integre frete na formação de preço **numa única dimensão**, demandas de retenção/reserva executáveis, ciclos de mercado com física conservada e liquidação sem crédito. Nenhuma dessas etapas é implicitamente aprovada aqui.
