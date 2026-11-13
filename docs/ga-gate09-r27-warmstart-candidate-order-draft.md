# Global Arena - Gate 09 R27: priorizacao historica de candidatos (Draft)

Data: 13/11/2026. Base experimental: PR #315/R26; R20-R25 intactos.

## Problema de engenharia

As buscas progressivas R26 reduziram as falhas dos raios fixos e,
em alguns cenarios, melhoraram o pior produtor. O custo por atualizacao
ainda compete mal com o decaimento R21, e o gate de estabilidade
simultanea nao foi atingido.

A R27 testara uma intervencao minima: usar o ultimo passo numericamente
aceito para priorizar a ordem da proxima busca, sem reaproveitar
precificacao, fluxo, custos, objetivos ou rotas antigas.

## Desenho experimental

Na primeira atualizacao, testar em ordem 1/4,1/8,1/16,1/32,1/64.
Posteriormente, ordenar pelas distancias em log2 ao ultimo alfa
aceito, com desempate deterministico por alfa maior. Caso nao haja
nenhuma proposta admissivel, selecionar fallback entre os
candidatos efetivamente avaliados e manter ancora anterior.

Duas politicas: warm_global (gap agregado nao crescente) e
warm_joint (gap agregado e pior grupo nao crescentes).
Cada candidato usa o melhor mercado F-072 da mesma fotografia
congelada, mas os precos/cargas/quantidades sao obrigatoriamente
recalculados no estado proposto usando R14. Evitar qualquer afirmacao
de que as rotas podem ser armazenadas entre snapshots.

Comparadores imutaveis: decaimento e backtracking R21;
progressive_global e progressive_joint R26. Todos usam as mesmas
fixtures de 42/162 nos, limites de passos e contagem individual
de avaliacao do oraculo, com fronteiras 25/49/97/193/385.

## Gate de integridade e criterio de decisao

Verificar manifesto upstream R20 16/16, paridade R13/R14 no mesmo
snapshot, reproducao exata do passo inicial R26, prefixos
deterministicos, venda integral e conciliacao F-084F. Arquivar
todas as sondagens e excecoes; nenhuma avaliacao descartada e gratis.

Para observacao de estabilidade, permanecer com tolerancia
1e-4 em cinco variaveis (gap agregado, gap do pior produtor,
variacao de preco, carga e quantidade) por tres atualizacoes
consecutivas. Isso NAO constitui prova de equilibrio global.

Nao promover uma politica caso apenas economize consultas sem
melhorar ou manter a qualidade comercial sob mesmo custo.
F-059 e motor C# integrado continuam pendentes.
Governanca: PR em Draft, empilhado sobre R26, sem merge.
