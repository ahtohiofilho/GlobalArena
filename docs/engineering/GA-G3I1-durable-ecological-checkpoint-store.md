# GA — G3-I1: armazenamento de checkpoints ecológicos por conteúdo e head durável

**Status:** implementação de referência de arquivo local efetivo, com aceitação de turno em um processo, empilhada sobre G3-H (#62). **Não é um save completo de partida nem uma nova versão do hash de `WorldState`.**

## 1. Contexto

O G3-H permite escrever o sidecar ecológico em bytes, mas ainda usa um `EconomicEcologicalCheckpointHost` apenas em memória. O G3-I1 acrescenta gravação concreta em disco e restauração de um checkpoint aprovado após encerramento do processo.

Os PRs #49/#50 continuam abertos. O #50 propõe a **seção 35 e o hash de `WorldState` v9** para identidades de instalações de transporte. A branch G3-I1 continua sobre G3-H e **não cria um hash concorrente**, nem inclui artificialmente instalações em sua própria estrutura. O sidecar contém `WorldStateHash.FormatVersion` e seu digest, portanto uma futura versão v9 poderá ser vinculada pelo mesmo formato de sidecar depois de uma integração real e validada entre as branches.

## 2. Escrita endereçada pelo conteúdo

`EconomicEcologicalSidecarArchive`:

- aceita somente diretório absoluto, controlado pelo host, e digest SHA-256 válido;
- nomeia blobs imutáveis pelo compromisso canônico: `<SHA256>.gaeco`;
- codifica o sidecar com `EconomicEcologicalSidecarCodec` de G3-H;
- escreve primeiro um arquivo temporário no **mesmo diretório**, descarrega seu conteúdo com `FileStream.Flush(true)`, e só depois o instala com `File.Move` sem sobrescrita;
- é idempotente para o mesmo digest + bytes e **recusa** substituir conteúdo diferente/corrompido sob nome já existente;
- recusa leitura de outro mundo/versão de hash/estado econômico incoerente por meio do decodificador existente.

Arquivos temporários após crash e blobs sem head são **órfãos**, não checkpoints aceitos. Limpeza de órfãos deve ser uma tarefa separada que nunca apague arquivos referenciados pelo head.

## 3. Head de aceitação em disco

`EconomicEcologicalDurableCheckpointHost` possui um único registro `ecological-head.gahead` com magic, versão 1, formato do hash do mundo, digest do mundo, compromisso ecológico aceito, número do último turno e checksum do registro.

Protocolo `CommitNext`:

1. obtém lock em memória e checa o **digest esperado do último checkpoint**;
2. checa o head atual (hash do mundo, compromisso, turno);
3. calcula a transição econômica/ecológica com G3-G e valida o turno;
4. grava em disco o blob completo do próximo checkpoint;
5. escreve um novo head temporário, faz flush e o instala por rename/replace **no mesmo diretório**;
6. apenas após a publicação do head muda a referência de `_accepted` em memória.

Um erro **antes** do passo 5 mantém o head anterior; um crash após o passo 4 pode deixar um blob órfão que não será carregado como turno aceito. Um crash após a publicação do head permite restaurar o novo checkpoint, desde que o blob não tenha desaparecido/corrompido.

A exclusividade do escritor é protegida pelo handle aberto em `.ecological-writer.lock` com `FileShare.None`. Isso é uma disciplina de **um escritor local**, não concorrência distribuída, nem garante durabilidade sob todos os sistemas de arquivos/falhas de energia. `Flush(true)` descarrega conteúdo, mas nem sempre garante fsync do diretório/metadados em todas as plataformas.

## 4. Segurança de restauração e rollback

`Restore(directory, generatedWorld, world, externallyTrustedAcceptedDigest)` exige:

- o último digest confiável **fornecido de fora** do diretório de sidecars;
- registro head íntegro e com digest exatamente igual ao digest confiável;
- mesmo `WorldStateHash`, inclusive **versão**;
- blob canônico existente e válido sob o digest do head;
- último turno ecológico do blob igual ao turno do head.

Se um operador malicioso reverter **head + blob** para uma versão antiga auto-consistente, só um compromisso confiável armazenado separadamente pode detectar o rollback. O head local e o checksum não são assinaturas nem substituem serviço de confiança. A fonte externa do digest ainda precisa de durabilidade, ACLs, autenticação e estratégia de atualização coordenada.

## 5. Testes

`GlobalArena.Tests/G3IDurableEcologicalCheckpointTests.cs` inclui:

1. persistência imutável e idempotente por compromisso, recusa de conteúdo corrompido;
2. commit físico, head avançado somente depois de blob completo, reabertura do host e segundo turno;
3. arquivo futuro não publicado (órfão) não avança o turno, mesmo que o blob exista;
4. rollback de head antigo recusa restauração com compromisso confiável mais recente;
5. recusa de hash de mundo diferente e corrupção do head;
6. transição inválida não altera registro, digest ou arquivo;
7. segundo escritor simultâneo e tentativa com CAS desatualizado recusados;
8. perda de blob referenciado causa recusa na restauração, não reconstrução fictícia.

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G3IDurableEcologicalCheckpointTests
```

O commit exato deverá passar as matrizes kernel/hash/benchmark em Ubuntu, Windows e macOS. Se houver falhas de compartilhamento/rename em uma plataforma, corrigir sem relaxar a rejeição de corrupção e rollback.

## 6. Limites e próxima integração autoritativa

- O `WorldState` **não é persistido neste incremento**. A restauração requer `WorldState` carregado de outra fonte confiável com digest e formato idênticos. Portanto G3-I1 é persistência **do sidecar**, não um save/replay de partida completo.
- O head local é publicado depois do blob, mas ainda não faz uma **transação distribuída** com a fonte externa do digest confiável; crash entre atualização dos dois sistemas requer protocolo de recuperação explicitamente definido antes de gameplay em produção.
- Nenhum evento de mercado, entrega física/comercial, construção de instalações ou fundação territorial é acrescentado.
- A população econômica do Neolítico continua sendo a dos **trabalhadores que produzem e consomem**, com censo legado apenas por compatibilidade; nenhuma segunda demanda de residentes.
- G3-I2 futuro: conciliar PR #49/#50, escolher hash do estado global posterior à v9, migrador de saves, journal de comandos autenticamente aceitos e única transação persistida para `WorldState` + ecologia + materiais.
- No avanço para infraestrutura: ocupação e produção básicas continuam sem custo obrigatório de madeira/pedra, e construções melhoradas exigirão receitas reais e controle de tile fino.

**Nenhum merge automático.** Branch isolada de `main`.
