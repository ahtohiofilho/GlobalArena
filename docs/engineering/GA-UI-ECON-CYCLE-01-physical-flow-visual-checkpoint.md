# GA-UI-ECON-CYCLE-01 — checkpoint visual do primeiro fluxo econômico físico

**Data:** 2026-11-06. **Base:** GA-ECON-BENEFICIARY-01 / PR #273. **Prioridade:** homologação visual ANTES de novas features de transporte.

## Marco de produto (decisão)

Chegamos ao primeiro percurso verificável do ponto de vista material: **pessoas reais → ciclo produtivo → estoque físico → compromisso de oferta/retenção → utilização local por grupo de trabalhadores → save/replay**.

A partir daqui, a diretiva de execução é: **interromper o acúmulo de incrementos puramente internos e priorizar UX/observabilidade na UI tática**, com revisão visual pelo usuário, antes de retornar à rede física de transporte remoto. A tela não pode tratar uso material como venda monetária, nem pressupor N derivado ou cadência-alvo implantada.

## Entrega neste checkpoint

- `CanonicalTacticalPhysicalFlowProjection.Read(session, tile)`: modelo somente-leitura de um tile real; soma saldo livre de alimento/madeira/pedra, ofertas e retenções ativas, material comprometido efetivamente aplicado, último grupo beneficiário e estatísticas explícitas de **ciclo global**; testa hash/journal não alterados.
- `Main.EconomicPhysicalFlowHud.cs`: componente do **painel direito existente**. Mostra leitura imediata com grupos separados `LIVRE`, `VINCULADO`, `USO LOCAL` e `CICLO GLOBAL`. Nunca sobrepõe conteúdo ao mapa, não abre um popup de debug ou viewport de laboratório separado.
- Ações de LABORATÓRIO ficam em foldout fechado por padrão: `RESERVAR OFERTA`, `RETER MATERIAL`, `UTILIZAR NO GRUPO` e `LIBERAR RESERVA`. Os botões validam a custódia e a atividade real antes de mutar a partida; o grupo é identificado pelo tile e pelo número de trabalhadores alocados (não é pessoa jurídica/comprador real).
- `Main.NeolithicWorkerLab.cs`: controles extensos de alocação, deslocamento, exploração e diagnóstico passam para `TRABALHO E MOVIMENTAÇÃO`, fechado por padrão. Os botões de passagem de turno/ciclo e salvar/reabrir seguem disponíveis no nível principal, sem barras desnecessárias acima/abaixo do viewport.
- Nenhuma mudança na câmera, malha, fronteiras, terreno, modos de retenção/mercado, saves ou cronologia. Estados históricos continuam intactos.

### Limites explícitos

1. A interface não pode apresentar a liquidação `LocalCashSettlement` (V12 legado) como poder de compra abstrato aprovado.
2. A linha `CICLO GLOBAL` soma mercadorias heterogêneas somente como TELEMETRIA da simulação, nunca como receita, PIB, preço ou oferta local por item.
3. A utilização local de madeira/pedra pode ser comprovada, **mas não há venda, preço, imposto, comprador ou entrega intertile**; o label da UI explicita isso.
4. A economia nutricional de seis alimentos ainda será integrada; o saldo alimentar V12 é legado.
5. Sem valor de preço por unidade, verba invisível, crédito ou teto de venda por D.

## Checklist de aceite visual (Godot Windows 4.7.2 Mono)

Abrir a cena `Main` na branch deste PR por worktree isolada, **compilando Debug**, importando recursos do projeto e executando direto (sem F5/editor), usando a receita reproduzível de QA visual previamente homologada para PR #264.

1. Confirmar abertura sem tela preta; mapa esférico e viewport táctico preservados e com área ampla; evitar faixas horizontais de debug.
2. Confirmar que o painel direito não espreme o mapa nem corta os títulos `MATERIAIS`, `TRABALHO E MOVIMENTAÇÃO` e `AVANÇAR 1 CICLO`.
3. Antes do primeiro ciclo, verificar `CICLO: ainda não houve fechamento econômico`, sem valores inventados.
4. Avançar um ciclo real; observar aumento do estoque material / produção total global e consumo alimentar efetivo; diferenciar `CICLO GLOBAL` de tile local.
5. Expandir `ABRIR OPERAÇÕES FÍSICAS (LAB)`, selecionar madeira, `RESERVAR OFERTA`: diminuição do estoque livre e aumento do compromisso sem pagamento.
6. Selecionar um grupo real de trabalhadores e `UTILIZAR NO GRUPO`: desaparece o compromisso, aparece `USO LOCAL` com pessoas/atividade; confirmar que nenhuma venda/receita é anunciada.
7. Em outro material, `RETER MATERIAL`, depois `LIBERAR RESERVA`: o estoque livre retorna exatamente ao ponto anterior.
8. Salvar e reabrir: mesma custódia e dados canônicos; reinspecionar o tile para confirmar o histórico material (sem exigir que o bookmark visual seja persistido). 
9. Alternar seleção de tiles e expansão do painel: sem deslocar câmera tática, trocar elementos econômicos de um tile com outro ou reabrir laboratórios antigos automaticamente.
10. Inspecionar resoluções menor/maior, rolagem e botões, verificando ausência de sobreposição com o viewport.

Se alguma etapa falhar visualmente, **corrigir a UI neste mesmo ciclo de homologação**, antes de iniciar o próximo PR de frete. Nenhum "PASS" de C# substitui screenshot e aprovação visual.

## CI / avanço

FAST direcionado: regressão sobre a projeção física, build Godot Release e, se UI detectada, Windows Debug. O novo PR permanece em Draft. Regressão completa multiplataforma somente no checkpoint/antes do merge. Qualquer job `skipped` não é evidência de sucesso.

Próxima feature de motor **após aceite da interface:** `GA-ECON-REMOTE-DELIVERY-01`, com origem/destino, custo/tempo efetivos e entregador autenticado, sem teleportar bens.
