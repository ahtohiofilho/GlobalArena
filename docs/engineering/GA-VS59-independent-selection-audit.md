# GA-VS59 — Auditoria independente de seleção finita

Base VS58 #165, `8ec66ed24ec0ab34b7d48bf8118efbeea5dce773`.
Branch/PR draft empilhado; main preservada; SETTLE real bloqueado.

VS58 reaudita a viabilidade da escolha, mas essa auditoria não prova que uma
alternativa melhor foi examinada. `CanonicalSimultaneousTransportAuditor.Verify`
recebe inputs originais, proposta de lotes e contagem alegada de configurações.
Não chama o seletor VS58 e não recebe seu recibo, pontuação ou custo como prova.

Reconstrói domínio, limites agregados e variantes quantizadas. Enumera por índice
de base mista, em vez da busca recursiva VS58. Recompõe cada candidato elegível
pelo gate VS57, exclui os que excedem orçamento abstrato e compara a soma exata
das frações de demanda atendidas, custo entregue e ordem canônica de desempate.
A proposta precisa coincidir com o ótimo canônico, inclusive IDs/ordem dos lotes
e objetos de serviço atestados. Contagem falsa, lote duplicado, escolha inferior,
volume fora do quantum ou oferta estrangeira são recusados.

O resultado aceito recebe uma nova auditoria física/institucional e mantém
`RequireCurrent(session)`. Mudanças de mundo, rede ou direitos invalidam sua
vigência. Não cria caixa, carteira privada, crédito, pagamento ou UI adicional.
O poder de compra abstrato e H continuam regidos pelos contratos VS52–VS57.

Independência é da enumeração/decisão em relação ao seletor: ambas as rotas
usam o kernel físico/econômico VS57, cuja correção permanece uma dependência
compartilhada. O auditor não é uma segunda implementação de preços ou geografia.
O objetivo e domínio são os fornecidos ao auditor, não uma assinatura de política
externa. Não comprova equilíbrio econômico, utilidade ou ótimo contínuo.
Mantém limites VS58 de 128 lanes e 100.000 configurações (padrão 4.096).

Testes adversariais: seleção inferior mas viável, proposta vazia com alternativa
melhor, quantum, excesso de oferta, contagem falsa, duplicação/renumeração,
domínio cuja única escolha é zero, revogação e invariância de save. O teste
conjunto VS58 verifica também o auditor com duas mercadorias, orçamento e
saturação compartilhados, em ordem de lanes invertida.

Benchmark `--vs59-independent-selection`: seleção mais auditoria independente,
8/32/128 repetições de uma rota com três configurações. Não mede 128 cidades.
CI Linux VS40–VS59, benchmark e Godot Release; Windows integral, benchmark e
Godot Debug. Homologação do commit final é registrada no PR após toda a CI.
