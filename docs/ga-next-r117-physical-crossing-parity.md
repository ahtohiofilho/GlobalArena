# GA-Next R117 - Paridade fisica dos custos naturais por aresta (proposta)

Status: **contrato para revisao, sem alteracao de frete, worldgen, schema ou calibragem**.
Base: R116 / mundo tatico *fine-first* G18. Arestas continuam com comprimento logico 1.

## Decisao

A qualidade natural da travessia deve ter **o mesmo nivel de detalhamento conceitual** em terra-terra (TT), terra-mar (TM) e mar-mar (MM). Paridade nao significa aplicar a mesma grandeza ou forcadamente o mesmo numero de termos: significa um fator fisico principal, continuo e explicavel por classe de travessia, derivado apenas do mundo tatico canonico. Evitar taxonomias discretas adicionais ou ruido para desempatar rotas.

| Travessia | Grandeza fisica principal | Custo natural de operacao | Investimento / construcao |
| --- | --- | --- | --- |
| TT | Diferenca absoluta de altitude entre os tiles | Base TT + funcao monotona suave da diferenca | Base de construcao TT + penalidade quadratica existente |
| TM (porto) | Profundidade relativa do tile aquatico de acesso, medida desde o nivel do mar tatico | Base TM + dificuldade suave de pouca profundidade | Base de construcao TM + penalidade monotona suave da mesma dificuldade |
| MM (mar aberto) | Menor profundidade relativa dos dois tiles aquaticos, como indicador conservador de aguas rasas | Base MM + penalidade discreta em magnitude, continua e saturada, so onde raso | Nao ha investimento direto nas arestas MM; preservar infraestrutura/controle exclusivamente nos acessos portuarios |

Todos os modificadores sao avaliados **uma vez no worldgen / seeding**, e nao por quantidade transportada ou turno. Manter o mesmo custo por aresta nos dois sentidos. Atributos fisicos nao dependem de civilizacao, demanda, preco, titular ou carga.

## Formula conceitual, ainda sem constantes de balanceamento

- Profundidade fisica normalizada `d_i = max(0, seaLevelRaw - FineElevationRaw[i])`; e uma **profundidade relativa no dominio da geografia gerada**, nao metros reais.
- Dificuldade de aguas rasas `s(d)` deve ser continua, monotona nao crescente com `d`, limitada, com `s(d)=0` acima da faixa de aguas suficientemente profundas. Nao usar o limiar de 100 metros como constante.
- Para TM, usar `s(d_agua)`, sem inferir batimetria de canal estreito, dragagem real ou abrigo de ondas. Custo de investimento portuario `B_TM = B_base + beta * f(s(d_agua))`, com `f` continua, monotona e nao negativa; **nao** aplicar penalidade de ondas sem dados proprios de vento/exposicao.
- Para MM, usar `s(min(d_a,d_b))`, com intensidade **pequena** relativamente ao custo natural MM. Em profundidade suficiente, todas as arestas MM mantem o custo base. Isso nao e um mecanismo para forcar desempates: se a fisica for equivalente, o empate e valido.
- Para TT, preservar a regra de diferenca de altitude ja implantada e seu termo quadratico de construcao; nao adicionar termos para equiparar contagem de atributos.
- Permanecem as classes funcionais Ground / Port / OpenSea / BlockedIceSeaBoundary (gelo intransponivel continua bloqueado). Nao criar classes `shallow/deep` ou multiplos tipos novos de aresta.
- Apos a semente natural, todo investimento permitido continua reduzindo frete pela curva R112 `c(I)=c0/sqrt(1+I/B)`, com aritmetica inteira exata. `c0` e `B` devem ser os valores naturais da aresta; sem estoque, capacidade-congestionamento ou distancia em km.

## Limites e verificacoes obrigatorias

1. **Fine-first**: TT, TM e MM leem a MESMA `FoundationFineFirstPhysicalGeography`. Nenhum dado da altitude estrategica antiga pode influenciar custos do mundo tatico.
2. **Resolucao**: o relevo normalizado atual nao comprova profundidade real em metros ou profundidade de canais; a penalidade sera aproximacao de adequacao natural, nao simulacao nautica de detalhe.
3. **Paridade de escopo**: um gradiente deterministico e continuo por classe, sem aleatoriedade, umidade ou sistema de ventos adicional. Em escala tatico-planetaria, a topografia fina e suficiente para um primeiro A/B, nao para calibracao definitiva.
4. **Direcionalidade**: custo simetrico para `A->B` e `B->A`. Nada de ventos/correntes direcionais por enquanto.
5. **Mar aberto**: preservar sem investimento direto; nao inventar infraestrutura maritima por aresta.
6. **Monotonicidade**: aumentar profundidade nunca piora o custo natural da travessia maritima; melhorar infraestrutura monetaria jamais aumenta frete.
7. **Determinismo e assinatura**: toda alteracao futura na composicao dos custos deve entrar de maneira canonicamente versionada na semente / hash e exigir revalidacao dos testes economicos multiplataforma.
8. **A/B e visual**: antes de ativar, comparar baseline R115/R116 versus proposta em malha identica: distribuicao das profundidades normalizadas, proporcao de MM com custo-base, dispersao de custos TM, rotas, caixa e desempenho. Se as diferencas forem despreziveis ou artificiais, nao ativar.
9. **Nao calibrar ainda**: coeficientes beta, intensidades e ponto de saturacao sao politica economica em aberto; nao introduzir numericos de referencia como valores definitivos no codigo.

## Auditoria que motivou a proposta

No seeder atual, TT ja usa `abs(fineElevation[a]-fineElevation[b])` no custo operacional e o quadrado da mesma grandeza no custo de construcao. TM tem custo operacional e custo de construcao constantes. MM tem custo operacional constante e nao aceita investimento direto. `FoundationFineFirstPhysicalGeography` publica `FineElevationRaw` e `SeaLevelRaw` taticos, mas nao batimetria em metros ou informacoes de canais. O modo legado de seeding estrategico permanece por compatibilidade; a futura implementacao desta proposta se restringira ao contrato fine-first.

## Proximo gate

Primeiro produzir diagnostico quantitativo de profundidades normalizadas e comparar as curvas candidatas, **sem mudar os custos autoritativos**. Somente apos aprovacao do A/B fazer um PR separado para implantacao no seeder fine-first, atualizar QA/assinaturas e reabrir a revisao visual Godot.
