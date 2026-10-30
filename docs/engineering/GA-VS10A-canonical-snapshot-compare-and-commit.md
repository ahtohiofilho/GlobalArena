# GA-VS10A — Integridade transacional da sessão física canônica

Base: GA-VS10, PR #98 (sobre GA-VS09/PR #97 e GA-VS08/PR #95). Incremento isolado, sem alterações na main.

## Risco observado

No VS10, `CanonicalPhysicalTacticalSession` reconstruía um novo estado a partir de `Game.Snapshot`, mas o método de publicação `CanonicalGameSession.AcceptSnapshot` validava apenas a identidade de mundo e a monotonicidade da revisão. Um terceiro escritor poderia publicar um novo snapshot entre o cálculo e o commit do comando físico; sem verificação de identidade, uma transição planejada sobre estado obsoleto poderia sobrescrever o estado mais recente.

## Correção

- `CanonicalGameSession.AcceptSnapshotIfCurrent(expected, next)` compara por referência o snapshot esperado com o atual dentro do mesmo lock usado no ingresso público `AcceptSnapshot`, e exige exatamente **uma** revisão adicional.
- O resolvedor físico mantém uma referência ao último snapshot que ele próprio aceitou. Toda leitura produtiva e exportação de save verifica essa identidade antes de prosseguir.
- Publicação de `move`, `tick` e inicialização passa por compare-and-commit; a referência autoritativa só é atualizada após o aceite.
- Operações obsoletas falham sem acrescentar eventos físicos, consumir estoque ou incrementar o contador de turnos do resolvedor. A sessão externa que tomou a autoridade **não** é desfeita silenciosamente.
- Não foi criada uma segunda autoridade econômica nem adicionado um serviço de replay em memória.

## Gate

Testes para rejeitar replay sobre snapshot antigo, impedir escrita física depois de uma mutação externa e preservar hash/salvamento/reabertura V9 após doze turnos. Executar em Windows e Linux; compilar o projeto Godot original e manter regressões VS07/VS09/VS10.

## Limites deliberados

Ainda não há política de turnos simultâneos, persistência distribuída, resolução de mercado ou logística entre tiles. O intervalo de seis turnos é provisório; a proposta de periodicidade adaptativa não é alterada por este incremento. O teste visual no Windows/Intel UHD continua obrigatório, sem modificação de câmera, geometria, seleção ou HUD. Não fazer merge na main até avaliação dos gates; MKT-01 bloqueado.
