# GA Gate 09 R26 - Busca progressiva com custo auditavel (Draft)

Data 13/11/2026. Baseline R25 PR #314 sobre R20-R24; todos sem promocao ao motor autoritativo.

## Problema

R24/R25 mostraram que reavaliar cinco candidatos por passo e impor restricao de raio nao produz convergencia melhor; as referencias R21 seguiram superiores. A proxima hipotese e uma selecao sequencial que pare cedo apos encontrar um passo aceitavel, reduzindo custo sem mascarar o pior produtor.

## Planos

1. Referencia R21 decaimento progressivo, sem alteracao.
2. Referencia R21 backtracking por gap global, sem alteracao.
3. Controle R25 elastic_opportunity, cinco candidaturas por passo.
4. R26 progressive_global: primeira proposta entre 1/4,1/8,1/16,1/32,1/64 que nao aumenta gap agregado.
5. R26 progressive_joint: primeira proposta que nao aumenta nem gap agregado nem gap do pior produtor.

Todas as propostas recomputam precos, congestionamentos e alocacoes segundo a mesma rede e o mesmo F-072 original. Fallback, quando nenhuma proposta passa, sempre e registrado, nunca declarado equilibrio. Todas as consultas do oraculo R14 sao contabilizadas, inclusive candidaturas descartadas; a restricao de custo limita a quantidade avaliada.

Criterios para comparacao: gap global, pior grupo, variaçao de preços, cargas e quantidades, quantidade de propostas, fallback, cenario, estagio de oraculos efetivamente executados e tempo observado. Cinco metricas numericas <=1e-4 por tres checkpoints consecutivos sao apenas condicoes de observacao local, nao prova de equilibrio.

## Contratos de integridade

Conservar R20 (manifesto de 16 arquivos), R21-R25, producao 100% vendida e receita bruta igual a creditos em F-084F. Conferir em checkpoints o oraculo R13 original e cache R14; reproduzir prefixos e a aceitacao do backtracking R21 quando aplicavel. F-059 permanece em aberto, assim como integracao do solucionador C#, desempenho do jogo e validacao de convergencia geral.

Go/no-go deve considerar custo por avaliacao real e pior grupo; nunca aprovar automaticamente por CI. Governanca: PR R26 Draft sobre R25, sem merge e sem alterar Kernel/Runtime/Simulation/Godot.
