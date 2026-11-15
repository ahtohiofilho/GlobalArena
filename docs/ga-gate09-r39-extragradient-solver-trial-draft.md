# Global Arena — Gate 09 R39: segundo método candidato de solução

Data: 14/11/2026; base PR #327/R38. Diagnóstico R30–R37 encerrado.

## Mandato

R38 testou e REJEITOU o método de troca coordenada de
fluxos: preservou os invariantes, mas perdeu para os
controles em todas as oito fixtures. Não prolongar essa
família por parâmetros cosméticos.

R39 implementa **predição–correção simultânea** de fluxos,
com dois estágios de avaliação integral original R14,
testando uma nova família numérica para o MESMO F-072.

## Operador original e correção

Se x representa o vetor de fluxo físico por coluna e T(x)
o vetor de destinos/rotas mais competitivos no estado x,
então para cada α testado:

   y = mix_F072(x, T(x), α)
   T(y) = best_response_R14(y) // reprecificação integral
   z = mix_F072(x, T(y), α)
   eval_R14(z) // reprecificação integral final

**Não** usar z=mix(y,T(y),α), pois queremos testar
realmente uma correção calculada com referência ao
mesmo ponto original. A avaliação y não é estado
aceito do jogo. Ambas as avaliações R14 são cobradas.

A estrutura lembra extragradiente, mas não há prova
de monotonicidade variacional do operador comercial
e, portanto, NÃO reivindicar o teorema clássico.

Buscar α em 1/4, 1/8, 1/16, 1/32. Primeira correção
com melhora estrita do mérito M=gap_global+0,3*gap_pior
e sem aumento do gap global é aceita. Caso contrário,
aplicar explicitamente o melhor candidato já avaliado,
ainda que seu mérito piore, identificando o fallback.
Não congelar fluxo e declarar equilíbrio.

## Comparativo igualitário

Usar MESMAS oito fixtures do R38:
mapas 42/162 nós, oferta/demanda normal e heterogênea,
capacidade x0,20 ou x1,00. Controles R21 decaimento,
R21 backtracking e R26 progressiva, sem alterações.

Teto duro por método/célula: 129 chamadas R14 (42 nós)
ou 97 (162 nós), incluindo descartadas. Reportar
a quantidade efetivamente usada por cada política.
Um candidato α pred./corr. custa duas chamadas R14;
com apenas uma disponível não pode iniciar o passo.

## Gate de decisão — sem negociar tolerância

Cinco métricas originais simultâneas <=1e-4 durante
TRÊS atualizações efetivas, mais superioridade do
candidato sobre o MELHOR controle para gap global
e pior grupo em TODAS as fixtures.

Se não conseguir: NO-GO neste orçamento, e não
autorização de produção. O R39 é a SEGUNDA família.
Em caso negativo, o R40 será a ÚLTIMA família antes
de decisão formal: reformular ou selecionar método.

## Segurança econômica e operacional

Preço P=sqrt(D/Q), custo físico c_free*(1+(B/K)^4),
tarifas do dono, controle de transporte, produção 100%
vendida, receita e conservação F-084F preservados.
Manifesto SHA-256 R20 16/16, paridade R13/R14, replay
dos controles e trilha de auditoria completa.
F-059 permanece problema financeiro separado.

Nenhuma alteração autoritativa no C#/Godot, nenhum
merge, nenhuma prova global de equilíbrio. PR Draft
empilhado em R38.
