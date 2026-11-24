# Folhas de Alteração – CIPO

Programa em C# (.NET 8, WinForms) que lê os Boletins Internos (BI) em PDF, identifica o que foi publicado sobre cada policial e gera as folhas de alteração em Word (.docx) no modelo da P1.

## Como compilar

1. Instale o **.NET 8 SDK** (https://dotnet.microsoft.com/download) ou o **Visual Studio 2022** com a carga "Desenvolvimento para desktop com .NET".
2. Abra a pasta no terminal e rode:

   ```
   cd FolhasCipo
   dotnet restore
   dotnet build -c Release
   dotnet run
   ```

   Pelo Visual Studio, basta abrir o `FolhasCipo.sln` e apertar F5.
3. Para gerar um executável único, que funciona sem o .NET instalado:

   ```
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

   O executável fica em `bin\Release\net8.0-windows\win-x64\publish\`.

Pacotes NuGet usados (baixados no `restore`): PdfPig (leitura do PDF), DocumentFormat.OpenXml (geração do Word), ClosedXML (importação da planilha do efetivo) e Microsoft.Data.Sqlite (banco de dados).

O banco fica em `dados\folhas.db`, ao lado do executável. **Faça backup desse arquivo**: ele guarda o cadastro, os BIs importados e as correções feitas à mão.

## Primeiro uso

1. **Aba Policiais → "Importar planilha do efetivo (.xlsx)"**: use o "Relatório do efetivo" exportado do sistema. A importação atualiza os policiais pela matrícula.
   - Confira o **nome de guerra** de cada um. Ele é usado para achar citações do tipo "CB PM FULANO".
   - Ao lado do campo "Ativo" há o tipo de folha: **AUTO** (oficiais e sargentos quadrimestral, cabos e soldados anual), ANUAL ou QUADRIMESTRAL.
   - Quem saiu da unidade: desmarque "Ativo" em vez de excluir, para não perder o histórico.
2. **Aba Boletins → "Definir pasta de BIs"**: escolha a pasta onde o BI de cada semana é salvo.
3. **Aba Gerar folhas**: confira o nome e a função de quem assina (Chefe da P1) e clique em "Salvar assinatura".

## Rotina semanal (toda sexta-feira)

1. Salve o PDF do BI na pasta de BIs.
2. Abra o programa. Ele avisa que há BI novo na pasta e pergunta se deve importar. Também dá para usar "Importar BI (PDF)..." ou "Verificar pasta de BIs".
   - O PDF pode trazer um BI só ou vários (por exemplo, o processo SEI inteiro).
   - Se o BI já tiver sido importado, o programa pergunta se deve reprocessar.
3. **Aba Publicações por policial**: escolha o policial e revise o que foi lançado.
   - **Editar**: corrige o título do item ou o teor.
   - **Excluir**: remove um item que não deve ir para a folha.
   - **Nova publicação manual**: inclui algo que o programa não pegou.
   - Itens editados ou incluídos à mão aparecem em azul. Eles **não são apagados** quando o BI é reprocessado.

## Policial transferido (chegada ou saída no meio do período)

Na edição do policial, marque **Chegada na unidade** e/ou **Saída da unidade** e informe o documento (ex.: "Portaria nº 6056 de 07 de agosto de 2026 (75273493)."). Na geração:

- a folha do período começa ou termina nessa data (ex.: "PERÍODO: 05 Ago 26 a 30 Set 26");
- o TC fica proporcional;
- a 1ª parte cita o documento.

Se ele não esteve na unidade em nenhum dia do período, a folha não é gerada.

## Gerar as folhas

Na aba **Gerar folhas**, escolha o ano, o quadrimestre e o tipo, marque os policiais e clique em **Gerar folhas**.

- **Automático**: cabos e soldados recebem a folha anual (janeiro a dezembro). Oficiais e sargentos recebem a folha do quadrimestre escolhido.
- **Quadrimestres**:
  - 1º: 01/fev a 31/mai
  - 2º: 01/jun a 30/set
  - 3º: 01/out a 31/jan do ano seguinte
- A folha traz apenas os BIs com data dentro do período.
- **CONF BI** é o último BI importado dentro do período.
- **Comportamento** é a última publicação de comportamento até o fim do período. Se não houver nenhuma, vale o comportamento do cadastro do policial. Oficiais não têm comportamento.
- FOLHA Nº é sempre 01, como nas folhas da P1.
- As folhas são salvas em subpastas (`Anual 2026` e `Oficiais e Sargentos - quadrimestral\1 Quadrimestre 2026`), com um `.docx` por policial, prontas para copiar e colar no SEI.

## Conferir com as folhas da P1

O botão **"Conferir com folhas da P1 (PDF)..."**, na aba Gerar folhas, lê folhas feitas à mão (PDF exportado do SEI) e compara BI por BI com o que o sistema lança para o mesmo policial e período. O relatório mostra o que falta e o que sobra. As regras levantadas das folhas da P1 estão em `PERFIL_DE_PREENCHIMENTO.md`.

## Como a extração funciona (resumo)

- A 1ª parte do BI (escala de serviço) é ignorada.
- O lixo de impressão do SEI é descartado (blocos "Documento assinado eletronicamente", código CRC etc.).
- O policial é identificado pelo nome completo, pela matrícula (inteira ou mascarada, como `******474`, junto com o nome), pelo nome com pequeno erro de digitação ou por posto + nome de guerra.
- **Quem só assina o ato não recebe o item.** Só conta quem é citado no teor.
- Homônimos (dois policiais com o mesmo nome completo) são separados pelo posto ou pela matrícula próximos da citação.
- Em listas e tabelas com vários militares, fica só a linha do policial; o resto vira `(...)`. Artigos de portaria que só citam outros militares são omitidos.
- O título do item (FÉRIAS INÍCIO / RETORNO, ELOGIO, PORTARIA...) vem da seção do BI e do início do documento. Ele pode ser trocado na edição.

## Estrutura do código

| Pasta/arquivo | O que faz |
|---|---|
| `Extracao/LeitorPdf.cs` | Lê o PDF (letras + bordas) e reconstrói linhas de texto e tabelas |
| `Extracao/Segmentador.cs` | Separa BIs, seções e atos e detecta assinaturas |
| `Extracao/Identificador.cs` | Descobre quais policiais são citados num texto |
| `Extracao/Extrator.cs` | Recorta o trecho de cada policial e dá o título do item |
| `Servicos/ImportadorBi.cs` | Importa o PDF e grava no banco |
| `Servicos/GeradorFolha.cs` | Monta o .docx no modelo da folha |
| `Servicos/Conferencia.cs` | Compara as folhas da P1 com o sistema |
| `Servicos/ServicoFolhas.cs` | Escolhe período e publicações, gera os arquivos e importa a planilha |
| `Dados/Database.cs` | SQLite: policiais, boletins, publicações e configuração |
| `Telas/*` | Telas (feitas em código, sem designer) |
