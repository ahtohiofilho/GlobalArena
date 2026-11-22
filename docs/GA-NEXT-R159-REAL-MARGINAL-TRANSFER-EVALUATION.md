# GA-Next R159 — Avaliação marginal real sob orçamento explícito

## Escopo aprovado

A R158 implementou classificação estável e barata de evidências marginais **já calculadas**, mas não avaliava mercados. A R159 estabelece a ponte de custo mensurável entre o motor econômico e o índice. Mantemos a separação entre diagnóstico consultivo e recomendação de realocação completa.

## Contrato R159

- Receber um estado canônico único, fisicamente vinculado, com trabalhadores individuais, ocupações e posições autorizadas, além de infraestrutura homologada.
- Receber uma lista **explícita e limitada** de trabalhadores produtores reais e uma atividade logística terrestre ou portuária de destino; rejeitar candidatos duplicados, inexistentes ou não produtivos.
- Para **cada candidato**, construir a variante com apenas aquele indivíduo transferido **no mesmo nó**; nunca usar a variante anterior como baseline para o seguinte.
- Executar o comparador econômico R154/R157, com A = trabalhadores originais, B = um trabalhador realocado e frete vigente, C = mesmo trabalhador realocado com cotação sombra experimental.
- Registrar o saldo A→C (receita bruta coletiva menos frete físico), em escala monetária exata. Esse é o efeito marginal **coletivo do cenário**, não a renda daquela cidade nem o resultado privado da civilização do trabalhador.
- Enviar todas as observações ao indexador determinístico R158 e fornecer `LowestReturns` e `HighestOpportunities` para os candidatos avaliados.
- Publicar contagem de soluções completas do mercado (3 por candidato) e tempo decorrido de teste de ponta a ponta; não afirmar complexidade empírica sem medição.

## Limites fundamentais

O **orçamento máximo de avaliações** é um teto operacional rígido, não um parâmetro que adapta os cálculos financeiros. Se o conjunto solicitado ultrapassar o limite, rejeitar a operação sem executar uma amostragem escondida.

A R159 **não** implementa buscas combinatórias de pares origem–destino, migração, cidades distintas, nascimento ou distribuição probabilística de crescimento. Não mede ainda a produtividade marginal de uma ocupação atual sem conhecer as outras atividades potenciais, nem calcula ganhos específicos por civilização. Uma avaliação de adição de novo trabalhador exigirá um cenário de oferta **e demanda** nova e não poderá reutilizar a transferência de um produtor.

Os resultados numéricos da R157, inclusive a perda superior ao benefício logístico em um cenário real, permanecem válidos. Não recalibrar o frete para forçar um ranking positivo.

## Gate e próximos passos

- Teste com **dois trabalhadores reais diferentes** no mesmo mundo, comparando transferências candidatas a LAND, sem alterar estado, trabalhadores ou capital.
- Checar hash canônico antes/depois; resultados de ranking e orçamento rígido.
- Workflow dedicado com valores monetários exatos, hash de referência e tempo decorrido.
- A partir da evidência do CI, decidir se a etapa cara — gerar avaliações completas por cidade/atividade — merece ser limitada por pré-filtragem e cache. **Não é necessário abrir o Godot**.
