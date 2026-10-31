# GA-VS55 — Titularidade institucional e receita hipotética de frete

**Base:** GA-VS54 PR #161, commit `9e92bba529949cbbaa0e33a47852c62113b9cbaa`, **20/20 workflows homologados**. **Status:** laboratório, PR draft stacked; `main` não alterada.

## Decisão de arquitetura

**Serviço de transporte, instituição operadora e instituições beneficiárias da receita são identidades diferentes.** Uma rota física não constitui contrato jurídico nem atribui receita automaticamente ao comprador ou ao produtor. Instituições são abstrações econômicas agregadas, **não trabalhadores com carteiras bancárias**. Os trabalhadores continuam simultaneamente residentes, produtores e consumidores, com poder de compra agregado por povoamento (VS52).

Uma instituição pode ser pública, comunitária, cooperativa ou operadora comercial, mas seu tipo e ID precisam ser explicitamente declarados por uma fonte institucional externa. O mesmo identificador não pode ter categorias contraditórias no registro. Um operador pode ser beneficiário **se e somente se** sua instituição constar expressamente da repartição da receita.

## Implementado

`ExperimentalTransportInstitutionalRegistry` recebe ID/versão de registro, hash físico do mundo, revisão da rede (etiqueta externa), ID/versão de política de poder de compra, ID/versão de calibração, unidade nominal VS53 e uma cobertura **exata de todos os serviços VS52 declarados**, inclusive os que não foram escolhidos na configuração hipotética. A coleção de direitos e os percentuais são copiados defensivamente.

Cada `ExperimentalTransportServiceEntitlement` identifica `(Source, Destination, ServiceId)` e contém: `Operator` explícito e uma lista explícita de `RevenueShares`, cada uma com instituição beneficiária e fração racional positiva. Percentuais de cada serviço **devem somar exatamente 1**, sem sobra invisível, imposto presumido ou recebimento duplicado. Dois serviços podem ter a mesma instituição, mas uma única identificação de direito por serviço.

`IExperimentalTransportInstitutionalRegistryAuthority` é uma interface de **aceitação institucional externa e mandatória**; não existe implementação padrão permissiva no kernel. A versão de rede é **declarada** e a autoridade externa precisa certificar sua legitimidade. O adaptador confere efetivamente `session.CurrentHash`, versões de poder de compra e calibração, unidade e cobertura; uma declaração que se autodescreve como válida **não comprova legalmente** o direito. O benchmark usa um stub próprio de laboratório, **não** uma autoridade real.

A VS55 chama **de novo a VS54** a partir dos inputs físicos e econômicos originais, incluindo os serviços ofertados íntegros; não confia em planilhas nem em um resultado de custo isolado. Para cada lote selecionado, aplica ao frete **calculado pela VS42** e convertido na escala VS53 as frações de seus beneficiários. Confere `Σ atribuições do lote = débito hipotético do lote` e `Σ instituições = Σ compradores = frete físico total`, com frações exatas (`BigInteger`). A receita atribuída é **meramente prospectiva**. A despesa das mercadorias é preservada separadamente no resultado VS54 e não atribuída como receita logística.

O retorno `ExperimentalTransportInstitutionalProjection` contém o operador de cada entrega, os beneficiários e montantes por lote, os totais de cada instituição e a reconciliação física integral. `CanAuthorizeRealSettlement = false` **incondicionalmente**, independentemente da aceitação da autoridade ou da capacidade abstrata do comprador.

## Exemplo de referência — direitos de operação e receitas diferentes

Duas entregas no mesmo corredor custam juntas 10 unidades abstratas de frete, com 5 por entrega.

- Serviço 17: operado por LOGISTICS-A, mas receita atribuída **75% à comunidade B (3,75)** e **25% ao poder público C (1,25)**.
- Serviço 18: operado por LOGISTICS-A, receita **100% à comunidade B (5)**.
- Repartição agregada: **8,75 à comunidade B + 1,25 ao poder público C = 10**. A operadora LOGISTICS-A **não recebe crédito automático**.
- O preço das duas mercadorias não faz parte da receita institucional do frete.

Essas proporções são **fixtures**, não imposto, tarifa ou repartição canônica. Não foi escolhido um modelo definitivo de propriedade de infraestrutura ou contrato de concessão.

## Segurança e limites

- O hash da sessão é conferido, mas o registro institucional ainda **não** está armazenado nem autenticado pelo mundo do jogo. A revisão da rede é etiqueta declarada: a autoridade host deve fornecer vínculo confiável com snapshot físico/network revision real e prova de titularidade.
- Aceitação da autoridade `AcceptReadOnlyRegistry` é somente **sinal de auditoria do laboratório**; não é assinatura, mandato irrevogável ou autorização de `SETTLE`.
- Um serviço sintético de testes é aceito pela cadeia VS54 somente para fixtures; produção futura exige `FromBuiltOffer` e verificação robusta de autenticidade, ativos construídos, acesso, vigência institucional, autoria e seleção simultânea.
- O laboratório não altera saldos, créditos V14, habitantes, carreiras, movimentação, saves, turnos, eventos, Godot ou a `main`. Não cria carteira de transportador, banco, dívida ou saldo privado.
- A VS54 avalia uma **seleção exógena de entregas**, não a carteira ótima nem uma liquidação de ativos reais. VS55 não liquida receita; registra apenas **direitos hipotéticos associados à configuração**.

## QA e benchmark

Testes: separação operador/beneficiários; cobertura de serviços completa, nenhuma instituição implícita; receita exata por serviço/instituição; dois povoamentos com orçamento separado; frações exatas; lote vazio não gera renda; omissão/duplicação/excesso de direitos; percentuais que não somam 1; beneficiário duplicado; tipos institucionais contraditórios; unidade/hash/versão diferentes; autoridade rejeitando registro; defesa contra mutação do input e serviço de frete de mesmo ID com tarifação adulterada; ordem determinística; hash/save/caixa V14 imutáveis.

Benchmark `--vs55-institutional-freight`: **8/32/128 repetições do mesmo lote com um comprador e dois beneficiários**. Não representa múltiplas cidades, impostos ou escala de mercados.

CI Linux testes dirigidos VS40–VS55, benchmark e Godot Release; Windows suíte integral e Godot Debug. A homologação depende dos workflows da **mesma versão**.

## Próxima fronteira técnica

Construir a autenticação institucional real e uma referência de seleção simultânea que concatene orçamentos abstratos, volumes, preços, frete recalculado e titularidade; ligar a uma auditoria independente orientada ao mundo físico. Manter `SETTLE` bloqueado até garantir origem de direitos, equivalência de unidades, custos reais e autorização formal, sem recair no caixa privado por trabalhador.
