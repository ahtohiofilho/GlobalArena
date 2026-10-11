# ADR GA-MESH-001 â€” Geometria esferica por centroides

Data: 2026-10-10
Status: Aceita para geometria estrategica; Gate B tatico pendente.

## Decisao
Adotar no gerador canonico a posicao de cada vertice dual como a media vetorial normalizada dos centros das tres celulas incidentes. Preservar os IDs, as adjacencias, a orientacao do contorno e o refinamento G(m,n) -> G(Nm,Nn).

## Fundamentacao
Comparacao geometrica Class I e III e aceitacao visual humana; prioridade a regularidade das arestas, com variacoes de area entre vizinhos como criterio secundario.

## Compatibilidade
A geometria anterior (circuncentro esferico) difere numericamente. Nao presumir equivalencia de snapshots antigos. A propriedade antiga de equidistancia angular deixa de ser invariante. Simulacao/IDs permanecem independentes da representacao.

## Evidencia e limites
QA R4: 4/4 testes especificos; 1112/1112 regressao. Auditoria estatica independente aprovada para a migracao. Gate B e visualizacao tatico-local ainda nao homologados. Outros arquivos experimentais da D.4 seguem fora deste fechamento.