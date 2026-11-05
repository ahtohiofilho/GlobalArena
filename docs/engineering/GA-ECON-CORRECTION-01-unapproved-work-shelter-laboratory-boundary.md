# GA-ECON-CORRECTION-01 — Remover o WorkShelter não aprovado da experiência normal

**Data:** 2026-11-05  
**Base:** GA-UI-PRODUCTION-STATES-01, PR #263, HEAD `e72c74077b52f3a1d3c3a19fd5d43e4dfed4291a` (85/85 workflows e 6/6 regressões reais homologados).
**Estado desta entrega:** separação de produto / laboratório em PR Draft empilhada, sem merge.

## 1. Correção expressa de escopo

O usuário **não decidiu** que existiria um objeto econômico "abrigo primitivo de apoio à produção" (no código `PhysicalPrimitiveFacilityKind.WorkShelter`). Este nome e objeto foram introduzidos pelo implementador como **fixture de laboratório** para exercitar material debitado, dedicação exclusiva de trabalhadores, duração da construção, hash do WorldState e save/replay. A interface posterior os apresentou indevidamente como construção de jogabilidade.

**Decisão:** `WorkShelter` NÃO é construção produtiva aprovada, não é roçado, serraria, pedreira, instalação genérica de capital e não pode aparecer como ativo de valor econômico na experiência normal do usuário. "Operando" quer dizer somente que pessoas existentes estão vinculadas a esse fixture experimental — não é novo produto, produção adicional, vantagem, receita, capital ou saldo de compra. A representação deve deixar isso inequívoco.

## 2. O que preservar integralmente

- **Produção neolítica rudimentar** sem edifício ou infraestrutura obrigatória, com pessoas reais simultaneamente residentes e produtoras.
- Estoques físicos, origem de lotes, incorporação real de insumos, reserva exclusiva de trabalho, tempo de construção avaliado em ciclos econômicos efetivos e validação de consumo.
- Estado histórico `PhysicalPrimitiveFacility` (`WorkShelter=1`), eventos, hashes v27/v29 e envelopes de save v27–v30. **Não renumerar o enum, trocar campos persistidos, reescrever o journal, migrar/descartar patrimônio antigo, nem fingir que materiais foram devolvidos.** É um arquivo válido do laboratório antigo.
- `EmergentSettlementProjection` e o símbolo do assentamento único por tile habitado, sem categoria lógica aldeia/cidade/megalópole. Acampamento de viagem compartilha a física de população/transportes, com diferenças de mobilidade e permanência.

## 3. Comportamento visível após esta correção

**Modo normal, por padrão:**
- Mostrar pessoas, atividades, estoques, produção básica, movimentos, assentamento emergente e mapa tático horizontal.
- Não oferecer **MONTAR / CONSTRUIR / OPERAR** para um `WorkShelter` genérico, que não possui função econômica aprovada.
- Não desenhar telhado verde/cinza/andaimes ou marcadores do `WorkShelter` como se fossem edifícios válidos de produção.
- Não anunciar `INSTALAÇÕES n`, `OPERANDO n` ou `obra concluída` referentes aos protótipos no painel econômico normal.
- Se uma partida antiga contiver `WorkShelter`, continuar restaurando, validando hashes e avançando a simulação, **sem promover essa estrutura a instalação aprovada**; o desenho técnico só aparece quando explicitamente solicitado no laboratório.

**Modo diagnóstico, opt-in:**
- O painel existente `MOSTRAR DETALHES E FERRAMENTAS DE LABORATÓRIO` inclui `GAWorksiteCockpit` e `GAWorksitePulse`, ambos escondidos normalmente.
- Título e advertência clara: `LABORATÓRIO · CONSTRUÇÃO EXPERIMENTAL`; `WorkShelter não é uma instalação aprovada e não oferece efeito produtivo`.
- Botões recebem prefixo `LAB` e continuam exercitando os mesmos comandos canônicos sem alterar a história de saves.
- Checkbox `GAWorksiteExperimentalOverlayToggle`, **desmarcado por padrão**, revela somente no modo diagnóstico a antiga malha gráfica dos protótipos e as contagens correspondentes no hover. Ao fechar o painel, é desligado automaticamente.
- As três bolinhas de ocupação já eram um diagnóstico separado e mantêm seu comportamento.
- Guardião de evento nos comandos: clique/programmatic `Pressed` com painel fechado **não emite ação de construção**.

## 4. Contrato de implementação e preservação de investimento

O `PhysicalPrimitiveFacility.IsApprovedGameplayFacility` retorna `false` como **propriedade derivada não serializada**. Nenhum novo campo, código de item, dado monetário, regra ou versão de hash é introduzido. Analogamente, projeções dos marcadores documentam `IsApprovedGameplayFacility = false` para impedir confusão conceitual na integração da UI.

São deliberadamente mantidos os serviços que passaram pela validação multiplataforma:
`AssemblePrimitiveMaterialFrame`, `StartPrimitiveFacility`,
`StaffPrimitiveFacility`, `CanonicalWorldStateHasher`, `CanonicalPhysicalTacticalSession.RestoreSaveJson`, `CanonicalFacilityAssistedWorkWitness`.
Eles continuam úteis como harness técnico; não devem ser chamados por nenhum fluxo novo de jogabilidade normal enquanto os tipos reais e suas funções não forem aprovados.

## 5. Critérios para uma construção real no futuro (ainda NÃO aprovada)

Uma nova instalação produtiva só será promovida à jogabilidade quando houver decisão de produto sobre:
- **O que existe fisicamente:** finalidade e vínculo com uma atividade física/tecnologia concreta;
- **Por que o jogador investiria nela:** benefício/necessidade verificável, não um bônus inventado nem ornamentação sem função;
- **Como se constrói:** materiais específicos a essa instalação, origem/custo, tempo e trabalhadores — sem receita global de madeira/pedra;
- **Como entra na produção:** trabalhadores efetivos, capacidade, insumos, manutenção e efeito na produção realmente contabilizado (sem duplicação); preservação da produção neolítica sem prédio;
- **O que aparece no mapa:** mudança de aparência derivada do estado, sem criar objetos políticos ou categorias de cidades.

Até essa aprovação, **não criar nova construção fictícia para ocupar o lugar do WorkShelter**. O próximo investimento prioritário do produto continua ser tornar legíveis no tático os estados econômicos *já canônicos* (residentes, atividades produtivas, recursos e fluxos), sem amarrar a UI a protótipos não decididos.

## 6. Evidência e gates

- `GaVs124UnapprovedWorkShelterLabBoundaryTests`: o trabalho neolítico segue sem prédio, a instalação experimental nunca é autorizada como produto, a projeção é somente de diagnóstico, produção do operador não recebe bônus e saves antigos v29/v30 (com/sem tesouro) mantêm bytes e hashes/replay. Outros formatos históricos seguem cobertos pelas suites VS119–VS123.
- `UiVisualQA.gd`: a interface normal e o overlay ocultam `WorkShelter`; o botão de laboratório revela a advertência e o opt-in; a saída do laboratório desliga o overlay, sem inventar desenho de construção no mapa.
- `GA-QA-UI-E2E-001` + `GA-QA-VISUAL-002`: renderer Godot e navegação; regressões reais kernel+hash Linux/Windows/macOS Intel; FAST, benchmark e gates elegíveis do HEAD.
- A homologação visual no Windows permanece adicional à aprovação CI.

**Nunca reinterpretar este documento como pedido para apagar os saves existentes ou remover a infraestrutura de testes.** Esta é uma correção de exposição e domínio, não migração física ou perda de trabalho validado.
