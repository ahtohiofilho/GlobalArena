# GA-VS10 — Commit gate para comandos da economia física

Data: 29/10/2026. Base: GA-VS09, PR #96 (empilhada sobre GA-VS08/PR #95). PR #97 é uma implementação alternativa do VS09; **não mesclar as duas variantes por acumulação**.

## Objetivo
O laboratório VS07 mutava seu simulador offline e só depois tentava publicar um snapshot físico no WorldState. Se a publicação canônica falhasse, o simulador e o estado do jogo poderiam divergir.

O VS10 introduz uma fronteira transacional em `PhysicalEconomyCanonicalCommandGate`:
- inicialização vincula mundo de origem e aceita um snapshot físico v9;
- cada ordem informa a próxima sequência e a revisão de base;
- a ordem roda em cópia isolada do replay offline;
- validação do WorldState, vínculo, incremento de revisão e hash ocorrem **antes** da publicação;
- a sessão canônica aceita a revisão completa, então a cópia passa a ser a única fonte do laboratório;
- erro, sequência repetida, revisão obsoleta ou mundo estranho não comprometem estoques, trabalhadores ou relógio;
- tick 6 produz/consome apenas uma vez. O recibo explicita se houve liquidação;
- reabertura do save offline é uma ordem explícita, validada, que publica uma revisão sem liquidar produção;
- Godot deixa de chamar `MoveWorkers` e `AdvanceMovementTurn` diretamente no simulador persistente; seus objetos locais são cópias defensivas destinadas ao HUD.

## Limites explícitos
Isto é **uma etapa transitória**, não o resolvedor canônico final de turnos simultâneos: a matemática de produção/consumo continua no simulador VS07, reexecutado de forma isolada. A clonagem por replay tem custo crescente com o histórico e deve ser substituída por reducer de estado puro para partidas longas. O log de recibos é **volátil**, não o event store definitivo; não há ainda save/load integral de WorldState v9.

A política de seis turnos por ciclo foi mantida **provisoriamente** para proteger as regressões do VS07. A periodicidade adaptativa (faixas de 30 turnos e proporção econômica/movimento) permanece uma proposta de design ainda não incorporada ao contrato.

Não foram introduzidos mercados, transporte, combate, IA, crédito, bancos, cidades ou instalações obrigatórias no início neolítico. Trabalhador continua sendo residente, produtor e consumidor. A `main` e a PR #95 não são alteradas; nenhuma liberação MKT-01.

## Verificações
- testes VS10: sequência exata, rejeição de replay duplicado, rejeição de revisão obsoleta, falhas sem mutação, conservação de trabalhadores, hash v9 distinto, liquidação apenas no sexto movimento, restore sem segunda liquidação, isolamento de cópias;
- regressão VS07 e VS09 e compilação do projeto Godot original em Windows/Linux;
- **gate visual manual Windows/Intel UHD ainda requerido**, incluindo início do laboratório, transferência, seis turnos, save/reabertura, contornos colados ao terreno e ausência de cintilação;
- log esperado: `GA-VS10-CANONICAL-PHYSICAL-COMMITTED` com `hash_format=9`, `revision` crescente e `next_sequence`.

## Próxima etapa
Migrar o resolvedor físico para uma fase de resolução canônica com eventos determinísticos e persistência tipada versionada, removendo a dependência de replay do laboratório. Só depois integrar mercado e logística física às trocas de mercadorias.
