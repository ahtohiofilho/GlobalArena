# GA-VS64 — Portfólio de candidatos comerciais para múltiplos recursos

**Baseline validada:** GA-VS63 #170, commit
`e55168dc35465151f9613c5fe5d1fec090adf53b` (44/44 workflows success).
**Governança:** incremento isolado em PR Draft, baseado na VS63; não alterar
`main` nem ativar SETTLE real.

## Decisão de arquitetura

A VS63 identifica ofertas de frete de **uma** mercadoria/atividade/tecnologia
por consulta, usando somente os direitos institucionais registrados, vias
efetivamente construídas e concessões comprovadas na sessão atual. A VS64
organiza até 12 desses perfis como um portfólio observável e determinístico:
`session.DiscoverPrimitiveFreightPortfolio(sites, profiles, ...)`.

Cada perfil recebe as mesmas evidências de sessão e executa o caminho
**VS63 → VS41/42 → VS57/59**, isoladamente e em ordem canônica por
mercadoria, atividade e ID/versão de tecnologia. A VS64 não inventa
estabelecimentos, rotas, empresas, meios de transporte, preços ou direitos.
Sem produção ou consumo físico positivo, a oferta permanece indisponível.
Não enumera todos os pares de povoamentos: examina os direitos registrados
em cada perfil. Limite de 12 perfis; cada consulta VS63 segue limitada a
128 direitos registrados. É um catálogo inicial, não uma simulação de
mercado integral para todas as tecnologias ao mesmo tempo.

## Integridade econômica: não duplicar o mesmo frete

Quando alimentos, madeira e pedra utilizam o mesmo serviço 77 ou o
mesmo ativo 31, os três itens são **usos potenciais concorrentes**.
`SharedServices` e `SharedAssets` identificam explicitamente
essas colisões, preservando os respectivos produtos e volumes brutos
hipotéticos por ciclo. Os números de capacidade em cada linha
**não podem ser somados**; a tecnologia atual não atribui volumes
simultâneos nem resolve congestionamento multicommodity.

Os resultados individuais VS63 ainda podem ser inspecionados como
propostas read-only (`Slices[].Discovery`). Não é permitido somar
suas propostas de gasto ou afirmar que existe poder de compra
independente para cada mercadoria, mesmo se não houver choque
de serviço/ativo: todas competem pelo mesmo orçamento abstrato
e H residual. Por contrato, `CanCombineIndependentTradePreviews=false`
e `CanAuthorizeRealSettlement=false`; não há método de liquidação
conjunta. A futura etapa de seleção simultânea deverá fazer um único
clearing para orçamento e ativos compartilhados com auditoria
independente, sem crédito nem garantias.

## Validação de entrada e tempo

O portfólio exige perfis únicos por
(commodity, atividade, tecnologia, catálogo da tecnologia).
Se um mesmo recurso aparecer em mais de um perfil com escalas físicas
incompatíveis (gramas por unidade), rejeita a operação antes de gerar
uma precificação. A infraestrutura mapeada precisa coincidir com
aquela declarada na política de preço. Não aceita mais que 12 perfis,
não aceita lote vazio e não aceita metadados institucionais sem versão.

Os objetos capturam a identidade da sessão, hash físico, worker revision,
network revision, direitos, vias e concessões, e são invalidados após
movimentos/realocações, ordens, mudanças da infraestrutura, bloqueio
das arestas e saves restaurados. `RequireCurrent(session)` valida
todas as fatias e seus proofs individuais. Inventários antigos
podem ser comparados como dados históricos, nunca reaproveitados
para afirmar autoridade atual.

## QA

Testes GA-VS64:
- Três recursos realmente produzidos que disputam um serviço real e
  um ativo físico; após a realocação, itens sem oferta produtiva
  desaparecem do portfólio, sem serviço fictício.
- Reordenação da lista de perfis não altera a saída.
- Direitos legados sem endpoints vivos não são implicitamente ativados.
- Duplicações, massa incompatível, mapeamento de tecnologia incorreto
  e limite de perfis são rejeitados antes do roteamento.
- Save/restauração, fechamento de aresta e realocação de trabalhadores
  invalidam o portfólio e recompõem a oferta real.
- Portfólio não autoriza SETTLE e não duplica orçamento de compra.

Benchmark `--vs64-freight-portfolio` mede 8/32/128 consultas sucessivas
em um cenário fixo de **dois sites e três mercadorias** (food, wood,
stone), uma via construída, um serviço institucionalmente autorizado.
O parâmetro 8/32/128 é número de consultas, **não** de povoamentos.
Não extrapolar esses números como promessa de escala O(N) para um
mundo grande.

Gates Linux: testes direcionados VS40–VS64, benchmark e Godot Release.
Windows: suíte integral, benchmark e Godot Debug. Só após todos os
workflows do commit final concluírem success é possível homologar.
O gate VS64 usa concorrência por PR para cancelar apenas suas próprias
versões obsoletas; os demais gates herdados preservam sua política.

## Não implementado nesta etapa

Não há otimização conjunta multi-commodity, preço dinâmico global,
transbordo, frota física, capacidade finita de porto, compra à vista
efetivamente liquidada, infraestrutura gratuita de nível superior
nem bancos, empréstimos, juros ou saldos do jogador por consumidor.
O poder de compra abstrato e H seguem congelados e não se traduzem
em carteiras individuais numéricas.
