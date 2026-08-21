# ADR-012 - Estados, Recuperação e Acompanhamento da Emissão

> Status: Aprovada
> Data: 2026-08-16
> Última atualização: 2026-08-20
> Dependências: ADR-003, ADR-004, ADR-007, ADR-010 e ADR-011

---

## 1. Contexto

A nota possui apenas os estados de negócio `Open` e `Closed`, conforme solicitado pelo desafio. Entretanto, a emissão atravessa Faturamento, RabbitMQ e Estoque e pode permanecer em andamento por tempo indeterminado quando um componente estiver indisponível. Representar esse andamento no próprio status da nota misturaria estado fiscal simplificado com estado técnico e dificultaria informar ao usuário o que realmente ocorreu.

O processo distribuído será, portanto, representado por `InvoiceIssuanceProcess`, pertencente ao Faturamento. Ele registra a solicitação aceita, sua chave de idempotência, o progresso conhecido e o resultado. A nota continua `Open` durante o processamento e só se torna `Closed` depois da confirmação da baixa atômica pelo Estoque.

Esta decisão também distingue três situações que exigem tratamentos diferentes:

- indisponibilidade temporária de um serviço, da qual o sistema deve se recuperar automaticamente;
- recusa de negócio, que exige correção pelo usuário e não deve ser repetida automaticamente;
- falha técnica persistente ou mensagem inválida, que exige diagnóstico operacional.

---

## 2. Decisão

### 2.1 Estados do processo

```text
InvoiceIssuanceProcessStatus
- Pending
- AwaitingStock
- Completed
- Rejected
- ManualIntervention
```

#### Pending

O Faturamento aceitou `PrintInvoice`, persistiu o processo e a mensagem na Outbox, mas ainda não possui confirmação de publicação no RabbitMQ. A nota permanece `Open`, porém bloqueada para alteração de itens e para outra emissão.

Esse estado é válido mesmo quando o RabbitMQ estiver indisponível. Como a intenção foi gravada no banco do Faturamento, a API retorna `202 Accepted` e o dispatcher da Outbox tenta publicar posteriormente.

#### AwaitingStock

O dispatcher recebeu confirmação do broker para `StockDeductionRequested`, e o Faturamento aguarda o resultado do Estoque. A nota continua `Open` e bloqueada.

Esse estado não significa que o Estoque já começou a executar a baixa. A mensagem pode estar aguardando na fila enquanto o serviço estiver parado.

#### Completed

O Faturamento consumiu `StockDeductionCompleted`, registrou a Inbox, fechou a nota e concluiu o processo na mesma transação local. O documento imprimível fica disponível. É um estado terminal.

#### Rejected

O Estoque recusou atomicamente a baixa por uma regra de negócio, por exemplo produto inexistente no momento do processamento ou saldo insuficiente. O Faturamento mantém a nota `Open`, registra o motivo compreensível, desbloqueia seus itens e permite correção. Uma nova emissão exige nova chave de idempotência. É um estado terminal para aquela tentativa.

#### ManualIntervention

Uma falha técnica não transitória ou repetida esgotou o tratamento automático e existe evidência persistida suficiente para impedir repetição cega. A nota permanece `Open` e bloqueada, pois não se pode presumir que a baixa ocorreu ou não ocorreu. É um estado terminal do processamento automático, não uma autorização para reiniciar a operação.

A saída desse estado exige diagnóstico e uma operação administrativa futura, fora da interface funcional deste desafio. Não haverá botão público de reprocessamento manual.

### 2.2 Transições permitidas

```text
Pending
  -> AwaitingStock
  -> ManualIntervention

AwaitingStock
  -> Completed
  -> Rejected
  -> ManualIntervention
```

`Completed`, `Rejected` e `ManualIntervention` são terminais para o processo existente. Eventos duplicados não repetem efeitos porque os consumidores registram Inbox. Transições atrasadas ou incompatíveis devem ser ignoradas de forma idempotente ou registradas como inconsistência, nunca alterar silenciosamente um resultado terminal.

Não haverá transição baseada apenas em tempo decorrido. Lentidão gera informação de atraso, não rejeição nem falha presumida.

---

## 3. Cenário principal de falha e recuperação

O cenário obrigatório demonstrado será a indisponibilidade temporária do Serviço de Estoque:

1. o usuário solicita a impressão de uma nota válida;
2. o Faturamento persiste o processo e aceita a solicitação;
3. `StockDeductionRequested` chega à fila principal;
4. o Estoque está parado, portanto ninguém consome a mensagem;
5. o frontend informa que o processamento está demorando e continuará automaticamente;
6. o Estoque volta a executar;
7. o consumer recebe a mensagem preservada, realiza a baixa e publica o resultado;
8. o Faturamento conclui ou rejeita a emissão;
9. o frontend apresenta o resultado.

Uma mensagem ainda não entregue ao consumer não é uma tentativa com falha. Ela permanece na fila principal e não consome o limite de retry. Essa propriedade é o mecanismo central de recuperação do cenário pedido pelo desafio.

---

## 4. Falhas de negócio e falhas técnicas

### 4.1 Falhas de negócio

Falhas esperadas e determinísticas recebem `ack` depois que o resultado de rejeição foi persistido na Outbox do Estoque. Não usam retry nem DLQ, pois repetir a mesma entrada sem alteração produziria o mesmo resultado.

Exemplos:

- saldo insuficiente;
- produto solicitado inexistente;
- quantidade inválida em uma mensagem que passou pelas validações anteriores, quando classificável com segurança como recusa de domínio.

O Estoque publica `StockDeductionRejected` com código estável e informações seguras para apresentação. A baixa de todos os itens é revertida.

### 4.2 Falhas técnicas transitórias

Uma falha ocorrida depois que o consumer recebeu a mensagem usa retentativas com atrasos de:

```text
5 segundos -> 30 segundos -> 2 minutos -> DLQ
```

Os atrasos serão implementados com filas de retry, TTL e dead-letter routing nativos do RabbitMQ, sem depender de plugin adicional do broker. Cada nova entrega continua sujeita à Inbox e à idempotência do efeito.

Exemplos de falha técnica transitória:

- banco temporariamente indisponível;
- timeout de infraestrutura;
- falha transitória de conexão;
- erro de publicação do resultado que possa ser recuperado pela Outbox.

### 4.3 Mensagens inválidas ou incompatíveis

Payload ilegível, contrato sem suporte ou estrutura que não permita executar o caso de uso com segurança vai diretamente para a DLQ. Repetir uma mensagem incompatível não a torna válida.

---

## 5. Dead-letter queues e intervenção

Haverá DLQ para os fluxos relevantes de solicitação e resultado, incluindo:

- `StockDeductionRequested`;
- `StockDeductionCompleted`;
- `StockDeductionRejected`.

A mensagem encaminhada à DLQ deve preservar, quando disponíveis:

- payload original;
- exchange e routing key de origem;
- message ID, correlation ID e causation ID;
- quantidade de tentativas;
- instante da falha;
- descrição técnica sanitizada.

Não haverá redrive automático infinito. Uma mensagem em DLQ exige inspeção para evitar duplicar efeitos desconhecidos ou repetir permanentemente um defeito.

Quando o Estoque esgotar o processamento técnico de uma solicitação e conseguir persistir e publicar essa informação com segurança, emitirá `StockDeductionProcessingFailed`. O Faturamento usará esse evento para mover o processo a `ManualIntervention`.

Essa transição não é garantida por uma DLQ isoladamente. Se a falha também impedir o Estoque de persistir ou publicar o evento técnico, ou impedir o Faturamento de consumir e gravar um resultado, o processo poderá permanecer em `AwaitingStock`. A DLQ, os logs correlacionados e a consulta do processo serão as evidências para diagnóstico e reconciliação. Automatizar um reconciliador administrativo não faz parte desta entrega; os SDDs devem documentar e testar o comportamento alcançável sem alegar consistência automática inexistente.

---

## 6. Política da Outbox

O dispatcher da Outbox:

- consulta a cada 1 segundo quando houver trabalho;
- processa lotes de até 50 mensagens;
- exige publisher confirm antes de marcar uma mensagem como publicada;
- aumenta o intervalo em falhas sucessivas até o máximo de 30 segundos;
- continua tentando enquanto a aplicação estiver ativa, sem descartar a intenção por limite temporal.

Essa política é diferente do retry de consumer. A Outbox representa uma intenção já confirmada no banco local; uma indisponibilidade prolongada do broker não deve transformá-la automaticamente em rejeição de negócio.

Concorrência entre dispatchers, reserva de lote e retenção de registros publicados serão detalhadas no SDD de emissão, sem alterar esses princípios.

---

## 7. Acompanhamento pelo frontend

`PrintInvoice` retorna `202 Accepted` com identificação e URL do processo. O Angular acompanha:

```text
GET /api/v1/invoice-issuance-processes/{processId}
```

Política inicial de polling:

- uma consulta por segundo nos primeiros 10 segundos;
- uma consulta a cada 3 segundos depois disso;
- encerramento ao receber um estado terminal;
- cancelamento no logout ou quando o componente for destruído;
- continuidade enquanto a tela responsável permanecer ativa, mesmo após 60 segundos;
- possibilidade de o usuário atualizar ou voltar à tela para retomar a consulta do mesmo processo na mesma aba.

A duração não encerra automaticamente o acompanhamento. O intervalo estabilizado em três segundos limita a frequência; `isDelayed` comunica a demora sem decidir sucesso, rejeição ou falha.

Se `Pending` ou `AwaitingStock` durar mais de 5 segundos, a resposta poderá expor `isDelayed: true`, calculado a partir do estado e dos timestamps. O frontend informa que o Estoque ou a infraestrutura pode estar temporariamente indisponível e que o processamento retomará automaticamente. `isDelayed` não é um novo estado persistido nem prova de falha.

O frontend não fecha a nota localmente e não deduz sucesso a partir do tempo. Ele sempre apresenta o estado informado pelo Faturamento.

---

## 8. Retentativas HTTP

Consultas `GET` podem ser repetidas automaticamente porque não alteram estado.

`PrintInvoice` não terá repetição automática cega no cliente. Se a resposta for perdida ou houver dúvida sobre a aceitação, a interface preserva a mesma `Idempotency-Key` e permite repetir conscientemente a solicitação. O Faturamento retorna o processo já criado para essa chave, sem iniciar nova baixa.

A consulta interna do Faturamento ao Estoque durante `AddInvoiceItem` terá timeout de 3 segundos e, no máximo, uma repetição para falha transitória. Como é uma consulta idempotente e está fora do processo de emissão, o resultado final de indisponibilidade é `503` e nenhum item é adicionado.

---

## 9. Consequências

### Positivas

- mantém os estados da nota aderentes ao enunciado;
- permite feedback preciso sem acoplar o frontend ao RabbitMQ;
- demonstra recuperação real quando o Estoque volta;
- diferencia recusa corrigível de falha operacional;
- evita retries inúteis de regras de negócio;
- explicita idempotência e limites da consistência distribuída.

### Custos e limitações

- exige persistência e consulta de um processo separado da nota;
- introduz filas de retry, DLQs e contratos adicionais;
- polling gera tráfego periódico, embora limitado e simples para o porte do desafio;
- intervenção e reconciliação completas não terão interface administrativa nesta entrega;
- uma falha que impeça registrar seu próprio resultado pode deixar o processo aguardando até análise operacional.

---

## 10. Alternativas não adotadas

### Alterar o status da nota para Processing ou Failed

Rejeitada porque o desafio define os estados da nota como aberta e fechada. O andamento técnico pertence a outro conceito.

### Declarar falha por timeout

Rejeitada porque tempo decorrido não informa se uma mensagem será processada nem se um efeito já ocorreu. Isso poderia desbloquear uma segunda emissão de forma insegura.

### Retry imediato no mesmo consumer

Rejeitada porque ocupa o consumer, pressiona uma dependência indisponível e oferece pouca visibilidade. Filas de retry tornam o atraso explícito.

### Redrive automático e ilimitado da DLQ

Rejeitada porque pode criar ciclos infinitos e esconder mensagens incompatíveis ou defeitos permanentes.

### WebSocket ou Server-Sent Events

Não adotados inicialmente. Polling limitado atende ao desafio com menos infraestrutura. A evolução permanece possível sem alterar o modelo do processo.

---

## 11. Impacto nos SDDs

- `SDD-06-BILLING-SERVICE.md` definirá a persistência e consulta de `InvoiceIssuanceProcess`;
- `SDD-07-EMISSAO-E-CONSISTENCIA-DISTRIBUIDA.md` detalhará contratos, filas, transações, idempotência, retry, DLQ e testes de recuperação;
- `SDD-09-FRONTEND-ANGULAR.md` definirá polling, mensagens, cancelamento e estados visuais;
- `SDD-10-TESTES.md` cobrirá indisponibilidade, redelivery, concorrência, DLQ e duplicidade;
- `SDD-11-DOCKER-COMPOSE-E-OBSERVABILIDADE.md` tornará o cenário de parada e retomada reproduzível.
