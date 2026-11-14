# Global Arena — Gate 09 R33: robustez da sensibilidade de precos

Data: 14/11/2026. Base: PR #321 / R32. Todo o motor R20–R32
e executavel C# / Godot preservados.

## Tese em investigacao

R32 mostrou que, entre 915 inversoes de mercado observadas em
oito trajetorias sobre duas fixtures, cotacoes novas combinadas
com transportes antigos reproduziam o novo mercado em todas.
Isso nao e prova causal nem generaliza automaticamente.

R33 confrontara a mesma metrica em cenarios fisicos e comerciais
heterogeneos sem alterar a funcao de preco P = sqrt(D/Q).

## Matriz de cenarios

- baseline original;
- mercado com D x4 uniforme;
- demandas exogenas de diferentes intensidades (0,35 a 2,4
  vezes a demanda de referencia);
- producao por grupo heterogenea (0,5 a 2 vezes a referencia);
- capacidade de transporte x0,25 (custo elevado/congestionamento);
- capacidade x4 (congestionamento reduzido);
- capacidade heterogenea (x0,25, x0,5, x1,5 e x4);
- combinacao de demanda e oferta heterogeneas e capacidade x0,2.

E permitido transformar somente campos fisicos de entrada
Market.demand, Group.supply e Edge.capacity, e realocar
exatamente a oferta inicial de cada produtor para manter
toda a producao vendida. Nao mudar preferencias, curva
de demanda/consumo, aliquotas, taxas, proprietarios,
conexoes do grafo, distancia ou formula de congestionamento.

## Eixos de decisao

Cada fixture sera executada com politicas de amortecimento
R21 decaimento, R21 backtracking e R26 progressive_global
inalteradas. Para toda transicao comercial, contar:
- total de mudanças de mercado;
- fracao reproduzida por ranking hipotetico de preco novo
  e custo antigo entre TODOS os mercados;
- fracao reproduzida por ranking de preco antigo e custo novo;
- predomino da magnitude logaritmica do componente preco;
- worst-group F-072, maximo carregamento/capacidade e
  eventual checkpoint de estabilidade.

Comparar resultados ponderados por eventos com resultados
de peso igual por fixture/politica. Nao considerar trajetorias
repetidas como amostras independentes. Cenarios sem mudancas
tem denominador indefinido, nao zero.

## Gate tecnico

- R20 SHA-256 16/16 e identificacao por hashes de fixture.
- Verificar em cada mercado e snapshot P=sqrt(D/Q).
- Paridade R13/R14, replay do baseline R32, conservacao
  F-084F e 100% das vendas.
- Cobranca de todas as chamadas do oraculo, inclusive
  propostas rejeitadas, e dos scans independentes dos
  rankings hibridos (consultas adicionais R14 = 0).
- Cinco tolerancias simultaneas <=1e-4 durante tres
  verificacoes consecutivas, sem flexibilizacao.

A matriz CI usa 42 nos/48 updates e 162 nos/24 updates.
Ela nao substitui a pesquisa de convergencia em horizonte
mais longo. Se o padrao do R32 se romper nos novos cenarios,
investigar quais combinacoes de oferta, demanda e capacidade
induzem dependencia maior de transporte. Se permanecer,
um proximo gate pode estudar amplificacao Q→P, sempre
sem criar preferencias artificiais ou alterar a economia
autoritativa antes de provar condicoes numericas adequadas.

F-059, prova de equilibrio global, paridade integrada
do solucionador C# e liberação de producao nao aprovados.
PR Draft empilhado em R32, sem merge.
