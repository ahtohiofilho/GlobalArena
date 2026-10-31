# GA-VS63 — Descoberta de serviços comerciais autorizados

Base de desenvolvimento: GA-VS62 #169, commit `069059f40c1fd016d08f4e43c75c766219811a5c`.
PR empilhado em Draft; sem merge ou alteração da `main`.

## Escopo

A VS62 descobre pontos econômicos sem fundação formal de cidade.
A VS63 conecta esta projeção à **oferta de transporte existente**, sem inventar
companhias, rodovias, permissões ou pares origem-destino.

`session.TransportNetwork.RegisteredEntitlements` expõe uma cópia
somente leitura dos **direitos cadastrados** na sessão; um registro não
prova rota construída nem autorização efetiva. Para cada registro com
origem produtiva e destino consumidor atuais, a nova operação
`session.DiscoverEligiblePrimitiveFreight(sites, policy, ...)` produz uma
intenção de frete com a tecnologia/preço de versão explícita.
A versão inicial é intencionalmente limitada a **uma commodity,
atividade e tecnologia comercial por consulta**. As configurações de
tecnologia e preço são políticas explícitas, nunca inferidas de
movimento militar, da classe do bioma ou de uma tarifa gratuita.

O sistema usa o VS41 para encontrar o trajeto mais barato no grafo
construído; reaproveita a árvore de origem por tecnologia; VS42
revalida o percurso completo e os trechos realmente operacionais,
e VS57 `Capture/Validate` confirma direitos e concessões de cada
ativo. A ausência de concessão válida falha fechado. A ausência de
uma rota conectada **não** fabrica trajeto — o candidato fica
indisponível. A consulta é somente leitura e não autoriza SETTLE.

Uma chamada sem candidatos válidos devolve inventário vazio; não pode
ser convertida em compra/transporte e não cria um frete "natural" ou
terminal implícito. O retorno possui `Services`, `Lanes`,
`InspectedRegisteredServices`, `EligibleEconomicIntents`,
`UnreachablePhysicalServices` e diagnósticos de buscas por origem.
`PreviewTrade` reaproveita o pipeline auditado VS62 → VS61 →
VS60 → VS59–VS52, com preço, poder de compra abstrato e H protegidos.

## Escala e autoridade

O algoritmo examina **somente os direitos registrados**, não todos
os pares de povoamentos (cartesiano N²). Limite fail-closed de 128
registros por consulta; IDs de serviços precisam ser distintos para
o resolvedor VS41 atual. Seleção só ocorre para uma commodity e
atividade explicitamente associadas a oferta/demanda positivas.
Volumes são expectativas derivadas da oferta/demanda atuais, não
estoques vendidos nem lotes liquidados.

Mudança de trabalhadores, população, saves, vias, proprietários,
concessões, direitos ou estado da rede invalida a consulta antiga.
Sessão restaurada precisa recomputar o snapshot e readquirir a
autoridade institucional, mesmo que os dados do save coincidam.
Históricos VS60–VS62 continuam operacionais.

## QA e benchmark

Testes GA-VS63 cobrem:
- Serviço autorizado único com dados de sites descobertos (sem lista
  manual), caminho construído e carga de referência.
- Conexão à seleção/auditoria econômica abstrata, sem SETTLE.
- Direitos antigos que não correspondem aos tiles atuais: nenhuma
  oferta fabricada.
- Bloqueio físico da via e revogação de concessão.
- Estouro do limite de registros, restore entre sessões e
  reatribuição de todos os produtores para outra atividade.

Benchmark `--vs63-authorized-freight`: 8/32/128 consultas de
um serviço num cenário com dois povoamentos; o parâmetro N é
**número de consultas**, não número de cidades ou habitantes.
Gates Linux: regressões VS40–VS63 + benchmark + Godot Release;
Windows: testes integrais + benchmark + Godot Debug.

## Limites deliberados

Ainda não resolve ciclos comerciais completos nem descobre operadores
sem direitos registrados; não gera novas licenças; não modela transbordos
multimodais, frete marítimo, rotas sem infraestrutura, política
comercial dinâmica, determinação de preços, estoque mercantil ou
liquidação monetária. Um futuro incremento poderá apoiar catálogos
com várias tecnologias e itens sem introduzir micromanagement.
