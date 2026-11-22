# GA-Next R160 — contribuição marginal da atividade atual

A R159 avaliava a rentabilidade de uma **transferência específica** para logística, com três soluções completas de mercado por trabalhador. A R160 responde a uma pergunta distinta: qual é a contribuição marginal de **continuar trabalhando na ocupação atual**?

## Protocolo econômico

- Fixar um mundo canônico único, infraestrutura existente, todos os demais trabalhadores, suas localidades, identidades e demanda.
- Resolver uma vez o mercado de referência, com frete físico autoritativo congelado por ciclo.
- Para cada candidato produtor real, construir uma variante **somente contrafactual** em que seu trabalho produtivo fica temporariamente inativo (`Other/Unassigned`) no mesmo tile; o trabalhador **não é eliminado**, nem se desloca, nem deixa de consumir.
- Resolver novamente o mercado sob idênticas políticas de demanda e mercadorias, e mesmo conjunto de arestas/obstruções.
- Contribuição do trabalho atual: `cash_baseline - cash_inactive_worker`, em escala monetária exata, onde `cash = gross_sales - physical_freight` representa o resultado agregado, não a receita privada de uma cidade.
- Retornar os `k` menores e maiores resultados com desempates determinísticos (civilização, tile, trabalhador), sem testar todas as novas profissões nem migrações.

O experimento com `n` candidatos usa **1+n** soluções econômicas completas (baseline compartilhado). A R159 usava **3n** soluções para transferência logística com desconto experimental. A diferença de custo é estrutural; o tempo observado varia por runner e tamanho de mundo.

## Limites e próximos passos

Ainda não é um ranking universal de toda a população: a busca é limitada a candidatos explicitamente selecionados. Não há modelagem da especialização de trabalhadores que já operam logística, militares ou viajantes. Tampouco é um valor por civilização: impostos, pedágios e transferência entre tesouros devem ser considerados quando vier a decisão individual estratégica. A R160 **não** acrescenta população, não altera preços autoritativos, não publica renda, não muda comandos, não introduz congestionamento, não faz migração e não altera a UI.

O próximo passo natural é comparar esse diagnóstico real de contribuições existentes com oportunidades de *adição de trabalhador* em atividades e localidades válidas, incorporando **tanto produção quanto consumo** no contrafactual de entrada. A decisão sobre automação permanece aberta; nenhuma função de diagnóstico emite ordens.
