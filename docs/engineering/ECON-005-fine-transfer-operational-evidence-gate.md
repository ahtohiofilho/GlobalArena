# GA / ECON-005 — Gate D2: evidência operacional de transbordo na malha fina

**Status:** experimento *opt-in* em PR, sem ativação de economia autoritativa; não é certificado de construção emitido pelo `WorldState`.

## Motivação

O Gate D1 (#42) verifica conectividade de um serviço sobre arestas físicas instaladas, tecnologias, versão do catálogo e posição exata do transbordo na topologia fina. Entretanto, `EconomicPhysicalTransferResourceBinding` é uma **declaração de recurso no local**, sem prova de que uma instalação existe ali, está operacional e autoriza a transição tecnológica. Uma instalação não pode ser inferida simplesmente porque duas arestas se encontram.

Esta entrega adiciona um **segundo portão de evidência**, sem modificar nem afrouxar o resolvedor D1:

- `EconomicFineTransferFacilityEvidence`: instalação identificada (`FacilityId`), recurso físico de custo (`ResourceId`), tile fino qualificado, tecnologia anterior e posterior, versão do catálogo e estado operacional; a autorização é **direcional**, não recíproca.
- `EconomicFineTransferFacilityAuthorizationSnapshot`: snapshot de um turno; rejeita recursos ou identidades de instalações incongruentes e permissões duplicadas. Pode representar várias transições explicitamente concedidas na mesma instalação e no mesmo tile.
- `EconomicFineAuthorizedServicePhysicalResourceAdapter`: camada estrita que exige que o snapshot pertença ao turno avaliado, vincula cada transferência à oferta e às duas arestas instaladas, verifica autorização operacional e **rejeita falta de evidência para uma oferta multimodal disponível ANTES de avaliar qualquer cotação de frete**. Em seguida reutiliza D1 para certificar continuidade de geografia, localização de handoff e condições dos ativos.

## Revisão estrutural do estado e rigor da evidência

A revisão do `GlobalArena.Runtime.WorldState` na `main` confirmou que os domínios persistidos atuais são civilizações, território, diplomacia, economia, população e guerra, além do vínculo ao mundo e da revisão. **Não há coleção de instalações construídas, equipamentos logísticos, propriedade de terminais ou suas capacidades operacionais no estado autoritativo.** `StrategicCellConstructionProvenance` representa proveniência de geração geométrica de células e **não** construções executadas pelo jogador.

Portanto, a autorização deste Gate D2 **não pode ser promovida a certificação de instalação construída** por confiar no `FacilityId`, no `ResourceId`, no `WorldState.Revision` ou no hash: nenhuma dessas informações demonstra a existência de um terminal. Um registro autoritativo futuro deve passar pelos contratos de persistência, invariantes entre domínios, hash canônico e atos de construção/desativação, antes de alimentar a evidência de turno. Até lá, todo resultado é de **proveniência atestada pelo chamador**, para cenários somente hipotéticos.

Na revisão adicional, o snapshot foi endurecido para rejeitar estado operacional contraditório de uma mesma instalação no mesmo turno, mesmo sob permissões tecnológicas diferentes. A autorização também rejeita explicitamente referências a trechos físicos ausentes **antes de precificar o frete**, em vez de lançar uma exceção incidental por busca de ID. Esses testes adversariais se somam aos do primeiro Gate D2.

## Fronteira de autoridade — crítica

**O snapshot continua sendo fornecido pelo chamador.** Ele **NÃO** consulta o mundo autoritativo para comprovar que a instalação foi construída, nem verifica permissão política ou de propriedade. O chamador deve no futuro obter os registros de um catálogo/registro de construções e instalações autenticado pelo `WorldState`, condicionado à versão do estado. Este portão apenas impede que o mercado trate um recurso simbólico sem permissão operacional declarada como um transbordo naturalmente disponível.

O adaptador anterior `EconomicFineServicePhysicalResourceAdapter.Resolve` continua intacto como referência exploratória para não quebrar testes e experimentos. Código futuro que precise de uma evidência adicional de transbordo deve escolher explicitamente a variante estrita. **Isso não promove o adaptador antigo à autoridade de execução do jogo.**

## Testes

Adicionadas verificações adversariais ao fixture de malha fina existente:
- permissão correta gera o mesmo recurso e itinerário do D1;
- instalação inexistente, não operacional, no tile errado ou com ID físico divergente é recusada;
- transição inversa, versão tecnológica diferente ou tecnologia não concedida são recusadas;
- snapshot de turno diferente falha;
- ID de instalação e recurso não podem representar duas instalações nem duplicar a mesma capacidade; tampouco a mesma instalação pode estar simultaneamente operacional e inoperante no snapshot;
- ausência de binding de tecnologia ou referência a trecho não instalado bloqueia o serviço **antes de chamar a função de preço**;
- trânsito na mesma tecnologia não cria terminal nem recurso de transbordo;
- auditoria de hash confirma ausência de mutação do `WorldState`.

Comando direcionado:
`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicFineServicePhysicalResourceAdapterTests`

Obrigatório: regressão completa, benchmark de frete e testes de hash nas matrizes Windows/Linux/macOS do SHA final.

## Complexidade e compromisso com a jogabilidade

Isto **não é um catálogo canônico de itens**. Instalação logística é identificada por sua função estratégica; peças, ferramentas e armaduras não são representadas como mercadorias intermediárias obrigatórias. O jogador não precisa administrar cada carregamento individual. A implementação é uma checagem técnica de admissibilidade futura, não um novo painel de operação.

**Não implementa** mutação do turno, escolha de preço/regime, abertura de corredor, contabilidade, bancos, crédito, novos itens, montagem de veículos ou instalação física de terminais.

## Próxima fronteira

Ao definir o modelo de construções, conectar a origem do snapshot à autoridade de instalações realmente construídas e operacionalmente disponíveis, vinculada ao hash/ordinal autoritativo do estado. Só então avaliar sua utilização pelo mecanismo econômico que liquida compras à vista. Até lá, usar este portão somente em avaliações hipotéticas.
