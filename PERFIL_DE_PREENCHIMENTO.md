# Perfil de preenchimento das folhas de alteração (P1/CIPO)

Regras levantadas a partir de 48 folhas feitas pela P1 em 2026 (32 policiais: cabos, soldados, sargentos, subtenente e oficiais). O sistema segue este perfil.

## Cabeçalho

| Campo | Regra observada |
|---|---|
| FOLHA Nº | Sempre **01**, inclusive nos quadrimestres. |
| Período (cabos e soldados) | Anual: janeiro a dezembro. |
| Período (oficiais, ST e sargentos) | Quadrimestral: 1º (01 Fev a 31 Mai), 2º (01 Jun a 30 Set), 3º (01 Out a 31 Jan do ano seguinte). Linha de título "1° QUADRIMESTRE 2026" e "PERÍODO: 01 Fev a 31 Mai 26". |
| Período recortado | Quem **chegou ou saiu** da unidade no meio do período tem a folha começando ou terminando nessa data (ex.: "05 Ago 26 a 30 Set 26"). |
| CONF BI | Último BI conferido ("CONFERIDO BI 43", "CONF BI 40/26"). |

## 1ª parte

- Por padrão: `INCLUSÃO: SEM ALTERAÇÃO.` / `EXCLUSÃO: SEM ALTERAÇÃO.`
- Na chegada à unidade, a inclusão cita o documento (ex.: "Portaria nº 6056 de 07 de agosto de 2026 (75273493).").
- Na saída, a exclusão cita o documento (ex.: "Portaria nº 5120 de 02 de julho de 2026 (74013569).").
- Remanejamento **interno** entre frações da própria CIPO (pelotão/grupo) **não** entra na 1ª parte. Ele aparece só como PORTARIA na 3ª parte.

## 2ª parte

- **Comportamento**: a última pontuação publicada ("COMPORTAMENTO ÓTIMO", "EXCEPCIONAL", "BOM"). Na maioria das folhas de oficiais o campo fica em branco.
- **TC**: anual = 01 A 00 M 00 D; quadrimestre = 00 A 04 M 00 D; período recortado = proporcional (ex.: 00 A 01 M 00 D, 00 A 02 M 00 D).
- TNC: 00 A 00 M 00 D, salvo afastamento que não conta como tempo de serviço.

## 3ª parte

- Ordem **cronológica** pelos BIs; itens seguidos do mesmo tipo ficam sob o mesmo número.
- Referência: `BI Nº 22 CIPO/P-1 de 25 de Maio de 2026. (id SEI do BI) PUBLICOU:` seguido do teor.
- **Títulos mais usados** (em ordem de frequência): PORTARIA, COMPORTAMENTO, ELOGIO, MOVIMENTO SANITÁRIO (atas de inspeção de saúde), FÉRIAS, REQUERIMENTO, OFÍCIO, DECISÃO, PARTE, INFORMAÇÃO, TRÂNSITO / INSTALAÇÃO, DISPENSA COMO RECOMPENSA, LICENÇA ESPECIAL, SINDICÂNCIA.
- Tabelas (férias, licenças) são copiadas com o cabeçalho e **só a linha do militar**.
- Em listas e portarias com muitos militares, fica só a linha do militar; o restante vira `(...)`.
- A pontuação de comportamento do **ano anterior**, publicada em janeiro, entra na folha anual.
- O nome de quem **só assina** o documento (comandante, chefe da P1) não gera item na folha dessa pessoa.
- Militar citado só por **posto + nome de guerra** ("CB PM FULANO") também recebe o item.

## Comparação sistema × folhas da P1 (BIs 01 a 47)

- O sistema encontrou **96%** dos lançamentos feitos pela P1, e 93% do que ele lança também está nas folhas manuais.
- Parte das diferenças são **enganos das folhas manuais**, com o número do BI trocado (ex.: "BI Nº 15 de 30 de março", quando 30 de março é o BI 14).
- Itens que o sistema lança e a P1 às vezes deixa de fora:
  - pedidos de elogio ("Informo que... solicita-se a concessão de elogio");
  - portarias coletivas de férias/abono (PLANAF);
  - decisões sobre requerimentos.

  Ficam para a revisão na aba "Publicações por policial".
- Para repetir a comparação quando houver folhas novas, use o botão **"Conferir com folhas da P1 (PDF)..."** na aba "Gerar folhas".

## Regras acrescentadas (revisão da folha de um CB)
- Portaria longa: ficam o preâmbulo, o artigo do PM, o fechamento (dois últimos artigos) e as assinaturas; artigos de outros militares e artigos normativos genéricos viram "(...)".
- Elogio coletivo: na lista de nomes fica só a entrada do próprio PM; as demais viram "(...)".
- Seção de atestados/inspeções se chama "MOVIMENTO SANITÁRIO".
- "TERMO DE DEVOLUÇÃO" etc. começam documento novo (não entram na folha de quem não é citado).
- Texto duplicado/embaralhado por sobreposição do PDF é limpo (trecho repetido, "Pol" + "lícia").
