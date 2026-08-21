# Convenções de Código

> Status: Aprovado
> Aprovação: 2026-08-17
> Última atualização: 2026-08-20
> Dependências: `VISAO-GERAL.md` e ADR-005 a ADR-016

---

## 0. Aplicação e força normativa

Este documento consolida regras recorrentes para implementação e revisão. Ele não substitui regras de negócio dos ADRs e SDDs. Em conflito, prevalece a ordem definida no `AGENTS.md`.

As convenções são obrigatórias quando utilizam “deve”, “não pode” ou apresentam uma proibição. Exemplos são ilustrativos e devem seguir o vocabulário de [`GLOSSARIO.md`](GLOSSARIO.md). Uma exceção exige justificativa no SDD ou nova decisão quando alterar arquitetura, segurança, persistência ou comportamento.

Antes de concluir uma fase, o desenvolvedor deve verificar:

- referências entre projetos e propriedade dos dados;
- nomes técnicos em inglês e termos canônicos;
- validação nas fronteiras e invariantes no domínio;
- propagação de cancelamento e correlação;
- ausência de segredos e dados sensíveis;
- critérios de aceite, testes e cobertura;
- atualização da [`MATRIZ-RASTREABILIDADE.md`](MATRIZ-RASTREABILIDADE.md).

---

## 1. Injeção de dependência

A injeção de dependência é obrigatória nas camadas de aplicação, infraestrutura e API. Será utilizado por padrão o container nativo do ASP.NET Core.

### 1.1 Regras gerais

- dependências são recebidas por construtor;
- classes não instanciam diretamente suas dependências técnicas;
- interfaces pertencem à camada que necessita da abstração;
- implementações técnicas pertencem à Infrastructure;
- registros são centralizados no composition root de cada aplicação;
- o domínio permanece independente do container de DI;
- `IServiceProvider` não pode ser usado como Service Locator;
- dependências não podem ser resolvidas manualmente dentro de casos de uso;
- propriedades públicas mutáveis não serão usadas para injeção;
- dependências opcionais não serão representadas por parâmetros nulos;
- configurações serão fornecidas pelo Options Pattern, não por leitura dispersa de chaves de configuração.

### 1.2 Composition root

O projeto `Api` de cada microsserviço é responsável por compor a aplicação:

```text
Api
  -> registra casos de uso da Application
  -> registra implementações da Infrastructure
  -> configura banco, mensageria, observabilidade e opções
  -> constrói o host
```

O API Gateway possui seu próprio composition root, limitado às dependências de roteamento e políticas de borda.

Para evitar um `Program.cs` excessivamente grande, cada camada pode expor métodos de extensão de registro, como:

```csharp
services.AddInventoryApplication();
services.AddInventoryInfrastructure(configuration);
```

Esses métodos apenas registram dependências. Eles não executam migrations, publicam mensagens ou iniciam regras de negócio durante o registro.

### 1.3 Lifetimes iniciais

| Componente | Lifetime | Justificativa |
|---|---|---|
| `DbContext` | Scoped | Uma unidade de persistência por requisição ou escopo de mensagem |
| Repositórios | Scoped | Compartilham o `DbContext` do escopo atual |
| Casos de uso sem estado | Transient | Não preservam estado entre execuções |
| Validadores sem estado | Transient | Executados sob demanda |
| Clientes HTTP | Gerenciados por `IHttpClientFactory` | Reuso seguro de handlers e conexões |
| Configurações | Options Pattern | Configuração tipada e validável |
| Conexões de mensageria | Gerenciadas pela biblioteca adotada | Conexões devem ser reutilizadas, não abertas por mensagem |
| Consumers | Escopo por mensagem | Cada entrega recebe dependências e `DbContext` próprios |

Lifetimes específicos de bibliotecas serão confirmados depois da escolha das dependências. Nenhum serviço `Scoped` poderá ser capturado por um `Singleton`.

### 1.4 Limites entre camadas

Exemplo conceitual:

```text
Application defines: IProductRepository
Infrastructure implements: ProductRepository
Api registers: IProductRepository -> ProductRepository
Use case receives: IProductRepository through its constructor
```

A Application não referencia a Infrastructure. A inversão ocorre por interfaces, conforme a regra de dependência do ADR-006.

### 1.5 Proibições

Não utilizar:

- `new ProductRepository(...)` dentro de endpoint ou caso de uso;
- estado global mutável;
- Singleton implementado manualmente;
- `IServiceProvider.GetRequiredService(...)` dentro da lógica de negócio;
- dependência direta de implementação quando existe uma fronteira técnica substituível;
- registro duplicado ou contraditório da mesma abstração;
- dependências com lifetime incompatível.

### 1.6 Critérios de revisão

- [ ] Toda dependência técnica é injetada.
- [ ] O domínio não conhece o container.
- [ ] Application não referencia Infrastructure.
- [ ] `DbContext` e repositórios compartilham o escopo correto.
- [ ] Nenhum `Singleton` captura dependência `Scoped`.
- [ ] Configurações são tipadas e validadas no startup.
- [ ] Consumers criam ou recebem um escopo independente por mensagem.
- [ ] Não existe uso de Service Locator na lógica da aplicação.

---

## 2. Padrões de projeto

O catálogo completo e suas justificativas estão no ADR-007. Um padrão só pode ser utilizado quando resolver uma necessidade concreta do SDD.

### 2.1 Criacionais

#### Factory Method

Entidades e agregados devem ser criados por métodos de fábrica que protejam invariantes desde a origem:

```csharp
var product = Product.Create(code, description, initialBalance);
var invoice = Invoice.Create(sequentialNumber);
```

- construtores usados pelo ORM permanecem privados ou protegidos;
- não se cria entidade válida para depois "completá-la" por setters;
- validações obrigatórias ocorrem antes de devolver a instância;
- Builder de produção não será utilizado sem complexidade real de construção.

#### Injeção de dependência

Segue integralmente as regras da seção 1. Singleton manual é proibido.

### 2.2 Estruturais

#### Adapter

Application define portas e Infrastructure fornece adapters para RabbitMQ, persistência e outras integrações. Código de domínio não conhece bibliotecas externas.

#### Decorator

Comportamentos transversais podem envolver casos de uso sem contaminar sua regra principal:

- logging;
- validação;
- métricas;
- idempotência;
- tratamento técnico aplicável ao pipeline.

O mecanismo concreto será definido por necessidade no SDD, sem depender de Mediator. Não duplicar o mesmo comportamento em Decorator, middleware e endpoint.

#### Proxy

O API Gateway atua como reverse proxy HTTP. Ele não implementa domínio, persistência ou mensageria, conforme ADR-004.

#### Repository e Unit of Work

- repositórios são específicos por agregado;
- não existe repositório genérico universal;
- interfaces ficam na Application;
- implementações ficam na Infrastructure;
- `DbContext` cumpre Unit of Work;
- não criar `IUnitOfWork` que apenas replique `SaveChangesAsync`.

#### Anti-Corruption Layer

Contratos HTTP e mensagens são convertidos em comandos e tipos internos antes de alcançar o domínio. Entidades não recebem diretamente DTOs externos.

### 2.3 Comportamentais

#### Command e separação de consultas

Intenções de alteração possuem casos de uso explícitos, como:

```text
CreateProduct
CreateInvoice
AddInvoiceItem
PrintInvoice
DeductInvoiceStock
```

Consultas não alteram estado. A separação lógica não implica bancos distintos de leitura e escrita.

#### State

Estados e transições são protegidos por métodos do agregado. Enums com comportamento guardado são preferidos quando suficientes; não criar uma classe para cada estado simples.

#### Observer/Publish-Subscribe

RabbitMQ implementa a comunicação publish-subscribe entre serviços. Eventos de integração não substituem chamadas internas de domínio.

#### Chain of Responsibility

Pipelines podem aplicar validação, logging, tratamento de erro e outras políticas em ordem explícita. A ordem deverá ser documentada quando alterar comportamento.

### 2.4 Sistemas distribuídos

São obrigatórios no fluxo de emissão, conforme detalhamento posterior dos SDDs:

- Transactional Outbox;
- Inbox/Idempotent Consumer;
- Process Manager pertencente ao Faturamento;
- retry com backoff para falhas temporárias;
- DLQ após esgotamento das tentativas;
- concorrência otimista no Estoque;
- Correlation ID e Causation ID;
- consumidores preparados para entrega at-least-once.

### 2.5 Padrões condicionais

Exigem necessidade concreta antes de serem utilizados:

- Builder em testes;
- Strategy para implementações realmente intercambiáveis;
- Specification para consultas complexas e reutilizáveis;
- Mediator após avaliação da biblioteca e do ganho arquitetural.

### 2.6 Padrões não adotados inicialmente

Não introduzir sem nova justificativa aprovada:

- Abstract Factory;
- Singleton manual;
- Prototype;
- Composite;
- Bridge;
- Flyweight;
- Template Method;
- Visitor;
- Memento;
- Saga com compensação sem efeito parcial real.

### 2.7 Checklist de padrões

- [ ] Cada padrão resolve um requisito ou risco identificado.
- [ ] Nenhuma abstração foi criada para apenas uma hipótese futura.
- [ ] Entidades são criadas em estado válido.
- [ ] Contratos externos não atravessam diretamente para o domínio.
- [ ] O `DbContext` não foi embrulhado por Unit of Work redundante.
- [ ] A máquina de estados rejeita transições inválidas.
- [ ] Consumers são idempotentes.
- [ ] Padrões condicionais possuem justificativa aprovada.

---

## 3. Idioma e nomenclatura

- Todo identificador técnico é escrito em inglês.
- Nomes devem ser descritivos e refletir a responsabilidade real.
- Classes e métodos usam termos do glossário, não sinônimos alternados.
- Abreviações não universais são proibidas.
- Endpoints e JSON usam nomes em inglês.
- Banco PostgreSQL usa `snake_case`.
- Documentação e mensagens destinadas ao usuário permanecem em português.

Exemplos aprovados:

```text
Product
StockMovement
Invoice
InvoiceItem
InvoiceIssuanceProcess
CreateProduct
CreateInvoice
AddInvoiceItem
PrintInvoice
DeductInvoiceStock
StockDeductionRequested
StockDeductionCompleted
StockDeductionRejected
StockDeductionProcessingFailed
```

---

## 4. Persistência

- PostgreSQL com banco lógico e credencial por serviço.
- Entity Framework Core 10 e Npgsql 10.
- Modelagem Code First configurada por Fluent API.
- `DbContext` específico por serviço.
- `AsNoTracking` em consultas somente leitura.
- Lazy loading desabilitado.
- `CancellationToken` propagado em operações assíncronas.
- Migrations versionadas e executadas por migrators.
- `EnsureCreated` proibido.
- Transações locais; nenhuma transação distribuída entre serviços.
- Concorrência otimista de saldo por `xmin`.
- UUID para entidades e eventos.
- PostgreSQL sequence para invoice number, com lacunas permitidas.
- Datas armazenadas em UTC.
- Constraints e índices complementam, mas não substituem, invariantes do domínio.

---

## 5. Ambiente de desenvolvimento

- Todo build, execução, migration e teste usa Docker e Docker Compose.
- O host precisa somente de Docker e Docker Compose.
- Dockerfiles usam multi-stage build.
- Imagens finais não carregam SDK ou ferramentas de desenvolvimento desnecessárias.
- O fluxo utilizado no CI deve reproduzir os comandos oficiais do ambiente local.

---

## 6. Testes e cobertura

- Testes unitários e de integração são obrigatórios.
- Testes de integração utilizam PostgreSQL e RabbitMQ reais no ambiente Docker.
- Coverlet coleta cobertura do backend em formato Cobertura XML.
- Cada assembly de produção relevante deve atingir no mínimo 80% de line coverage.
- A média global é informativa e não compensa assembly abaixo do gate.
- Branch coverage é coletada e acompanhada.
- Migrations e código gerado podem ser excluídos; regra de negócio e código manual não.
- Exclusões precisam ser explícitas, visíveis e justificáveis.
- ReportGenerator é auxiliar para consolidar e visualizar; não substitui coleta nem gate.
- Relatórios gerados não são versionados.
- Percentual global não substitui cobertura explícita dos critérios de aceite.
- Falha no gate de cobertura impede conclusão da fase.

---

## 7. APIs e contratos HTTP

- ASP.NET Core Minimal APIs organizadas por feature.
- Endpoints delegam imediatamente para casos de uso da Application.
- Casos de uso são injetados diretamente; MediatR não será utilizado inicialmente.
- Rotas possuem prefixo `/api/v1`.
- DTOs de request e response são records dedicados.
- Entidades não são retornadas diretamente.
- JSON usa `camelCase` e datas ISO 8601 UTC.
- Validação nativa trata somente o contrato HTTP.
- Regras de Application e Domain não dependem de Data Annotations.
- `PrintInvoice` é o único comando público de impressão.
- Impressão aceita retorna `202 Accepted`.

---

## 8. Erros e exceções

- Respostas de erro usam `ProblemDetails` ou `ValidationProblemDetails`.
- `code` é estável e em inglês; `detail` pode ser exibido em português.
- `traceId` acompanha toda resposta de erro.
- `IExceptionHandler` centraliza mapeamento e possui lifetime Singleton.
- Exception handlers não injetam dependências Scoped.
- Erro de validação retorna `400`.
- Recurso inexistente retorna `404`.
- Duplicidade ou estado inválido retorna `409`.
- Solicitação não persistida por indisponibilidade retorna `503`.
- Erro inesperado retorna `500` sem detalhes sensíveis.
- Saldo insuficiente descoberto assincronamente atualiza o processo; não altera a resposta `202` já enviada.

---

## 9. Mensageria

- Biblioteca oficial `RabbitMQ.Client` 7.x.
- Conexão longa e reutilizada por processo.
- Channels possuem ownership explícito e não são usados concorrentemente sem sincronização.
- Manual acknowledgement somente após commit.
- Publisher confirms obrigatórios.
- Mensagens persistentes.
- Prefetch limitado.
- Outbox e Inbox obrigatórias.
- Entrega assumida como at-least-once.
- Redelivery e duplicidade são testadas.
- Falhas de negócio publicam rejeição e recebem ack, sem retry ou DLQ.
- Falhas técnicas utilizam retry e DLQ conforme política do SDD.
- RabbitMQ indisponível não impede `202` quando processo e Outbox já foram persistidos.
- Mensagem aguardando consumo na fila principal não conta como tentativa com falha.
- Falhas técnicas após entrega usam atrasos de 5 segundos, 30 segundos e 2 minutos antes da DLQ.
- Retry atrasado usa filas com TTL e dead-letter routing, sem plugin adicional.
- Mensagens inválidas ou com versão incompatível seguem diretamente para DLQ.
- DLQ não possui redrive automático infinito.
- Outbox é consultada a cada 1 segundo, em lotes de até 50, com backoff de falha limitado a 30 segundos.
- Outbox não descarta intenção persistida por limite de tentativas.

---

## 10. OpenAPI e observabilidade

- Cada serviço gera documento OpenAPI próprio com `Microsoft.AspNetCore.OpenApi`.
- Gateway roteia `/openapi/identity/v1.json`, `/openapi/inventory/v1.json` e `/openapi/billing/v1.json`.
- Gateway não combina contratos.
- Logs usam templates estruturados de `ILogger<T>`.
- Não usar interpolação em mensagens de log.
- Propagar `traceId`, `correlationId` e `causationId`.
- Não registrar segredos ou payloads sensíveis.

---

## 11. Regras de domínio transversais

- Product code é obrigatório, normalizado para uppercase, imutável e único.
- Product code possui no máximo 50 caracteres e aceita somente caracteres alfanuméricos, hífen, underscore e ponto.
- Product description é obrigatória e possui no máximo 200 caracteres.
- Balance e quantities são inteiros; zero é aceito apenas como saldo.
- Balance nunca fica negativo.
- StockMovement registra toda baixa confirmada.
- Invoice nasce `Open` e termina `Closed`.
- Invoice fechada é imutável e não pode ser impressa novamente.
- Invoice pode existir vazia enquanto aberta, mas não pode ser impressa vazia.
- Product aparece uma vez por invoice.
- Billing guarda snapshot de ProductCode e ProductDescription, nunca saldo.
- Inclusão de item valida Product por HTTP interno direto ao Inventory.
- Emissão ativa bloqueia alterações de itens.
- Baixa de todos os itens é atômica.
- Rejeição mantém invoice aberta e permite correção.
- Retry idempotente com a mesma chave recupera o processo existente sem nova impressão.
- O processo de emissão usa `Pending`, `AwaitingStock`, `Completed`, `Rejected` e `ManualIntervention`.
- Tempo decorrido não produz transição terminal; atraso é apenas informação derivada.
- `Pending` e `AwaitingStock` mantêm a invoice aberta e bloqueada.
- `Rejected` mantém a invoice aberta e desbloqueia correção.
- `Completed` fecha a invoice e libera o documento imprimível.
- `ManualIntervention` mantém a invoice aberta e bloqueada para preservar um efeito técnico incerto.

---

## 12. Acompanhamento e resiliência HTTP

- Consulta do processo usa `GET /api/v1/invoice-issuance-processes/{processId}`.
- Polling ocorre a cada 1 segundo nos primeiros 10 segundos e a cada 3 segundos depois.
- O cliente encerra polling em estado terminal, logout ou destruição do componente.
- A permanência prolongada em estado ativo pode ser informada como atraso, mas não encerra automaticamente o acompanhamento nem produz decisão de sucesso ou falha.
- `isDelayed` pode ser derivado após 5 segundos em `Pending` ou `AwaitingStock`; não é estado persistido.
- `GET` pode ter retry automático.
- `PrintInvoice` não possui retry automático cego e deve preservar a mesma `Idempotency-Key` quando repetido.
- A consulta HTTP interna de produto possui timeout de 3 segundos e uma única repetição transitória.

---

## 13. Autenticação e autorização

- ASP.NET Core Identity é responsável por usuários e hash de senha no `Identity.Service`.
- JWT bearer é validado no Gateway, Inventory API e Billing API.
- Assinatura, algoritmo, issuer, audience, validade e claims exigidas são sempre verificados.
- Inventory e Billing não consultam o banco de Identidade nem chamam Identity por requisição.
- Login e health checks são anônimos; operações funcionais exigem política explícita.
- Ausência ou invalidade de credencial retorna `401`; falta de permissão retorna `403`.
- O perfil inicial é `Admin`; novos perfis exigem regras de autorização reais e aprovação.
- Access token possui curta duração e não utiliza refresh token nesta entrega.
- Segredos e credenciais são fornecidos pelo ambiente e nunca versionados ou registrados em log.
- Seed administrativo é idempotente e não redefine credenciais existentes no startup.
- Configuração sensível ausente impede inicialização segura; não existe senha padrão embutida.
- Guardas e interceptor do Angular melhoram a experiência, mas não substituem autorização no backend.

---

## 14. Frontend e biblioteca visual

- Angular Material é a única biblioteca completa de componentes visuais.
- A major version do Material acompanha a major version do Angular.
- Componentes standalone importam apenas os módulos utilizados.
- Tema, tokens semânticos e layout são próprios da aplicação.
- SCSS cobre responsividade, domínio e mídia de impressão.
- Não misturar Angular Material com Bootstrap, PrimeNG, Tailwind ou outro framework completo sem nova decisão.
- Componentes próprios representam comportamento ou conceito recorrente; não envolvem controles Material sem benefício.
- A interface mantém labels, foco visível, teclado, contraste e mensagens acessíveis.
- Component Harnesses são preferidos quando tornarem os testes menos acoplados ao HTML interno.
- Fontes, ícones e recursos obrigatórios não dependem de CDN em runtime.

---

## 15. Impressão

- `PrintInvoice` é o único comando de negócio que inicia emissão e impressão.
- Após `Completed`, o frontend renderiza HTML e solicita o diálogo nativo com `window.print()`.
- CSS de mídia de impressão oculta navegação, ações e elementos não pertencentes à nota.
- Não gerar, armazenar ou disponibilizar PDF pela aplicação.
- O conteúdo não inventa preço, impostos, cliente ou dados fiscais fora do escopo.
- O frontend não oferece nova ação de impressão para invoice fechada.
- Cancelar ou bloquear o diálogo não reabre a invoice nem repete a baixa.
- Testes comprovam o gatilho de impressão, não a impressão física.
