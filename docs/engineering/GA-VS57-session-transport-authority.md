# GA-VS57 — Autoridade de transporte vinculada à sessão

Base: VS56, PR #163, `af65a432376508d49e87aeeff925786f2a27a60b`.
Branch/PR draft empilhado sobre VS56; não mesclar nem alterar `main`.

## Origem da autoridade

`CanonicalPhysicalTacticalSession.TransportNetwork` pertence à sessão viva.
O registro começa vazio. Comandos explícitos de construção, estado operacional,
acesso, transferência de titularidade, emissão/revogação de concessões e
titularidade das receitas alteram esse registro e incrementam sua revisão.
Não existe importação de testemunha externa nem callback permissivo na API VS57.
Construção exige ativo único, aresta da resolução fina correta e terra física
trabalhável na geografia canônica. Propriedade é registrada separadamente de
operadora e beneficiários; transferência revoga as concessões do ativo.
Partilhas institucionais usam frações exatas que somam um, sem carteiras privadas.

Assim como ordens canônicas de trabalhadores, os comandos são a fronteira
confiável do host local. Não implementam autenticação de usuário remoto,
assinatura jurídica, custo/material de construção ou nova interface de jogo.
Uma instituição não se torna proprietária por apresentar um grant: o dono deve
coincidir com o registro instalado previamente por comando da sessão.

## Prova e verificação independente

`Capture` emite `CanonicalTransportProof` sem construtor público, com referência
à sessão, hash físico, revisão da rede e digest SHA-256 do diário de comandos.
Ativos, acesso, concessões e receitas vêm exclusivamente dos registros atuais.
O chamador fornece ofertas e metadados de calibração, que continuam sujeitos
aos contratos VS52–VS56; não fornece a autoridade que os aprovará.

`CanonicalTransportFreightProjector` verifica a prova contra o registro vivo,
antes/depois da materialização dos inputs, nas duas verificações institucionais
e ao final. Valida titularidade, identidade operacional, concessão vigente e
acesso e executa a auditoria física/econômica VS56 completa (incluindo VS55,
VS54 e reconstrução VS42). A prova não valida sozinha uma oferta: somente a
projeção completa revalida rota, tarifa, âncoras, orçamento e partilhas.

Qualquer comando de rede invalida provas antigas, mesmo se desfizer uma
mudança. Mudança física invalida a prova pelo hash; outra sessão, inclusive
um restore idêntico, deve emitir nova prova. Vigência inclusiva é avaliada na
revisão corrente, sem presumir renovação por progressão. A revisão pertence
ao registro e não é recebida do chamador. As APIs VS55/VS56 continuam disponíveis
para regressão experimental; não são o ponto de entrada autoritativo VS57.

## Persistência e compatibilidade

O hash físico existente conserva seu significado. O estado composto de uma
prova inclui também revisão/digest de transporte; o hash físico sozinho não
atesta direitos. O digest não é uma assinatura e não autentica arquivos de
origem não confiável. A identidade de sessão impede empréstimo de provas.

Cada comando guarda posição no diário físico, hash físico e revisão anterior.
O restore intercala os comandos com o replay físico, usando as mesmas regras
da execução viva, e compara o payload reproduzido. Saves com comandos usam
envelope V18, preservando no payload a versão da mecânica física original.
Leitores anteriores recusam V18; não descartam silenciosamente direitos.
Saves sem rede mantêm bytes/formato anteriores. Não há migração implícita de
ativos declarados na VS56. Um save local inteiramente reescrito em uma sequência
legal continua sendo um cenário local diferente: a fronteira não é assinatura
criptográfica contra um host ou autor de save malicioso.

## Invariantes e limites

- Poder de compra abstrato, reserva H e fluxos VS52–VS56 permanecem nas auditorias existentes.
- `CanAuthorizeRealSettlement=false` em provas e resultados. Nenhum pagamento,
  crédito, imposto, carteira de trabalhador ou SETTLE real é introduzido.
- A auditoria não altera save, caixa V14, população, turno ou registro.
- Comandos de rede são explícitos; não há micromanagement adicional na UI.
- Execução segue a sessão canônica serial do host, sem promessa de concorrência multithread.
- Permanece um registro experimental de cruzamentos abstratos construídos;
  não simula engenharia de obras, geometria de vias ou seleção econômica simultânea.

## Verificação

Testes VS57: sessão vazia, prova não fabricável pela API pública, sessão alheia,
mudança física, fechamento/desativação, revogação, troca de dono, partilha,
vigência futura/vencida e limites inclusivos, comandos rejeitados sem mutação,
entrada lazy que muda a rede, cópia defensiva, topologia estrangeira/água,
save/restore com comandos intercalados, downgrade e adulteração com checksum
recalculado. Regressões anteriores preservam H, calibração e conservação.

Benchmark `--vs57-session-authority`: 8/32/128 repetições da auditoria completa
de **uma estrada, uma rota, um comprador e um lote**, com worldgen e comandos
fora da medição, warmup e três amostras; tempo e alocação sem limite arbitrário.
Não mede 128 cidades nem liquidação. Gate CI: Linux VS40–VS57 + benchmark +
Godot Release; Windows suíte integral + benchmark + Godot Debug.

Status de homologação: pendente de todos os workflows do commit final.
