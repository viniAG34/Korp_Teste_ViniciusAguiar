# ADR-006 - Clean Architecture por Microsserviço

> Status: Aprovada
> Data: 2026-08-16
> Atualizada em: 2026-08-17 para incorporar Identity.Service e a nomenclatura inglesa aprovada

---

## Contexto

Inventory e Billing possuem regras próprias, persistência isolada e integração por mensagens. Identity possui persistência e responsabilidades próprias de autenticação. É necessário organizar cada serviço de forma que regras internas não dependam indevidamente de ASP.NET Core, banco de dados ou RabbitMQ.

O API Gateway não possui domínio próprio e `Shared.Contracts` deve conter apenas contratos de integração.

---

## Decisão

Cada serviço com persistência utilizará a estrutura de Clean Architecture com quatro projetos, preservando apenas responsabilidades que existam de fato:

```text
Korp.<Servico>.Api
Korp.<Servico>.Application
Korp.<Servico>.Domain
Korp.<Servico>.Infrastructure
```

A solução terá também:

```text
Korp.Gateway.Api
Korp.Shared.Contracts
```

Estrutura prevista:

```text
src/
|-- Services/
|   |-- Identity/
|   |   |-- Korp.Identity.Api/
|   |   |-- Korp.Identity.Application/
|   |   |-- Korp.Identity.Domain/
|   |   `-- Korp.Identity.Infrastructure/
|   |-- Inventory/
|   |   |-- Korp.Inventory.Api/
|   |   |-- Korp.Inventory.Application/
|   |   |-- Korp.Inventory.Domain/
|   |   `-- Korp.Inventory.Infrastructure/
|   `-- Billing/
|       |-- Korp.Billing.Api/
|       |-- Korp.Billing.Application/
|       |-- Korp.Billing.Domain/
|       `-- Korp.Billing.Infrastructure/
|-- Gateway/
|   `-- Korp.Gateway.Api/
`-- Shared/
    `-- Korp.Shared.Contracts/
```

---

## Responsabilidades

### Domain

- entidades e value objects;
- invariantes e transições de estado;
- exceções de domínio;
- eventos internos de domínio, quando necessários;
- nenhuma dependência de ASP.NET Core, Entity Framework Core ou RabbitMQ.

### Application

- casos de uso;
- comandos e consultas;
- DTOs internos;
- validação de entrada dos casos de uso;
- interfaces de repositórios, mensageria e serviços externos;
- coordenação do domínio.

### Infrastructure

- persistência e migrations;
- implementação de repositórios;
- mensageria;
- Outbox e Inbox;
- integrações técnicas;
- observabilidade de infraestrutura.

### Api

- endpoints e contratos HTTP;
- configuração do host;
- injeção de dependência;
- middlewares;
- OpenAPI;
- health checks.

### Shared.Contracts

- somente mensagens imutáveis de integração;
- nenhuma entidade ou regra de domínio;
- nenhuma interface de repositório;
- nenhuma dependência de Entity Framework Core ou framework de aplicação.

### API Gateway

- projeto único e stateless;
- roteamento HTTP e políticas de borda;
- nenhuma camada de domínio artificial;
- nenhuma persistência ou mensageria.

### Identity

- utiliza ASP.NET Core Identity sem duplicar suas abstrações internas;
- mantém usuários, credenciais, migrations e emissão de tokens isolados;
- Domain contém somente regras próprias que existam de fato;
- não referencia contratos de estoque, faturamento ou mensageria.

---

## Regra de dependência

```text
Domain            -> nenhuma camada interna
Application       -> Domain
Infrastructure    -> Application + Domain
Api               -> Application + Infrastructure
Shared.Contracts  -> nenhuma camada dos serviços
```

Um serviço nunca referencia projetos `Domain`, `Application`, `Infrastructure` ou `Api` de outro serviço. Inventory e Billing podem referenciar `Shared.Contracts` para publicar e consumir contratos comuns. Identity não referencia esse projeto porque não participa da integração de emissão.

---

## Limites pragmáticos

Não serão criados sem necessidade aprovada:

- projeto genérico `Core`;
- camada separada de `Presentation`;
- biblioteca compartilhada de utilitários diversos;
- repositório genérico universal;
- Unit of Work adicional sobre o `DbContext`;
- projeto independente para cada feature;
- bancos separados para leitura e escrita apenas para declarar CQRS.

---

## Consequências

- regras podem ser testadas sem banco ou broker;
- dependências técnicas permanecem nas bordas;
- a solução terá mais projetos e configuração inicial;
- testes de arquitetura deverão verificar as dependências entre camadas;
- decisões sobre padrões e bibliotecas deverão respeitar essas fronteiras.
