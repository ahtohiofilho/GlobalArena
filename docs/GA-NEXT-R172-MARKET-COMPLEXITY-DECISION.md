# GA-Next R172 — medição antes de simplificar redistribuição global

## Hipótese a validar
A R168 distribui presença, demanda e produção entre nós por frações do turno. Não existe justificativa causal para que essa simples repartição exija um solucionador iterativo adicional. A fonte de repetição é a antiga coordenação de múltiplos lotes em `FoundationPhysicalGlobalMarginalCoordinator`; sua utilidade econômica precisa ser confrontada com custo e benefício.

## Primeira instrumentação (sem mudança econômica)
`FoundationMarginalMarketComplexityAuditTests` registra, em cenários determinísticos de duas civilizações e dois mercados:
- existência/ausência de travessia física conectando mercados;
- volume produzido por trabalhador;
- número de lotes agregados;
- passagens realmente executadas e quantidades movimentadas por passagem;
- diferença líquida de quantidades de mercado entre distribuição inicial e final;
- tempo medido em milissegundos, **somente diagnóstico**, sem impor limite dependente da máquina do CI.

Os testes continuam exigindo conservação total e uma passagem final sem movimentação; não mudam o coordenador, regras de mercado, transporte, impostos, fórmula de preço ou tolerância.

## Interpretação cautelosa
- O teste de duas fontes NÃO representa milhares de mercados, mas cria uma referência reproduzível da origem do custo.
- Uma diferença física de alocação não demonstra melhoria relevante de receita. O próximo passo da auditoria deve comparar receitas/custos ou utilidade integrada quando os dados medidos indicarem múltiplas passagens.
- O tempo registrado pelo GitHub não é um benchmark confiável para performance final; será confrontado com cenários maiores e medição local.
- O algoritmo executa uma distribuição inicial por lote e, depois, `Passes` voltas globais; a R170 ainda executa derivação financeira e replay independente.
- A ausência de congestionamento permanece canônica no caminho de liquidação. Nenhum custo físico é reprificado por volume de tráfego.

## Decisão pendente
Antes de alterar o código de produção, medir em cenários representativos:
1. quantas passagens agregam ganhos econômicos relevantes;
2. qual parte do custo vem do algoritmo e qual do replay necessário para determinismo;
3. se uma distribuição sem retroajuste global é suficientemente justa para a regra do jogo.

Evitar simplesmente reduzir `MaxProducerPasses` de 64 para outro número sem fundamentação: seria apenas esconder a complexidade. Se o retroajuste não se justificar, remover o requisito de ponto fixo por uma decisão de modelo econômico explicitamente testada.
