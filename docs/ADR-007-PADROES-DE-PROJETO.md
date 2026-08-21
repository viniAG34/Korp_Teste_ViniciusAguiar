# ADR-007 - Padrões de Projeto

> Status: Aprovada
> Data: 2026-08-16
> Dependências: ADR-004 e ADR-006

---

## Contexto

O projeto precisa aplicar padrões que protejam invariantes, limites arquiteturais e o fluxo distribuído. A aplicação indiscriminada de padrões aumentaria a complexidade sem benefício e prejudicaria a legibilidade.

---

## Decisão

Serão adotados somente padrões ligados a uma necessidade concreta do sistema. O catálogo é dividido entre padrões aprovados, condicionais e não adotados inicialmente.

### Padrões aprovados

#### Criacionais

- Factory Method para criação válida das entidades e agregados.
- Injeção de dependência, com lifetimes administrados pelo container nativo do ASP.NET Core.

#### Estruturais

- Adapter nas fronteiras de mensageria e integrações técnicas.
- Decorator para comportamentos transversais dos casos de uso.
- Proxy por meio do API Gateway como reverse proxy.
- Repository por agregado, sem repositório genérico universal.
- Unit of Work exercida pelo `DbContext`, sem abstração adicional duplicada.
- Anti-Corruption Layer entre contratos externos e operações de domínio.

#### Comportamentais

- Command para intenções que alteram estado.
- separação entre comandos e consultas, sem CQRS físico obrigatório.
- State como máquina de estados protegida por métodos do domínio.
- Observer/Publish-Subscribe por meio das mensagens no RabbitMQ.
- Chain of Responsibility nos pipelines HTTP, de aplicação e consumo.

#### Sistemas distribuídos

- Transactional Outbox;
- Inbox/Idempotent Consumer;
- Process Manager no Faturamento;
- retry com backoff;
- Dead Letter Queue;
- controle de concorrência otimista;
- Correlation ID e Causation ID;
- entrega at-least-once com consumidores idempotentes.

### Padrões condicionais

- Builder somente para organizar dados e cenários de testes.
- Strategy somente quando existirem duas implementações reais intercambiáveis aprovadas.
- Specification somente quando consultas complexas e reutilizáveis justificarem a abstração.
- Mediator somente após avaliação de custo, biblioteca e benefício para os casos de uso.

### Padrões não adotados inicialmente

- Abstract Factory;
- Singleton manual;
- Prototype;
- Composite;
- Bridge;
- Flyweight;
- Template Method;
- Visitor;
- Memento;
- Saga com compensação sem cenário real de efeito parcial.

---

## Regras de aplicação

- Um padrão deve resolver um problema identificável no SDD.
- O nome do padrão não substitui a explicação de sua responsabilidade.
- Padrões condicionais exigem necessidade concreta e aprovação antes da implementação.
- Adapters e consumers convertem contratos externos antes de chamar o domínio.
- Máquina de estados simples não exige uma classe por estado.
- `DbContext` já cumpre Unit of Work; uma camada adicional é proibida sem novo motivo.
- O API Gateway é proxy de infraestrutura e não facade de regras de negócio.
- Compensação só será adicionada se existir um efeito parcial que não possa ser evitado por atomicidade e idempotência.

---

## Consequências

- O detalhamento técnico poderá apontar padrões e casos reais de uso.
- Testes deverão verificar o comportamento protegido pelo padrão, não a presença de classes com nomes específicos.
- O catálogo reduz abstrações prematuras.
- Novos padrões exigem justificativa registrada no SDD ou em novo ADR.
