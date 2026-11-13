# Global Arena - Gate 09 R24: controle adaptativo de passos (Draft)

Data: 13/11/2026. Base: R23 PR #312, CI homologada como
diagnóstico experimental; R20-R22 são dependências congeladas.

## Motivação

No R23, a priorização simples do quartil de maior arrependimento não
apresentou ganho robusto nos dois portes e comprometeu o pior grupo e
variações de cargas em testes. O próximo experimento deve controlar as
perturbações simultâneas de preço, carga e quantidade sem interferir
no equilíbrio de mercado definido pelo usuário.

## Critério numérico proposto (não econômico)

Após cada fotografia congelada do R20, avaliar cinco propostas de
relaxação alfa: 1/4, 1/8, 1/16, 1/32 e 1/64. Para cada uma, recalcular
preços, carga por aresta e gap F-072 de todos os grupos. Considerar
admissíveis propostas cuja maior variação relativa de preço, carga e
quantidade não exceda o raio numérico. Dentro delas, comparar:
- minimização da pior razão entre gap agregado e gap de pior produtor
  (experimento trust_joint);
- minimização somente da razão do gap agregado (controle trust_global).

Raio inicial 5%, ajustável entre 0,2% e 20% segundo desempenho. Nenhum
raio representa valor material da economia; é apenas ajuste do
algoritmo de busca. Se nenhum candidato respeitar o raio, adotar o
de menor volatilidade, registrar violação e continuar. Isso não
pode ser contado como convergência.

## Governança e métricas de validação

Comparar com R21 decaimento progressivo e backtracking nos mesmos
cenários, tanto por número de atualizações quanto pelo orçamento de
avaliações reais do oráculo (25/49/97/193/385 avaliações).
As sondagens extras do trust têm custo explícito; não inferir
performance real do C# a partir de tempos Python.

Condições de observação de estabilidade: gap agregado, pior grupo,
mudança de preços, carga e quantidade <= 1e-4 em três checkpoints
consecutivos, mantendo vendas integrais e conservação financeira.

A aprovação do laboratório não ratifica F-059, nem demonstra
convergência global ou superioridade geral de uma política. Se o
trust apresentar ganho apenas em orçamento de iteração e perder
em orçamento de avaliações, rejeitar promoção até obter mecanismo
mais barato. Se piorar o pior grupo, vetar a escolha independentemente
do gap agregado.

Fronteiras preservadas: R20/R21/R22/R23 e motor autoritativo
inalterados; PR R24 em Draft sem merge.
