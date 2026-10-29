# GA — G3-J3c: autorização histórica e candidato conjunto de mundo/economia v9

**Status:** proposta *sem publicação*, baseada no PR #72; evita criar um segundo escritor do head. **Não** representa o fechamento transacional de turnos com comandos. Sem merge.

## Propósito

G3-J3b produz um transcript reproduzível `GAMVJ001` e reconsulta a autorização externa atual durante o replay. Após revogação das permissões, um movimento que foi autorizado no passado poderia ficar impossível de verificar. Adicionalmente, o resolvedor ecológico de G3-J3a opera no `WorldState` inalterado; ele não pode publicar diretamente uma alteração militar sem quebrar a suposição de que o mesmo hash do mundo acompanha o journal ecológico.

G3-J3c resolve **a verificação histórica de uma autorização** com chave confiável e constrói um **candidato não publicado** que inclui `WorldState` militar resultante + capital natural/estoque ecológico do mesmo turno.

## 1. Selo histórico GAHMAC01

`HistoricalMilitaryMoveAuthorizationSeal` oferece:

- `Issue(accepted, acceptedDigest, trustedContext, submitted, liveAuthority)`: chama **primeiro** `AuthorizedMilitaryMoveTurnAudit.Project`, que exige uma aprovação explícita do host para cada comando, aplica o validador militar original e registra o resultado do `TurnResolver` no transcript `GAMVJ001`. Somente depois emite um **MAC HMAC-SHA256 de 256 bits** sobre o registro histórico completo.
- `Verify(accepted, acceptedDigest, independentlyTrustedContext, certificate)`: valida o MAC com chave histórica de host, identidade de chave, compromisso aceito v9, mundo de origem, turno e semente vindos de fonte confiável, extrai o transcript e **reexecuta** a resolução pelo motor com as validações de domínio. O serviço de permissões atual não é consultado **após** verificar o MAC original. A mudança posterior das ACLs não apaga a validade histórica do resultado.

O envelope carrega magic `GAHMAC01`, versão 1, **keyId** de 1–128 caracteres ASCII imprimíveis, número do turno, semente, digest do checkpoint v9, digest do `WorldState` v9, tamanho explícito, transcript `GAMVJ001` e MAC final. O decoder tem limite de 16 MiB para o transcript e verificações de comprimento antes de alocar. A chave secreta é injetada pelo host (mínimo de 32 bytes), copiada defensivamente e **nunca gravada no envelope ou save**.

**Escopo de confiança:** HMAC é autenticação **simétrica do serviço emissor**, não assinatura pública, prova de identidade do jogador, PKI, carimbo de tempo independente ou não repúdio. O servidor deve autenticar jogadores antes de emitir, controlar a chave e guardar versões antigas em armazenamento protegido durante todo o período de retenção. O `keyId` identifica a chave esperada, mas não autoriza usá-la por si só. Se a chave do host for comprometida, certificados falsificados podem ser aceitos. O digest do checkpoint e o contexto esperado devem vir de uma fonte externa confiável.

## 2. Candidato conjunto GAFULL09

`HistoricalMilitaryEcologicalTurnProjector.Propose` recebe checkpoint anterior confiável, contexto confiável, certificado histórico e demandas/retenções de trabalhadores:

1. exige turno ecológico exatamente subsequente e compromisso v9 anterior aceito;
2. verifica o MAC e o replay militar, incluindo os eventos executados e rejeitados;
3. obtém o `WorldState` resultante **real do `TurnResolver`**, com mudanças em guerra e revisão;
4. impede mudanças em outros domínios durante o movimento: civilizações, território, economia/trabalhadores, população e instalações físicas devem permanecer idênticos;
5. reconstrói checkpoint intermediário com o `WorldState` militar e **os mesmos livros imutáveis** de capital natural e reservas anteriores, sem reinicialização;
6. resolve a produção ecológica, consumo dos trabalhadores e retenções deliberadas para o novo turno sobre o **mundo militar atualizado**;
7. constrói um `GAFULL09` completo com o mesmo hash v9 de instalações/estado militar e o novo compromisso ecológico, e exige round-trip canônico.

`HistoricalMilitaryEcologicalTurnCandidate` contém o transcript auditado, novo checkpoint proposto, bytes completos do save e certificado, **todos sem publicação**. Os arrays de bytes são disponibilizados como cópias. O candidato não altera a sessão, a head durável `full-v9-head.gahead`, o journal `GAJRN001`, nem o estado anterior. O save `GAFULL09` resultante pode ser verificado, mas **não é um commit**, mesmo que seus bytes sejam válidos.

## 3. Testes

`G3J3cHistoricalAuthorizationCombinedV9Tests.cs` cobre 7 casos:

1. autorização selada válida permanece reexecutável após revogação da permissão atual e reprodução determinística;
2. serviço emissor não sela comandos não autorizados, de outro turno ou lote vazio;
3. recusa de chave errada, `keyId` trocado, turno/seed incorretos, checkpoint errado e outro mundo;
4. adulteração do registro, truncamento e recomputação de SHA simples sem chave HMAC rejeitados;
5. movimento militar efetivo e saldo de madeira retida coexistem no **mesmo save v9** com instalações e trabalhadores preservados;
6. bytes idênticos na repetição e recusa de salto de turno/hash stale;
7. proposta de mudança militar **não avança head, sequência nem journal ecológico**.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3J3cHistoricalAuthorizationCombinedV9Tests
```

## 4. Gate ainda necessário: commit geral único

**Não** conectar `HistoricalMilitaryEcologicalTurnCandidate` diretamente a `EconomicCompleteV9DurableSaveHost.CommitNext`: este host e seu verificador de `GAJRN001` atualmente pressupõem que o hash do `WorldState` permaneça idêntico entre turnos. Isso recusaria (corretamente) uma transição militar real.

O próximo gate deve versionar um journal **misto** com certificado de comando, input econômico, hash v9 anterior/posterior, eventos/flags, proposta ecológica, hash de resultado e cadeia de autorização; só então publicar **um único** `GAFULL09` e um único head após verificar replay de todo o turno e política de fechamento. Deve definir recuperação de crash entre disco, confiança externa e sessão. Até lá, o G3-J3c é apenas fase de pré-confirmação.

Nenhum novo estoque de mercadorias é criado por compra não entregue, nenhuma instalação é fabricada e a demanda continua calculada por trabalhadores, não duplicada a partir do censo legado.

**PR em rascunho, sem merges em main, #49, #50 ou PRs da pilha G3.**
