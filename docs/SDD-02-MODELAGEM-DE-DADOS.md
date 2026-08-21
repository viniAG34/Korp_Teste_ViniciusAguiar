# SDD-02 - Modelagem de Dados

> Status: Aprovado
> Versão: 1.0
> Data: 2026-08-17
> Gate A aprovado em: 2026-08-18
> Dependências: SDD-01, ADR-001, ADR-008, ADR-011, ADR-012 e ADR-013

---

## 1. Objetivo

Definir o modelo persistente pertencente a cada serviço, incluindo entidades, agregados, invariantes, relacionamentos, constraints, índices, concorrência, auditoria técnica e estruturas de confiabilidade necessárias aos fluxos já aprovados.

Este documento deverá permitir implementar o domínio e a persistência sem decisões implícitas sobre propriedade de dados ou integridade.

---

## 2. Requisitos rastreados

Este SDD detalhará principalmente:

- `OBR-003` a `OBR-009`;
- `OBR-012`, `OBR-014`, `OBR-017`, `OBR-018` e `OBR-022`;
- `OPA-001` e `OPA-002`;
- `DIF-002` a `DIF-006`;
- `QLT-001` a `QLT-003`, `QLT-006` e `QLT-007`.

A rastreabilidade será refinada quando os critérios de aceite forem fechados.

---

## 3. Escopo previsto

### Identity

- usuário administrativo baseado em ASP.NET Core Identity;
- credenciais e dados mínimos necessários para autenticação e autorização;
- banco `identity_db` e migrations próprias.

### Inventory

- `Product`;
- `StockMovement`;
- Inbox e Outbox do serviço;
- banco `inventory_db` e migrations próprias.

### Billing

- `Invoice`;
- `InvoiceItem`;
- `InvoiceIssuanceProcess`;
- Inbox e Outbox do serviço;
- banco `billing_db` e migrations próprias.

### Aspectos transversais

- UUIDs, UTC e nomes físicos em `snake_case`;
- Fluent API e constraints de segunda linha de defesa;
- token de concorrência PostgreSQL `xmin` onde houver estado concorrente;
- índices derivados de consultas e garantias reais;
- limites das transações locais;
- dados auditáveis e política inicial de retenção;
- estratégia e responsabilidade das migrations.

---

## 4. Fora do escopo

- endpoints e contratos HTTP;
- payloads e versionamento de eventos;
- implementação de consumers, publishers e dispatchers;
- configuração de filas RabbitMQ;
- autenticação JWT e fluxo de login;
- casos de uso completos;
- telas Angular;
- topologia final do Docker Compose.

O modelo poderá reservar somente estruturas cuja necessidade já esteja aprovada. Não serão criadas tabelas ou propriedades para evoluções hipotéticas.

---

## 5. Blocos de decisão

O SDD será construído e aprovado em blocos:

1. bancos, propriedade e agregados;
2. modelo do Identity;
3. modelo do Inventory;
4. modelo do Billing;
5. Inbox, Outbox e idempotência;
6. constraints, índices e concorrência;
7. auditoria, retenção e migrations;
8. critérios de aceite, testes e marcadores de qualidade.

Cada bloco deve eliminar ambiguidades antes do seguinte. Nenhuma implementação está autorizada enquanto o documento não atingir o Gate A e seu plano não atingir o Gate B.

---

## 6. Registro inicial de decisões herdadas

As seguintes decisões já estão aprovadas e não serão reabertas sem novo ADR:

- PostgreSQL e EF Core 10 com provider Npgsql 10;
- um banco lógico e uma credencial por serviço persistente;
- ausência de banco no Gateway;
- Code First e configurações via Fluent API;
- migrations separadas e executadas por processo controlado;
- proibição de `EnsureCreated` e lazy loading;
- `DbContext` como Unit of Work;
- inexistência de transação distribuída;
- UUIDs para entidades e eventos;
- sequence PostgreSQL para número da nota, com lacunas permitidas;
- UTC para datas persistidas;
- `snake_case` para nomes físicos;
- `xmin` como mecanismo de concorrência otimista;
- domínio como primeira defesa e banco como segunda defesa;
- isolamento absoluto entre bancos dos serviços.

---

## 7. Decisões em elaboração

### 7.1 Bloco 1 - Bancos, propriedade e agregados

> Estado: Aprovado pelo engenheiro em 2026-08-17

Cada serviço persistente é proprietário exclusivo de seu banco, modelo e migrations:

```text
Identity.Service  -> identity_db
Inventory.Service -> inventory_db
Billing.Service   -> billing_db
Gateway           -> sem persistência
```

O schema padrão `public` será utilizado dentro de cada banco. Schemas adicionais não aumentariam o isolamento já garantido por banco lógico e credencial exclusivos e acrescentariam configuração sem necessidade demonstrada.

As fronteiras de agregados são:

- Identity utiliza o modelo do ASP.NET Core Identity e não duplica usuários em uma entidade de domínio artificial;
- `Product` é Aggregate Root do Inventory;
- `StockMovement` é registro histórico auditável criado na transação da baixa, mas não integra uma coleção carregada pelo agregado `Product`;
- `Invoice` é Aggregate Root do Billing e controla sua coleção de `InvoiceItem`;
- `InvoiceIssuanceProcess` é Aggregate Root separado, com identidade, ciclo de vida, consulta e idempotência próprios;
- Inbox e Outbox são modelos técnicos de persistência, não entidades de domínio.

Não existem foreign keys, navegações do EF Core ou transações atravessando bancos. Identificadores externos como `ProductId` no Billing e `InvoiceId` no Inventory preservam correlação, mas não representam relacionamento relacional sob controle do serviço local.

Consequências:

- consultar um produto não carrega seu histórico completo de movimentações;
- uma invoice protege a consistência dos seus itens dentro do próprio agregado;
- o processo distribuído não amplia indevidamente o estado interno da invoice;
- serviços podem evoluir e migrar seus bancos sem dependência de schema compartilhado;
- integridade entre serviços é garantida por contratos e processamento idempotente, não por constraints entre bancos.

### 7.2 Bloco 2 - Modelo do Identity

> Estado: Aprovado pelo engenheiro em 2026-08-17

O Identity utilizará `ApplicationUser : IdentityUser<Guid>` e `IdentityRole<Guid>`. Não haverá uma segunda entidade `User` no Domain, pois isso duplicaria estado e regras já protegidos pelo ASP.NET Core Identity sem existir comportamento próprio que justificasse outro agregado.

#### Identidade e login

- o identificador persistente do usuário é um UUID;
- login utiliza exclusivamente e-mail e senha;
- `UserName` recebe o mesmo e-mail informado; `NormalizedUserName` e `NormalizedEmail` são produzidos pelo normalizador do Identity;
- e-mail é obrigatório;
- `NormalizedEmail` possui índice único;
- o único papel funcional inicial é `Admin`;
- o seed administrativo associa o usuário ao papel `Admin`;
- `EmailConfirmed` será verdadeiro para o usuário administrativo criado pelo ambiente, pois confirmação de e-mail e cadastro público estão fora do escopo.

Não serão adicionados nome, telefone, endereço, empresa, preferências, status de perfil ou outros dados sem uso no desafio. Campos internos mantidos pelo framework continuam presentes quando necessários à segurança e ao funcionamento do Identity.

#### Estrutura relacional

O banco `identity_db` manterá a estrutura relacional completa usada pelo ASP.NET Core Identity:

```text
users
roles
user_roles
user_claims
user_logins
user_tokens
role_claims
```

As tabelas auxiliares são mantidas mesmo quando uma capacidade não é exposta nesta entrega. Remover seletivamente estruturas internas do framework aumentaria o custo de configuração, testes e evolução sem reduzir de forma relevante o risco ou o tamanho do desafio.

Tabelas e colunas físicas seguem `snake_case`. Chaves estrangeiras e índices exigidos pelo Identity serão preservados, incluindo unicidade do nome normalizado do papel e do e-mail normalizado do usuário.

#### Seed administrativo

O papel e o usuário iniciais serão criados por inicializador idempotente executado em preparação controlada do ambiente, depois da aplicação das migrations.

- e-mail e senha vêm de configuração segura;
- senha passa exclusivamente pelo password hasher do Identity;
- senha, hash e chave JWT não aparecem em migration, `HasData`, repositório ou log;
- ausência de configuração obrigatória causa falha sanitizada;
- usuário existente não tem senha redefinida a cada inicialização;
- papel existente não é duplicado;
- a associação usuário-papel também é idempotente.

`HasData` não será usado para o administrador porque produzir e versionar hash a partir de segredo de ambiente é incompatível com migrations determinísticas e seguras.

#### Concorrência e tokens

O modelo utiliza o `ConcurrencyStamp` nativo do Identity. Não será adicionado `xmin` ao usuário, pois não existem operações públicas concorrentes de gestão de conta e os dois mecanismos protegeriam o mesmo estado sem benefício proporcional.

JWTs não são armazenados. Também não existirão tabelas próprias para refresh token, sessão, revogação, histórico de login ou auditoria de autenticação nesta entrega. `user_tokens` permanece como tabela interna do Identity, mas nenhum fluxo de refresh token será implementado sobre ela.

#### Responsabilidade arquitetural

- modelos e configurações do ASP.NET Core Identity pertencem à Infrastructure;
- Application coordena autenticação e emissão, sem conhecer detalhes de tabelas;
- Domain permanece vazio enquanto não houver regra de identidade própria;
- nenhum outro serviço referencia classes, tabelas ou migrations do Identity;
- migrations do `identity_db` pertencem exclusivamente ao Identity.Service.

### 7.3 Bloco 3 - Modelo do Inventory

> Estado: Aprovado pelo engenheiro em 2026-08-17

#### Product

`Product` é o Aggregate Root responsável por proteger seu código, sua descrição e seu saldo atual.

```text
Product
- Id: Guid
- Code: string
- Description: string
- Balance: int
- CreatedByUserId: Guid
- Version: uint
- CreatedAtUtc: DateTimeOffset
- UpdatedAtUtc: DateTimeOffset
```

A tabela `products` utiliza:

| Coluna | Tipo PostgreSQL | Regra |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `code` | `varchar(50)` | Obrigatório, normalizado e único |
| `description` | `varchar(200)` | Obrigatória e sem espaços externos |
| `balance` | `integer` | Obrigatório e não negativo |
| `created_by_user_id` | `uuid` | Autor externo obrigatório, sem FK para Identity |
| `created_at_utc` | `timestamptz` | Obrigatório |
| `updated_at_utc` | `timestamptz` | Obrigatório |
| `xmin` | coluna de sistema | Token de concorrência mapeado em `Version` |

O domínio aplica trim e uppercase invariant ao código. O banco atua como segunda defesa com:

- índice único em `code`;
- código não vazio;
- código persistido sem espaços externos e em uppercase;
- código limitado a caracteres alfanuméricos, hífen, underscore e ponto;
- descrição não vazia e sem espaços externos;
- `balance >= 0`.

Não existem soft delete, `is_active` ou edição pública de código. Também não será criada uma tabela genérica de histórico de produto sem requisito de consulta ou auditoria correspondente.

#### StockMovement

`StockMovement` é um registro histórico imutável criado para cada produto baixado com sucesso.

```text
StockMovement
- Id: Guid
- ProductId: Guid
- InvoiceId: Guid
- Quantity: int
- BalanceBefore: int
- BalanceAfter: int
- Type: StockMovementType
- EventId: Guid
- CreatedAtUtc: DateTimeOffset
```

A tabela `stock_movements` utiliza:

| Coluna | Tipo PostgreSQL | Regra |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `product_id` | `uuid` | FK local obrigatória para `products` |
| `invoice_id` | `uuid` | Referência externa obrigatória, sem FK para Billing |
| `quantity` | `integer` | Positiva |
| `balance_before` | `integer` | Não negativo |
| `balance_after` | `integer` | Não negativo e consistente com a baixa |
| `type` | texto limitado | Único valor atual: `invoice_deduction` |
| `event_id` | `uuid` | Identificador da solicitação consumida |
| `created_at_utc` | `timestamptz` | Obrigatório |

Constraints garantem `quantity > 0` e `balance_after = balance_before - quantity`. O registro não é atualizado ou excluído por fluxo funcional.

Garantias de unicidade:

- `(event_id, product_id)` impede que a repetição da mesma mensagem crie outra movimentação para o mesmo produto;
- `(invoice_id, product_id)` impede nova baixa lógica do mesmo produto para a mesma nota, mesmo que uma mensagem incorreta chegue com outro identificador técnico.

A primeira garantia complementa a Inbox. A segunda protege contra duplicidade lógica que a Inbox, isoladamente, não reconheceria.

Índices:

- índice único em `(event_id, product_id)`;
- índice único em `(invoice_id, product_id)`;
- índice em `(product_id, created_at_utc desc)` para histórico auditável do produto.

#### Saldo inicial e dados demonstrativos

`InitialBalance` continua sendo uma entrada obrigatória do cadastro e deve ser um inteiro maior ou igual a zero. O valor é persistido diretamente em `Product.Balance`.

Não será criada uma movimentação fictícia de entrada, fornecedor, compra ou documento de origem. A procedência comercial do saldo inicial é reconhecida como parte de um domínio maior de suprimentos, mas está fora desta feature.

A escolha entre banco funcional inicialmente vazio e criação de produtos demonstrativos será tomada posteriormente no planejamento de seed e apresentação do ambiente. Caso existam exemplos, eles serão dados de demonstração controlados e não alterarão o modelo nem representarão uma compra real.

### 7.4 Bloco 4 - Modelo do Billing

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Bloqueio operacional da invoice

`InvoiceStatus` permanece restrito a `Open` e `Closed`. Para proteger a concorrência entre edição de itens e emissão, a invoice possui separadamente `IsIssuanceInProgress`.

- nasce como `false`;
- muda para `true` na mesma transação que cria `InvoiceIssuanceProcess` e a mensagem Outbox;
- inclusão, alteração e remoção de itens exigem valor `false`;
- rejeição de negócio retorna o valor para `false`;
- conclusão fecha a invoice e retorna o valor para `false`;
- `ManualIntervention` mantém o valor `true`, pois o resultado do efeito externo é incerto;
- qualquer transição altera a invoice e, portanto, participa da concorrência otimista por `xmin`.

Essa propriedade não é um terceiro status fiscal. Ela representa uma trava de edição persistente e impede que `PrintInvoice` e uma alteração de item sejam confirmados simultaneamente com versões diferentes do agregado.

#### Invoice

```text
Invoice
- Id: Guid
- Number: long
- Status: InvoiceStatus
- IsIssuanceInProgress: bool
- CreatedByUserId: Guid
- CreatedAtUtc: DateTimeOffset
- UpdatedAtUtc: DateTimeOffset
- ClosedAtUtc: DateTimeOffset?
- Version: uint
- Items: collection
```

A tabela `invoices` utiliza:

| Coluna | Tipo PostgreSQL | Regra |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `number` | `bigint` | Positivo, único e gerado pelo backend |
| `status` | texto limitado | `open` ou `closed` |
| `is_issuance_in_progress` | `boolean` | Obrigatório |
| `created_by_user_id` | `uuid` | Autor externo obrigatório, sem FK para Identity |
| `created_at_utc` | `timestamptz` | Obrigatório |
| `updated_at_utc` | `timestamptz` | Obrigatório |
| `closed_at_utc` | `timestamptz` | Nulo enquanto aberta |
| `xmin` | coluna de sistema | Token de concorrência mapeado em `Version` |

O número utiliza a sequence `invoice_number_seq`, do tipo `bigint`, iniciada em 1, incremento 1 e sem ciclo. A sequence garante unicidade e ordenação, mas lacunas são aceitas quando uma transação falha depois de reservar um valor.

Constraints garantem:

- `number > 0`;
- status pertencente ao conjunto permitido;
- invoice `open` com `closed_at_utc` nulo;
- invoice `closed` com `closed_at_utc` preenchido;
- invoice fechada com `is_issuance_in_progress = false`;
- `updated_at_utc >= created_at_utc`;
- `closed_at_utc`, quando presente, não anterior à criação.

Existe índice único em `number` e índice para listagem em `(created_at_utc desc, id)`. O `id` como segundo componente fornece ordenação determinística para paginação.

Imutabilidade após fechamento continua sendo uma regra do agregado e dos casos de uso. Uma constraint não consegue impedir genericamente toda futura atualização de uma linha fechada sem introduzir trigger, que não será adotado para duplicar o comportamento do domínio.

#### InvoiceItem

```text
InvoiceItem
- Id: Guid
- InvoiceId: Guid
- ProductId: Guid
- ProductCode: string
- ProductDescription: string
- Quantity: int
```

A tabela `invoice_items` utiliza:

| Coluna | Tipo PostgreSQL | Regra |
|---|---|---|
| `id` | `uuid` | Chave primária |
| `invoice_id` | `uuid` | FK obrigatória para `invoices` |
| `product_id` | `uuid` | Referência externa ao Inventory, sem FK entre bancos |
| `product_code` | `varchar(50)` | Snapshot obrigatório e normalizado |
| `product_description` | `varchar(200)` | Snapshot obrigatório |
| `quantity` | `integer` | Positiva |

Existe índice único em `(invoice_id, product_id)`. Um produto não aparece duas vezes na mesma nota; tentativa duplicada deve falhar como conflito, sem soma silenciosa.

As constraints de código, descrição e quantidade repetem as garantias estruturais relevantes do snapshot. O Billing não confia em um texto externo para persistir valores vazios ou fora dos limites, mesmo que o Inventory já os tenha validado.

`InvoiceItem` pertence ao agregado `Invoice` e não possui repositório próprio. Remover um item de uma invoice aberta exclui sua linha. Não será criado histórico de versões de itens porque o desafio exige apenas o documento em seu estado atual e a invoice fechada se torna imutável.

A FK local utiliza exclusão em cascata como proteção estrutural do agregado, embora a invoice não possua operação pública de exclusão nesta feature.

#### InvoiceIssuanceProcess

`InvoiceIssuanceProcess` é um Aggregate Root separado e preserva cada tentativa de emissão, inclusive rejeições anteriores.

```text
InvoiceIssuanceProcess
- Id: Guid
- InvoiceId: Guid
- IdempotencyKey: Guid
- RequestedByUserId: Guid
- Status: InvoiceIssuanceProcessStatus
- OutcomeCode: string?
- OutcomeDescription: string?
- CreatedAtUtc: DateTimeOffset
- UpdatedAtUtc: DateTimeOffset
- FinishedAtUtc: DateTimeOffset?
- Version: uint
```

A chave HTTP de idempotência será um UUID válido. A representação restrita simplifica validação, armazenamento e geração no Angular, sem reduzir a capacidade de reconhecer a mesma intenção.

A tabela `invoice_issuance_processes` utiliza:

| Coluna | Tipo PostgreSQL | Regra |
|---|---|---|
| `id` | `uuid` | Chave primária e identificador consultável do processo |
| `invoice_id` | `uuid` | FK obrigatória para `invoices` com exclusão restrita |
| `idempotency_key` | `uuid` | Única globalmente |
| `requested_by_user_id` | `uuid` | Solicitante externo obrigatório, sem FK para Identity |
| `status` | texto limitado | Estado técnico aprovado |
| `outcome_code` | `varchar(100)` | Código estável para rejeição ou intervenção |
| `outcome_description` | `varchar(500)` | Explicação sanitizada e segura para apresentação |
| `created_at_utc` | `timestamptz` | Obrigatório |
| `updated_at_utc` | `timestamptz` | Obrigatório |
| `finished_at_utc` | `timestamptz` | Instante de estado terminal |
| `xmin` | coluna de sistema | Token de concorrência mapeado em `Version` |

Estados persistidos:

```text
pending
awaiting_stock
completed
rejected
manual_intervention
```

Constraints garantem:

- estado pertencente ao conjunto permitido;
- `Pending` e `AwaitingStock` sem resultado e sem `finished_at_utc`;
- `Completed` com `finished_at_utc` e sem código ou descrição de falha;
- `Rejected` e `ManualIntervention` com `finished_at_utc` e `outcome_code` obrigatório;
- `updated_at_utc >= created_at_utc`;
- `finished_at_utc`, quando presente, não anterior à criação.

Índices e garantias:

- índice único em `idempotency_key`;
- índice parcial único em `invoice_id` quando status estiver em `pending` ou `awaiting_stock`;
- índice em `(invoice_id, created_at_utc desc)` para consultar o histórico de tentativas;
- índice em `(status, updated_at_utc)` para acompanhamento técnico de processos não terminais.

O índice parcial impede duas emissões automáticas ativas para a mesma invoice. `ManualIntervention` não entra nesse índice porque é terminal para o processamento automático; a trava `IsIssuanceInProgress` da invoice continua verdadeira e impede uma nova tentativa funcional.

#### Transações locais relevantes

Iniciar emissão confirma atomicamente:

```text
marcar Invoice.IsIssuanceInProgress
    -> criar InvoiceIssuanceProcess(Pending)
        -> criar Outbox StockDeductionRequested
            -> commit
```

Consumir conclusão confirma atomicamente:

```text
registrar Inbox
    -> fechar Invoice
        -> remover bloqueio operacional
            -> concluir InvoiceIssuanceProcess
                -> commit
```

Consumir rejeição confirma atomicamente:

```text
registrar Inbox
    -> manter Invoice Open
        -> remover bloqueio operacional
            -> rejeitar InvoiceIssuanceProcess
                -> commit
```

Uma falha em qualquer etapa reverte toda a transação local. Não existe atualização direta do banco do Billing pelo RabbitMQ ou pelo Inventory.

### 7.5 Bloco 5 - Inbox, Outbox e idempotência técnica

> Estado: Aprovado pelo engenheiro em 2026-08-18

Inventory e Billing possuem tabelas próprias de Inbox e Outbox. Identity não participa da mensageria e não recebe essas estruturas.

As classes, configurações do EF Core e tabelas são locais a cada serviço. `Korp.Shared.Contracts` compartilha somente contratos de eventos; não contém modelos de persistência, repositórios ou infraestrutura comum de banco.

#### OutboxMessage

```text
OutboxMessage
- Id: Guid
- MessageType: string
- SchemaVersion: int
- Payload: jsonb
- CorrelationId: Guid
- CausationId: Guid?
- OccurredAtUtc: DateTimeOffset
- PublishedAtUtc: DateTimeOffset?
- AttemptCount: int
- NextAttemptAtUtc: DateTimeOffset
- LockId: Guid?
- LockedUntilUtc: DateTimeOffset?
- LastError: string?
```

A tabela `outbox_messages` utiliza:

| Coluna | Tipo PostgreSQL | Regra |
|---|---|---|
| `id` | `uuid` | PK e `MessageId` publicado |
| `message_type` | `varchar(200)` | Tipo canônico obrigatório |
| `schema_version` | `integer` | Positiva |
| `payload` | `jsonb` | Contrato serializado obrigatório |
| `correlation_id` | `uuid` | Correlação do fluxo distribuído |
| `causation_id` | `uuid` | Mensagem causadora, quando aplicável |
| `occurred_at_utc` | `timestamptz` | Instante de criação do evento |
| `published_at_utc` | `timestamptz` | Preenchido somente após publisher confirm |
| `attempt_count` | `integer` | Inicia em zero e nunca é negativo |
| `next_attempt_at_utc` | `timestamptz` | Próxima tentativa elegível |
| `lock_id` | `uuid` | Identificador do lease do dispatcher |
| `locked_until_utc` | `timestamptz` | Expiração recuperável do lease |
| `last_error` | `varchar(1000)` | Diagnóstico técnico sanitizado |

`Id` é reutilizado como `MessageId` no RabbitMQ. Tipo e versão permanecem fora do payload para permitir roteamento, diagnóstico e evolução de contrato sem desserialização prévia.

O dispatcher reserva mensagens por lease. `LockId` e `LockedUntilUtc` devem ser ambos nulos ou ambos preenchidos. Lease expirado pode ser adquirido por outra instância. Publisher confirm é obrigatório antes de definir `PublishedAtUtc`.

Falha de publicação:

- incrementa `AttemptCount`;
- registra erro sanitizado;
- calcula `NextAttemptAtUtc` conforme a política aprovada;
- libera o lease;
- não descarta a intenção por limite de tentativas.

Constraints garantem versão positiva, tentativas não negativas, consistência do par de lease e timestamps coerentes. Mensagem publicada não permanece reservada.

O índice operacional principal é parcial:

```text
(next_attempt_at_utc, occurred_at_utc)
WHERE published_at_utc IS NULL
```

Também existe índice em `published_at_utc` para a futura retenção de mensagens concluídas.

#### InboxMessage

```text
InboxMessage
- MessageId: Guid
- MessageType: string
- SchemaVersion: int
- CorrelationId: Guid
- CausationId: Guid?
- PayloadHash: string
- ProcessedAtUtc: DateTimeOffset
```

A tabela `inbox_messages` utiliza:

| Coluna | Tipo PostgreSQL | Regra |
|---|---|---|
| `message_id` | `uuid` | Chave primária |
| `message_type` | `varchar(200)` | Tipo canônico obrigatório |
| `schema_version` | `integer` | Positiva |
| `correlation_id` | `uuid` | Correlação do fluxo |
| `causation_id` | `uuid` | Mensagem causadora, quando aplicável |
| `payload_hash` | `char(64)` | SHA-256 hexadecimal normalizado |
| `processed_at_utc` | `timestamptz` | Instante da confirmação local |

O registro da Inbox ocorre na mesma transação dos efeitos locais e das mensagens de resposta eventualmente adicionadas à Outbox.

- mesmo `MessageId` e mesmo hash: duplicidade legítima, confirmada sem repetir efeitos;
- mesmo `MessageId` e hash diferente: inconsistência técnica, nunca tratada silenciosamente como sucesso;
- mensagem nova: efeitos e Inbox são confirmados atomicamente.

O payload completo não é duplicado na Inbox. O hash permite verificar identidade de conteúdo sem manter outra cópia potencialmente grande ou sensível.

Existe índice em `processed_at_utc` para retenção futura. A chave primária já atende a consulta de idempotência por `MessageId`.

#### Limites da idempotência

- Inbox protege a repetição de uma mesma mensagem consumida;
- unicidade de `StockMovement` protege também a duplicidade lógica de baixa;
- `InvoiceIssuanceProcess.IdempotencyKey` protege a repetição HTTP de `PrintInvoice`;
- Outbox garante persistência da intenção, mas entrega continua sendo at-least-once;
- nenhuma dessas garantias será descrita como exactly-once.

A política de retenção será decidida no bloco de auditoria e operação. Até essa decisão, não será presumido job de exclusão.

### 7.6 Bloco 6 - Constraints, índices e concorrência

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Tokens de concorrência

`xmin` será mapeado como `Version: uint` nas estruturas mutáveis sujeitas a disputa:

- `Product`;
- `Invoice`;
- `InvoiceIssuanceProcess`;
- `OutboxMessage`.

`StockMovement` e `InboxMessage` são imutáveis e não recebem token de concorrência.

`InvoiceItem` também não recebe token próprio. Toda inclusão, alteração ou remoção de item deve atualizar `Invoice.UpdatedAtUtc`, obrigando a operação a disputar o `xmin` da raiz. A coleção inteira permanece protegida pela versão do agregado, em vez de permitir versões independentes que poderiam confirmar um conjunto incoerente.

`OutboxMessage` incorpora `Version: uint`. A aquisição e a liberação de leases, o registro de tentativas e a confirmação de publicação são atualizações concorrentes e devem detectar disputa entre dispatchers.

O Identity preserva `ConcurrencyStamp` e não duplica esse mecanismo com `xmin`.

#### Transações e resolução de conflitos

- isolamento padrão `ReadCommitted`;
- transação local explícita quando um fluxo grava mais de uma estrutura;
- `DbContext` como Unit of Work;
- ausência de transação distribuída;
- `DbUpdateConcurrencyException` nunca convertida automaticamente em sucesso;
- estado recarregado e regra reavaliada quando uma nova tentativa for segura;
- proibição de retry cego para comando mutável;
- conflito que altere a decisão de negócio retorna resultado coerente com o estado atualizado.

No cenário da última unidade, duas transações podem ler o mesmo saldo, mas somente uma confirma a versão do produto. A outra recarrega o saldo e produz rejeição por insuficiência, sem saldo negativo e sem baixa parcial.

#### Representação física

- enums como texto em `snake_case` e com conjunto limitado por constraint;
- datas como `timestamptz` e valores produzidos em UTC;
- identificadores como `uuid` nativo;
- payloads da Outbox como `jsonb`;
- tamanhos de texto explícitos;
- inteiros PostgreSQL compatíveis com o intervalo do modelo .NET;
- ausência de colunas monetárias, pois preço e total fiscal estão fora do desafio.

#### Convenção de nomes

```text
pk_<table>
fk_<table>_<referenced_table>
uq_<table>_<columns>
ix_<table>_<columns>
ck_<table>_<rule>
```

Exemplos:

```text
uq_products_code
ck_products_balance_non_negative
uq_invoice_items_invoice_id_product_id
ck_invoices_status_timestamps
uq_invoice_issuance_processes_idempotency_key
ck_outbox_messages_lease_consistency
```

Nomes excessivamente longos podem ser abreviados de modo determinístico na configuração, respeitando o limite do PostgreSQL sem perder o conceito protegido.

#### Divisão de responsabilidades

O domínio valida antes de alterar estado. O banco repete somente garantias locais, determinísticas e relevantes contra corrupção ou acesso concorrente.

Não serão utilizados triggers para reproduzir factories, transições de estado ou regras que exigem contexto externo. A imutabilidade funcional de invoice fechada e de movimentos é protegida pelo domínio e pelos casos de uso; constraints complementam os aspectos estruturais que o banco consegue avaliar.

#### Política de índices

Índices são admitidos somente quando ligados a:

- chaves primárias e unicidade;
- foreign keys locais;
- listagens e paginação previstas;
- histórico auditável;
- consulta de processo ativo;
- aquisição de trabalho da Outbox;
- futura retenção de Inbox e Outbox.

Não serão criados índices especulativos para filtros, relatórios ou buscas ainda inexistentes. Toda inclusão posterior deve apontar para consulta real e ser avaliada por plano de execução quando o volume justificar.

### 7.7 Bloco 7 - Auditoria, retenção e migrations

> Estado: Aprovado pelo engenheiro em 2026-08-18

#### Auditoria mínima de autoria

Os modelos registram somente autores ligados a ações relevantes e realmente disponíveis no fluxo autenticado:

```text
Product.CreatedByUserId
Invoice.CreatedByUserId
InvoiceIssuanceProcess.RequestedByUserId
```

Os valores são UUIDs obtidos da claim autenticada `sub`. São referências externas e não possuem foreign key ou navegação para o banco do Identity.

Essa auditoria permite identificar quem cadastrou o produto, criou a nota e solicitou cada tentativa de emissão. Não será criado `UpdatedByUserId`, histórico de alterações de campos ou tabela genérica `audit_logs`, pois o projeto não possui requisito para reconstrução completa de edições.

O limite deve permanecer explícito: a aplicação registra autoria das principais intenções, mas não alega oferecer trilha de auditoria regulatória completa.

Rastreabilidade técnica e de negócio é complementada por:

- timestamps UTC;
- `StockMovement` com saldos anterior e posterior;
- histórico de `InvoiceIssuanceProcess`;
- `MessageId`, `CorrelationId` e `CausationId`;
- registros de Inbox e Outbox.

#### Retenção

No volume e no horizonte deste desafio, nenhuma rotina automática de exclusão será implementada.

- produtos, invoices, itens, processos e movimentações são preservados;
- Inbox é preservada para não reduzir silenciosamente a janela de idempotência;
- Outbox publicada é preservada como evidência técnica;
- Outbox pendente nunca é descartada por idade ou número de tentativas;
- mensagens em erro continuam sujeitas à política de tentativa e diagnóstico aprovada.

Os índices temporais já definidos tornam uma política futura possível, mas não constituem autorização para apagar registros.

Uma retenção temporal de produção dependeria de volume, backup, compliance, tempo máximo de redelivery e requisitos operacionais inexistentes neste desafio. Criar um job de limpeza agora aumentaria o risco de perda de evidência e de repetição de efeitos.

#### Organização das migrations

Cada serviço possui sequência e histórico próprios:

```text
Identity.Infrastructure  -> identity_db
Inventory.Infrastructure -> inventory_db
Billing.Infrastructure   -> billing_db
```

Regras:

- uma migration inicial independente por serviço;
- migrations e model snapshots armazenados na respectiva Infrastructure;
- nomes explicativos em inglês;
- migrations e snapshots versionados no repositório;
- geração somente após mudança consciente e aprovada do modelo;
- `EnsureCreated` proibido em aplicação e testes de integração de schema;
- APIs não aplicam migrations no startup;
- seed administrativo separado e posterior à aplicação das migrations;
- SQL gerado inspecionado antes da execução;
- alterações destrutivas exigem revisão explícita;
- pipeline futuro falha quando houver model change sem migration correspondente;
- script idempotente pode ser gerado para inspeção e implantação controlada, mas não substitui o histórico do EF Core.

A aplicação será feita por processo ou container controlado. A forma executável, ordem de inicialização, health checks e comportamento diante de falha pertencem ao SDD-11 e não serão antecipados neste documento.

### 7.8 Convenção automática de nomes físicos

> Estado: Aprovado pelo engenheiro em 2026-08-18

`EFCore.NamingConventions` será adotado para aplicar `snake_case` automaticamente a tabelas e colunas. A dependência evita mapeamento mecânico extenso, especialmente nas estruturas do ASP.NET Core Identity, e reduz risco de divergência em evoluções.

A convenção não substitui configurações explícitas de:

- tipos e tamanhos;
- chaves e relacionamentos;
- comportamento de exclusão;
- índices únicos, parciais e ordenados;
- constraints e seus nomes;
- conversões de enums;
- concorrência por `xmin`;
- sequence da invoice.

Versão compatível e estável será verificada no início da futura implementação e fixada em `Directory.Packages.props`. Não será aceita versão preview.

---

## 8. Tratamento de falhas de persistência

Falhas técnicas não podem vazar SQL, nomes internos desnecessários, connection strings, payloads ou valores sensíveis.

### Violação de unicidade ou constraint

Infrastructure identifica constraints conhecidas pelo nome estável e as traduz para erros internos específicos. Application decide a resposta do caso de uso. O texto bruto do PostgreSQL não é retornado ao cliente.

Exemplos:

- código de produto duplicado;
- produto repetido na invoice;
- chave de idempotência já associada a outra intenção;
- segunda emissão ativa;
- movimentação de estoque duplicada.

O SDD-03 definirá os contratos HTTP e códigos públicos finais.

### Concorrência otimista

`DbUpdateConcurrencyException` provoca rollback. O caso de uso pode recarregar o estado em novo escopo e reavaliar a regra quando essa repetição for segura. O erro nunca será ocultado e entidades rastreadas com estado antigo não serão reutilizadas.

### Banco indisponível

- comando não é declarado concluído;
- transação local é revertida;
- API produz erro sanitizado conforme o futuro contrato;
- consumer segue a política de retry técnico;
- migrator encerra com código de falha;
- nenhum serviço tenta acessar o banco de outro serviço como fallback.

### Falha de serialização da Outbox

O contrato é serializado antes da confirmação da transação. Se a serialização falhar, estado de negócio e Outbox não são confirmados parcialmente.

### Falha de migration

A migration interrompe o processo controlado e impede que o ambiente seja considerado pronto. A API não tenta reparar, ignorar ou aplicar automaticamente uma migration que falhou.

---

## 9. Critérios de aceite

### CA-DATA-01 - Isolamento por serviço

**Dado** o modelo persistente dos três serviços,  
**quando** bancos, credenciais, migrations e referências forem inspecionados,  
**então** cada serviço acessará somente seu próprio banco e não existirão FKs ou navegações entre serviços.

### CA-DATA-02 - Modelo mínimo do Identity

**Dado** um `identity_db` vazio,  
**quando** suas migrations e o inicializador controlado forem executados,  
**então** as tabelas do Identity existirão em `snake_case`, haverá um único papel `Admin`, o usuário configurado estará associado a ele e nova execução não duplicará nem redefinirá sua senha.

### CA-DATA-03 - Invariantes de Product

**Dado** o cadastro de um produto,  
**quando** código, descrição e saldo inicial forem processados,  
**então** valores válidos serão normalizados e persistidos, enquanto vazio, formato inválido, duplicidade, excesso de tamanho ou saldo negativo serão rejeitados sem linha inválida.

### CA-DATA-04 - Concorrência do saldo

**Dado** um produto com uma unidade e duas baixas concorrentes dessa unidade,  
**quando** ambas tentarem confirmar a mesma versão,  
**então** apenas uma alterará o saldo e a outra reavaliará o estado sem produzir saldo negativo.

### CA-DATA-05 - Movimento auditável e idempotente

**Dado** uma baixa confirmada,  
**quando** a transação do Inventory for consultada,  
**então** cada produto terá movimento imutável com quantidade e saldos consistentes, sem duplicidade por mensagem ou por nota e produto.

### CA-DATA-06 - Numeração e estado da invoice

**Dado** a criação de invoices,  
**quando** números forem reservados pela sequence,  
**então** serão positivos, únicos e crescentes, com lacunas permitidas, e toda invoice nova será `Open`, desbloqueada e sem data de fechamento.

### CA-DATA-07 - Consistência dos itens

**Dado** uma invoice aberta e desbloqueada,  
**quando** seus itens forem alterados,  
**então** quantidade, snapshots e unicidade de produto serão protegidos e a versão da raiz será atualizada; invoice fechada ou bloqueada não aceitará alteração.

### CA-DATA-08 - Exclusão mútua da emissão

**Dado** duas solicitações concorrentes de emissão para a mesma invoice,  
**quando** processo, bloqueio e Outbox forem persistidos,  
**então** no máximo uma transação criará processo ativo e a invoice permanecerá protegida contra edição.

### CA-DATA-09 - Estados do processo

**Dado** um `InvoiceIssuanceProcess`,  
**quando** uma transição for persistida,  
**então** status, resultado, timestamps e bloqueio da invoice obedecerão às combinações aprovadas e estados terminais não serão sobrescritos por eventos atrasados.

### CA-DATA-10 - Inbox idempotente

**Dado** uma mensagem recebida,  
**quando** seus efeitos forem confirmados,  
**então** Inbox e efeitos locais estarão na mesma transação; repetição com mesmo hash não repetirá efeitos e mesmo ID com conteúdo diferente será tratado como inconsistência.

### CA-DATA-11 - Outbox recuperável

**Dado** uma intenção persistida na Outbox,  
**quando** leases concorrentes, falhas e uma confirmação explícita de publicação forem registradas no modelo,  
**então** a concorrência impedirá reserva simultânea intencional, somente a operação de sucesso preencherá `PublishedAtUtc` e a intenção não será descartada.

A prova de que essa operação de sucesso ocorre somente depois do publisher confirm real pertence ao SDD-07, pois publishers e RabbitMQ estão fora desta modelagem.

### CA-DATA-12 - Migrations reproduzíveis

**Dado** bancos PostgreSQL vazios,  
**quando** as três sequências de migrations forem aplicadas por processo controlado,  
**então** os schemas esperados serão produzidos sem `EnsureCreated`, sem intervenção manual e sem a API aplicar alterações no startup.

### CA-DATA-13 - Autoria sem acoplamento

**Dado** uma operação autenticada que cria produto, invoice ou processo,  
**quando** o estado for persistido,  
**então** o UUID do usuário será registrado sem FK, consulta ou navegação para o banco do Identity.

### CA-DATA-14 - Ausência de modelo especulativo

**Dado** o schema final desta fase,  
**quando** suas estruturas forem comparadas ao escopo,  
**então** não existirão fornecedor, compra, preço, tributo, pagamento, soft delete, refresh token, sessão, audit log genérico ou tabela sem responsabilidade aprovada.

---

## 10. Estratégia de testes planejada

Testes de persistência utilizarão PostgreSQL real em container. Banco em memória ou provider diferente não comprova constraints, sequence, `xmin`, índices parciais, `jsonb` ou comportamento do Npgsql.

| ID | Teste planejado | Nível | Critérios |
|---|---|---|---|
| TST-DATA-001 | Aplicar cada migration do zero | Integração | CA-DATA-01, CA-DATA-12 |
| TST-DATA-002 | Validar tabelas, nomes físicos e isolamento | Integração/arquitetura | CA-DATA-01, CA-DATA-02 |
| TST-DATA-003 | Executar seed administrativo duas vezes | Integração | CA-DATA-02 |
| TST-DATA-004 | Validar factory, normalização e invariantes de Product | Unitário | CA-DATA-03 |
| TST-DATA-005 | Forçar violações de constraints de Product | Integração | CA-DATA-03 |
| TST-DATA-006 | Disputar a última unidade em dois contextos | Integração | CA-DATA-04 |
| TST-DATA-007 | Confirmar baixa e movimentos na mesma transação | Integração | CA-DATA-05 |
| TST-DATA-008 | Repetir movimento por evento e por invoice/produto | Integração | CA-DATA-05 |
| TST-DATA-009 | Criar invoices concorrentes e inspecionar sequence | Integração | CA-DATA-06 |
| TST-DATA-010 | Validar estados e timestamps de Invoice | Unitário/integração | CA-DATA-06, CA-DATA-07 |
| TST-DATA-011 | Alterar itens e disputar versão da raiz | Integração | CA-DATA-07 |
| TST-DATA-012 | Criar emissões concorrentes | Integração | CA-DATA-08 |
| TST-DATA-013 | Exercitar transições válidas e inválidas do processo | Unitário/integração | CA-DATA-09 |
| TST-DATA-014 | Reentregar mesma mensagem e mensagem adulterada | Integração | CA-DATA-10 |
| TST-DATA-015 | Disputar e expirar lease de Outbox | Integração | CA-DATA-11 |
| TST-DATA-016 | Registrar falha e sucesso explícitos sem usar broker | Unitário/integração de persistência | CA-DATA-11 |
| TST-DATA-017 | Verificar model changes pendentes | Build/QA | CA-DATA-12 |
| TST-DATA-018 | Persistir autoria e provar ausência de FK externa | Integração | CA-DATA-13 |
| TST-DATA-019 | Inspecionar schema contra lista negativa | Auditoria | CA-DATA-14 |

Testes unitários cobrem regras puras sem banco. Testes de integração cobrem garantias específicas do PostgreSQL e transações reais. Migrations geradas e código sem lógica podem ser excluídos da cobertura conforme ADR-014; Domain, Application e Infrastructure manual relevante permanecem sujeitos ao gate de 80% quando implementados.

---

## 11. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Modelo técnico maior que o domínio básico | Prazo e manutenção | Estruturas limitadas à confiabilidade já aprovada; sem abstrações genéricas |
| Acoplamento ao PostgreSQL por `xmin`, sequence, `jsonb` e índice parcial | Menor portabilidade | Decisão consciente; testes usam o banco real adotado |
| Enum textual alterado sem migration | Dados incompatíveis | Valores canônicos estáveis e mudança somente por migration revisada |
| Crescimento de Inbox e Outbox | Armazenamento crescente | Volume do desafio é baixo; índices temporais permitem política futura aprovada |
| Lease implementado incorretamente | Duplicidade de publicação concorrente | `xmin`, aquisição atômica, expiração e testes com múltiplos contextos |
| Bloqueio da invoice divergente do processo | Edição ou reemissão insegura | Atualizações na mesma transação e testes de todas as transições |
| Dados de autoria confundidos com FK | Acoplamento ao Identity | UUID externo sem navegação, consulta ou constraint entre bancos |
| Migration destrutiva aplicada automaticamente | Perda de dados | Processo separado, revisão de SQL e ausência de migration no startup |
| Retenção indefinida interpretada como política de produção | Crescimento futuro | Limitação registrada e reavaliação exigida antes de produção |

---

## 12. Marcadores de qualidade

| Marcador | Exigência neste SDD |
|---|---|
| ESP | Modelo completo aprovado antes de entidades, configurações ou migrations |
| RAS | Cada estrutura e critério ligado à matriz e a teste planejado |
| ARC | Bancos, modelos e migrations isolados por serviço |
| DOM | Agregados e invariantes protegidos antes da persistência |
| ERR | Constraints e falhas técnicas traduzidas sem vazamento de detalhes |
| SEG | Credenciais isoladas, senha fora de seed/migration e autoria sem FK externa |
| TST | PostgreSQL real para garantias específicas e 80% sobre lógica relevante |
| INT | Transações, concorrência, Inbox e Outbox verificadas com infraestrutura real |
| OBS | Correlação, movimentos, processos e erros sanitizados persistem evidência suficiente |
| DOC | Diagramas conceituais, tabelas, constraints, migrations e limites documentados |
| QA | Model changes, schema negativo e SQL de migrations auditados |

---

## 13. Limites para a futura implementação

Uma implementação deste SDD poderá criar:

- entidades, enums e factories de domínio aprovados;
- modelos técnicos locais de Inbox e Outbox;
- `DbContext` e configurações Fluent API por serviço;
- dependências EF Core, Npgsql, Identity EF e convenção de nomes aprovadas;
- migrations iniciais e testes de persistência;
- abstrações mínimas necessárias para testar transações e concorrência.

Não poderá criar antecipadamente:

- endpoints ou contratos HTTP;
- contratos finais de eventos;
- publishers, consumers ou topologia RabbitMQ;
- casos de uso funcionais completos;
- geração e validação JWT;
- telas ou serviços Angular;
- composição final do ambiente.

Qualquer necessidade que ultrapasse esse limite exige o SDD responsável ou atualização documental previamente aprovada.

---

## 14. Condição para Gate A

O SDD-02 estará apto ao Gate A quando:

- todos os blocos de decisão estiverem aprovados;
- entidades, estruturas técnicas, constraints e índices não apresentarem contradições;
- cada critério possuir teste planejado;
- matriz de rastreabilidade for atualizada;
- dependências documentais futuras estiverem identificadas;
- revisão integral não encontrar decisão implícita relevante.

A aprovação do Gate A concluirá a especificação da modelagem, mas, conforme a macroetapa atual, não autorizará implementação. O projeto seguirá para o SDD-03.
