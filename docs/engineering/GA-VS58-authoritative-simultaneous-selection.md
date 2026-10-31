# GA-VS58 — Seleção simultânea com autoridade da sessão

Base: VS57 PR #164, commit `50e0827b586c097fb3e35903f36adb8593612dcd`.
PR draft empilhado; main preservada; SETTLE real bloqueado.

`CanonicalSimultaneousTransportSelector` enumera conjuntamente volumes
quantizados e serviços por lane. Usa os fluxos canônicos como limites de oferta
e demanda agregadas, inclusive quando lanes competem pelos mesmos recursos.
Cada configuração elegível passa pela auditoria VS57 completa: direitos da
sessão, reconstrução física, saturação compartilhada VS42, reconciliação VS54,
partilha VS55, orçamento abstrato e H. Configurações sem orçamento são excluídas.
Nenhum snapshot de caixa privado é criado. A configuração vazia é candidata.

O objetivo explícito deste laboratório é maximizar a soma das frações de demanda
atendidas (quantidade / demanda de cada par comprador-mercadoria); em empate,
minimizar o custo entregue abstrato. Empates finais seguem ordem estável de
origem, mercadoria, atividade, comprador e serviço. Não é utilidade, justiça
distributiva, equilíbrio de mercado ou política econômica definitiva.

A busca é exaustiva somente no domínio finito declarado: até 128 lanes e
100.000 configurações (padrão 4.096). Excesso é recusado antes da enumeração,
sem retornar falso ótimo parcial. Resíduos inferiores ao quantum não são
negociados. Capacidade física segue o contrato de saturação compartilhada
existente; não inventamos capacidade rígida de engenharia para estradas.

Ao final, a escolha é reconstruída pelo projetor VS57, independentemente do
recibo armazenado durante a busca. `RequireCurrent(session)` invalida resultados
após mudanças físicas, de rede, de titularidade ou permissões; sessão estrangeira
também é recusada. O resultado não é comando de execução e nunca autoriza SETTLE.
Ofertas não selecionadas também devem ter autoridade vigente: não podem ser
usadas para esconder permissões inválidas escolhendo zero comércio.

Testes cobrem seleção conjunta de duas mercadorias com corredor compartilhado,
permutação de lanes, orçamento inviável, zero comércio, limites de busca,
auditoria reproduzida e provas desatualizadas. Os contratos VS52–VS57 permanecem
em regressão. Benchmark `--vs58-simultaneous-authority`: 8/32/128 repetições de
uma rota com três configurações; não representa 128 cidades. Gate Linux:
VS40–VS58, benchmark e Godot Release; Windows: suíte integral, benchmark e Godot
Debug. O status de homologação do commit final será registrado no PR após CI.
