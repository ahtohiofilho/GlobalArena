# Gate 09 — Decisao de arquitetura R19

**Aprovado anteriormente:** R18 corrigido pela REV4, executado no Windows, 23/23 cenarios e 270/270 bits, SDK 10.0.402. Evidencia original `GA-Gate09-R18-REV4-Evidencias-20261112-160717.zip`.

**Entrega R19:** versao rastreavel de `Program.cs` dos dois executaveis e helper `ExactRatioBinary64.cs` (uma fonte unica) com hashes identicos as fontes compiladas e aprovadas na REV4. Vetores oficiais copiados do R18 sem modificar numeros. Workflow para CI Windows/Linux; benchmark Release somente observacional.

**Nao se admite:** confundir R19 com homologaçao ja executada no Windows/GitHub, aprovar performance antes de executar o benchmark, integrar tesouros/journal, canonizar F-059, declarar convergencia global ou alterar F-072/F-043.

**Checkpoints de aceite:**

1. Manifesto R19 integrou hashes SHA-256 de fontes e vetores.
2. Fonte coincide com os hashes da evidencia Windows REV4.
3. Python rederiva os 270 bits a partir de Fraction e valida 23 cenarios fixos.
4. CI .NET 10 compila em Release Windows/Linux.
5. JSON C# completo para 23 casos segue o contrato de paridade R15; 270 testemunhas em igualdade absoluta.
6. Benchmark registra medianas, variabilidade e hardware; sem gate de promocao automatico.

**Gate 09 R19 permanece Draft ate verificacao CI e benchmark.**
