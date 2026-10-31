# GA-VS66 — Seleção simultânea finita de frete multirrecurso com auditoria independente

**Baseline:** VS65 PR #172, commit `bab76906fd042ad141ea26c963892d8baa18b886`, 31/31 workflows homologados. Branch empilhada sem alterações de `main`.

## Decisão econômica

A VS65 já valida uma **proposta explícita** de lotes contra toda a oferta/demanda física, um único poder de compra por consumidor e os congestionamentos de vias compartilhadas. A VS66 adiciona a escolha automática **dentro de um domínio fechado e limitado**; não assume o papel de clearing geral de mercado, maximização de utilidade, venda automática, ordem institucional ou despacho de transporte.

O objetivo explícito e provisório do laboratório é:

1. Maximizar a soma das frações da demanda dos compradores/produtos efetivamente cobertas pelos lotes propostos (frações racionais exatas, não preços nem 'pontos' arbitários).
2. Em empate, minimizar o **comprometimento total** dos compradores: mercadorias mais frete agregado por corredor, mantendo H protegido.
3. Se ainda houver empate, preferir quantidades positivas no **primeiro candidato canônico**; em serviços equivalentes, desempatar pelo menor ServiceId. A ordem de entrada do chamador não muda o resultado.

Não se presume substituibilidade entre categorias; a soma simples de frações é apenas um objetivo provisório de referência e não uma afirmação de bem-estar social ou política definitiva da economia GA.

## Contrato e domínio

`CanonicalPrimitiveFreightPortfolio.SelectJointFreight(...)` recebe:

- 1..8 candidatos reais, com identidade de objeto gerada pelo próprio portfólio VS64 (origem, destino, serviço, tecnologia, produto, atividade);
- preços unitários nominais para **todas** as demandas do snapshot, política de compra com ID e versão do portfólio e congestionamento físico declarado;
- `rawQuantum>0`, `maximumQuantaPerCandidate` 1..8 (default 1), `maximumConfigurations` 1..4096 (default 4096).

A dimensão do candidato `i` varia de zero a `min(MaximumRawQuantity/rawQuantum, maximumQuantaPerCandidate)`. Nenhuma quantidade parcial de um quantum é autorizada. O produto do número de alternativas por candidato é verificado **antes de enumerar**; se exceder o teto, a consulta falha fechada, sem declarar ótimo parcial. O limite não significa capacidade total das estradas, nem substitui custos marginais de congestionamento. Se não houver combinação positiva coberta pelo orçamento disponível, a solução legítima é **zero lotes**, com score/custo zero e `Preview=null`.

Para cada vetor:

1. A origem e a demanda têm quantidades agregadas verificadas entre serviços concorrentes, impedindo que o mesmo produto físico seja vendido duas vezes.
2. Cada proposta positiva é auditada pela VS65, que autentica os candidatos existentes, recalcula uma única carga física por corredor e reconcilia o custo agregado com os débitos nominais.
3. A cobertura do orçamento inclui preço + frete, descontando H uma única vez por consumidor. Propostas com déficit são descartadas sem crédito ou utilização de receitas futuras.
4. A escolha vencedora é produzida pela enumeração recursiva de todo o domínio (no máximo 4096 combinações) e verificada automaticamente por um **auditor separado** que reconstrói os vetores por índices mistos, recalculando os mesmos preflights diretamente na sessão atual. Divergência de escolha, valores ou contagem bloqueia a resposta.

A auditoria não é um oráculo externo de preços: a avaliação VS65 é uma função física/monetária comum, enquanto a sequência de enumeração e a verificação de ótimo são independentes.

## Evidência, invariantes e limites de escopo

O resultado expõe quantidades, preview conjunto, score, gasto, contagens de configurações e os sinalizadores `CanAuthorizeRealSettlement=false` e `IsCommitted=false`. `RequireCurrent` invalida resultados de outro mundo, save restaurado, topologia e direitos de passagem modificados. O seletor não persiste carteiras, contratos, turnos, bancos, dívida nem saldos; não fundam cidades e os trabalhadores são produtores/consumidores da mesma entidade populacional.

**Não se promete ótimo global fora do domínio declarado.** Em particular, não se avaliam todas as cidades, rotas e tecnologias do globo, não se procura decisão de produção, não se avalia substituibilidade nem se resolve mercado macroeconômico completo. Esse estágio é um baseline reproduzível e uma garantia de não duplicar direitos físicos nem o H.

## Validação

`GaVs66BoundedJointFreightSelectionTests` testa:
- três produtos na mesma estrada e orçamento protegido, reconciliação e ausência de mutação;
- orçamento insuficiente levando a zero lote, sem compra a crédito;
- direitos comerciais concorrentes sobre a mesma fonte; desempate canônico;
- quanta de 1 e 2 unidades, limites finitos e reordenação determinística;
- tentativas de falsificar candidato, preços incompletos, política/versionamento divergente, limite inválido;
- restauração de save e fechamento de aresta invalidando escolha e auditoria.

Benchmark `--vs66-joint-selection` compara 8/32/128 **consultas**, não 128 cidades, num cenário de três produtos e um corredor; relata tempo e alocação mediana após warmup. O gate VS66 roda Linux + Windows, testes direcionados/completos, benchmark Release, Godot Mono Release/Debug. Concorrência cancela somente runs antigos do gate VS66. A promoção exige sucesso em **todos** os workflows do head final.

**Governança:** PR em Draft, empilhado sobre VS65; sem merge à `main`; etapas futuras de clearing/SETTLE devem ter ADR e auditorias próprias.