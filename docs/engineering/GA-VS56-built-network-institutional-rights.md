# GA-VS56 — Rede construída e direitos institucionais de exploração

**Base:** VS55 PR #162, commit `70dccaa606606508151003ed7a183d775fc1ce90`, 21/21 workflows homologados. **Implementação experimental em branch/PR draft empilhado**, sem merge ou alteração da `main`.

## Decisão arquitetural

Uma rede física construída **não** concede, por si só, permissão econômica de exploração. Um serviço comercial **não** prova quem é o dono da infraestrutura; operar um serviço **não** confere automaticamente participação na receita. A VS56 exige um encadeamento explícito e revalidado:

1. **Topologia e infraestrutura:** células de origem e destino vinculadas aos povoamentos VS52; os ativos devem ser construídos, operacionais, contíguos na grade fina, com tecnologia catalogada e travessias de terra atualmente acessíveis, revalidados por `ExperimentalSharedFreightOption.FromBuiltOffer` VS42 sobre a sessão viva.
2. **Serviço comercial:** cada proposta VS41 deve corresponder exatamente a um serviço VS52, com o próprio objeto efetivamente ofertado; refazemos preço/geografia e comparamos campos completos da tarifa e o itinerário físico VS42, não somente o `ServiceId`.
3. **Direito de exploração por ativo:** cada travessia usada deve ter `ExperimentalBuiltFreightAssetGrant` com ID do ativo, **instituição proprietária**, **operadora autorizada**, ID do contrato e intervalo de revisões de rede inclusivo. Um mesmo ativo pode servir diversos itinerários; não se deduz acesso exclusivo de geometria compartilhada nem da identidade do transportador.
4. **Titularidade e receitas:** o operador do serviço deve ser exatamente a instituição autorizada em cada um dos ativos, segundo o registro VS55. Os beneficiários e frações de receitas permanecem **separados** do operador e da propriedade do ativo. VS55 recompõe de novo a VS54 e reconcilia os custos compartilhados VS42, sem tributar ou creditar ninguém por inferência.
5. **Dupla autoridade externa obrigatória:** a autoridade do registro VS55 e a nova `IExperimentalBuiltTransportNetworkAuthority`. Nenhum aceite permissivo padrão no kernel; um **stub de testes** serve apenas ao laboratório. O host é responsável pela autenticidade da fonte dos ativos construídos, do estado de acesso, da revisão de rede e dos contratos.

### Identidade de revisão

`ExperimentalBuiltTransportNetworkWitness` mantém `WorldStateHash` tipado, ID/versão da testemunha e revisão da rede. O hash deve coincidir com a sessão física atual e com o registro VS55; a revisão de rede **deve coincidir exatamente**, não apenas estar acima ou abaixo do registro. Autorizações por ativo também precisam estar vigentes no valor exato dessa revisão, nem futuras nem vencidas.

A coleção de serviços da testemunha cobre integralmente a proveniência VS52, inclusive alternativas não selecionadas, e corresponde integralmente ao conjunto de `offeredLanes`. O registro institucional fornece operador para cada serviço. As permissões de infraestrutura têm correspondência exata com os pares `(AssetId, InstitutionId)` utilizados, rejeitando permissões extras, duplicadas, conflitantes ou sem uso.

### Tipos

- `ExperimentalBuiltFreightServiceEvidence`: oferta VS41 integral + objeto de serviço VS42 realmente passado às projeções VS52–VS55.
- `ExperimentalBuiltFreightAssetGrant`: proprietário, operador com permissão, contrato, vigência e ativo físico.
- `ExperimentalBuiltTransportNetworkWitness`: prova **declarada** da rede corrente com cópias de coleções, rotas e permissões; o testemunho só é aceito se revalidado e autenticado pelo host.
- `ExperimentalBuiltFreightInstitutionalRightsProjector`: gate somente leitura que recompõe os direitos e o custo econômico institucional usando VS55 sobre os inputs originais.
- `ExperimentalBuiltFreightRightsProjection`: revisão, hash, serviços físicos e autorizações por ativo, auditoria VS55 embutida. `CanAuthorizeRealSettlement = false` incondicionalmente.

## Limites de segurança

- A validade institucional ainda é **atestada pelo chamador**; não há no mundo autoritativo persistido um registro de propriedade, concessões, contratos, revisão global de rede e permissões assinadas. O kernel não autentica a procedência de `InstalledAssets` e `StrategicEdgeAccessSnapshot` sozinho. A exigência de autoridade externa impede aceite implícito, mas **não substitui integração nativa confiável**.
- As funções `FromBuiltOffer` validam estrutura e estado dos ativos **fornecidos**, não estabelecem sua titularidade real. A VS56 vincula permissões explícitas às rotas fisicamente verificadas, mas a autoridade institucional deve confirmar que a permissão foi legalmente emitida e ainda vale na revisão corrente.
- Não há liquidação, operador com carteira, imposto, empréstimo, crédito, lucro monetário privado, salário ou lançamento bancário. O poder de compra dos trabalhadores continua abstrato; `H` permanece consumo invisível protegido.
- Não altera `CanonicalPhysicalTacticalSession`, caixa V14, história do jogo, eventos, saves, UI, turnos, Godot nem `main`.
- Não executa otimizador de transportes/mercadorias nem comprova seleção simultânea efetiva: os lotes de VS54 continuam hipóteses e o frete compartilhado é calculado para a configuração selecionada.

## QA e benchmark

Fixture com **estrada operacional realmente construída** entre dois tiles táticos de terra, trabalhador deslocado a um povoamento adjacente, rota ofertada pela VS41 e opção reconstituída com VS42. Confere direito de exploração, vigência e operador da VS55, conservação das receitas, hash/save/caixa V14 e idempotência somente leitura.

Casos negativos: estrada fechada, ativo inoperante, permissões ausentes, vencidas, prematuras, duplicadas, para operador errado, ativo inexistente, propriedade conflitante, revisão ou hash divergente, âncoras trocadas, custo/rota adulterados, serviço faltando ou duplicado e rejeição de qualquer uma das duas autoridades externas.

Benchmark `--vs56-built-rights`: 8/32/128 **repetições de uma única estrada, um comprador, uma rota e uma operação com um beneficiário**, excluído worldgen. Não representa 128 cidades nem múltiplas rotas dinâmicas. CI: Linux testes dirigidos VS40–VS56, benchmark, Godot Release; Windows suíte integral e Godot Debug.

## Próximo incremento

**GA-VS57** deve fechar a origem autoritativa das revisões e permissões da infraestrutura ou viabilizar uma referência de seleção simultânea já acoplada à capacidade econômica, sem tratar auditoria somente leitura como assentamento comercial. `SETTLE` permanecerá bloqueado até haver uma cadeia autenticada end-to-end e um gate econômico de autorização independente.
