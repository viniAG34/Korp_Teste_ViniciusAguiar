# SDD-01 - Setup e Arquitetura da Solução

> Status: Validado
> Versão: 1.0
> Data: 2026-08-17
> Aprovação do Gate A: 2026-08-17
> Gate C aprovado em: 2026-08-17

---

## 1. Objetivo

Criar a base executável e reproduzível do projeto Korp ERP, estabelecendo:

- estrutura física do repositório;
- solution e projetos .NET;
- workspace Angular;
- referências permitidas entre projetos;
- gerenciamento central de versões;
- imagens de build e runtime;
- comandos Docker-first;
- configurações comuns de compilação;
- projetos iniciais de testes;
- gates objetivos que comprovem que a base está pronta para receber os próximos SDDs.

Este SDD cria estrutura, não implementa regras de Produtos, Notas, Identidade, mensageria ou persistência.

---

## 2. Rastreabilidade

Este SDD atende ou prepara:

| ID | Contribuição desta fase |
|---|---|
| OBR-001 | Cria o workspace Angular executável |
| OBR-016 | Cria Inventory e Billing como aplicações independentes |
| OBR-022 | Prepara serviços e Compose para bancos reais, sem modelá-los ainda |
| APR-006 | Centraliza dependências para inventário posterior |
| APR-008 | Fixa frameworks e versões do backend |
| DIF-001 | Cria o projeto stateless do Gateway |
| DIF-002 | Cria a fronteira física do Identity.Service |
| DIF-006 | Materializa as regras de Clean Architecture |
| DIF-007 | Cria o fluxo Docker-first |
| QLT-005 | Prepara o projeto de testes de arquitetura |
| QLT-008 | Define build sem erros e avisos injustificados como gate |

---

## 3. Dependências documentais

Antes da implementação devem ser lidos:

- `../AGENTS.md`;
- `README.md`;
- `VISAO-GERAL.md`;
- `CONVENCOES-CODIGO.md`;
- `GLOSSARIO.md`;
- `MATRIZ-RASTREABILIDADE.md`;
- `AUDITORIA-DOCUMENTAL-001.md`;
- ADR-004, ADR-005, ADR-006, ADR-008, ADR-009, ADR-010, ADR-013, ADR-014 e ADR-015.

---

## 4. Escopo

### 4.1 Incluído

- solution tradicional `.sln`;
- projetos C# vazios e compiláveis;
- referências entre camadas;
- projetos de testes vazios e compiláveis;
- frontend Angular standalone;
- Angular Material configurado;
- arquivos centrais de build e dependências;
- Dockerfiles multi-stage iniciais;
- Docker Compose suficiente para build e validação da estrutura;
- `.editorconfig`, `.gitignore` e `.dockerignore`;
- comandos oficiais de restore, build e test;
- documentação breve dos comandos de bootstrap.

### 4.2 Fora do escopo

- entidades e migrations;
- endpoints funcionais de domínio;
- login e emissão de JWT;
- configuração completa do YARP;
- integração com PostgreSQL ou RabbitMQ;
- Outbox, Inbox, retry ou consumers;
- telas funcionais;
- tema visual definitivo;
- pipeline de CI;
- cobertura efetiva de 80%, pois ainda não haverá código de produção relevante;
- deploy.

Esses itens pertencem aos SDDs posteriores e não podem ser antecipados para “completar” o setup.

---

## 5. Versões e ferramentas

### 5.1 Backend

| Item | Decisão |
|---|---|
| Target framework | `net10.0` |
| Linguagem | C# 14 fornecido pelo SDK; sem `LangVersion` experimental |
| SDK de referência | .NET SDK 10.0.302 |
| Runtime de referência | .NET/ASP.NET Core 10.0.10 |
| Build image | `mcr.microsoft.com/dotnet/sdk:10.0.302` |
| Runtime image | `mcr.microsoft.com/dotnet/aspnet:10.0.10` |
| Solution format | `.sln`, criado explicitamente com `--format sln` |
| Gerenciamento NuGet | Central Package Management |

O SDK 10.0.302 e o runtime 10.0.10 são as versões estáveis correntes na data desta especificação. `global.json` e as imagens fixam essas versões para que o setup seja reproduzível. Atualizações de segurança serão aplicadas deliberadamente, com rebuild e testes, e as versões e digests efetivamente usados serão registrados antes da entrega.

Não será adotado `.slnx` neste projeto. Embora seja o padrão de `dotnet new sln` no .NET 10, `.sln` foi escolhido por compatibilidade ampla com ferramentas e ambientes corporativos.

### 5.2 Frontend

| Item | Decisão proposta |
|---|---|
| Angular | 21 LTS |
| Angular CLI | mesma major do Angular |
| Angular Material | mesma major do Angular |
| Node.js | 24 LTS |
| TypeScript | faixa compatível com Angular 21, fixada pelo lockfile |
| RxJS | 7.x compatível com Angular 21 |
| Gerenciador | npm |
| Lockfile | `package-lock.json` obrigatório |
| Componentes | standalone |
| Estilos | SCSS |

Angular 21 foi escolhido em vez de Angular 22 porque já está em LTS e permanece oficialmente suportado, reduzindo risco de mudanças durante o desafio. Node 24 é LTS e compatível com Angular 21. O lockfile preserva versões transitivas exatas.

### 5.3 Infraestrutura futura

As versões de PostgreSQL e RabbitMQ serão fixadas no `SDD-11-DOCKER-COMPOSE-E-OBSERVABILIDADE.md`. Este setup pode reservar os nomes dos serviços no Compose, mas não deve antecipar topologia, credenciais ou health checks definitivos.

---

## 6. Estrutura do repositório

```text
/
|-- AGENTS.md
|-- Korp.Erp.sln
|-- global.json
|-- Directory.Build.props
|-- Directory.Packages.props
|-- .editorconfig
|-- .gitignore
|-- .dockerignore
|-- compose.yaml
|-- docs/
|-- src/
|   |-- Services/
|   |   |-- Identity/
|   |   |   |-- Korp.Identity.Api/
|   |   |   |-- Korp.Identity.Application/
|   |   |   |-- Korp.Identity.Domain/
|   |   |   `-- Korp.Identity.Infrastructure/
|   |   |-- Inventory/
|   |   |   |-- Korp.Inventory.Api/
|   |   |   |-- Korp.Inventory.Application/
|   |   |   |-- Korp.Inventory.Domain/
|   |   |   `-- Korp.Inventory.Infrastructure/
|   |   `-- Billing/
|   |       |-- Korp.Billing.Api/
|   |       |-- Korp.Billing.Application/
|   |       |-- Korp.Billing.Domain/
|   |       `-- Korp.Billing.Infrastructure/
|   |-- Gateway/
|   |   `-- Korp.Gateway.Api/
|   `-- Shared/
|       `-- Korp.Shared.Contracts/
|-- frontend/
|   `-- korp-erp-web/
`-- tests/
    |-- Identity/
    |   |-- Korp.Identity.UnitTests/
    |   `-- Korp.Identity.IntegrationTests/
    |-- Inventory/
    |   |-- Korp.Inventory.UnitTests/
    |   `-- Korp.Inventory.IntegrationTests/
    |-- Billing/
    |   |-- Korp.Billing.UnitTests/
    |   `-- Korp.Billing.IntegrationTests/
    |-- Gateway/
    |   `-- Korp.Gateway.IntegrationTests/
    `-- Architecture/
        `-- Korp.ArchitectureTests/
```

Não serão criados projetos genéricos `Common`, `Core`, `Utils` ou `BuildingBlocks`.

---

## 7. Tipos de projeto

| Sufixo | Template | Responsabilidade inicial |
|---|---|---|
| `.Api` de serviço | `dotnet new web` | Host Minimal API e composition root |
| `.Domain` | `dotnet new classlib` | Domínio independente |
| `.Application` | `dotnet new classlib` | Casos de uso e portas |
| `.Infrastructure` | `dotnet new classlib` | Adapters técnicos |
| `Shared.Contracts` | `dotnet new classlib` | Records de integração, inicialmente vazio |
| `Gateway.Api` | `dotnet new web` | Host YARP stateless |
| Unit tests | `dotnet new xunit` | Regras isoladas |
| Integration tests | `dotnet new xunit` | Fronteiras reais futuras |
| Architecture tests | `dotnet new xunit` | Dependências entre assemblies |

Os templates devem ser executados pelo SDK dentro do Docker. Arquivos de demonstração gerados pelos templates serão removidos quando não representarem comportamento aprovado.

---

## 8. Regras de referência

### 8.1 Por serviço

```text
Domain          -> nenhuma referência interna
Application     -> Domain
Infrastructure  -> Application + Domain
Api             -> Application + Infrastructure
```

`Api` não acessa o `DbContext` diretamente. A referência a Infrastructure existe para o composition root registrar adapters.

### 8.2 Contratos compartilhados

```text
Inventory.Infrastructure -> Shared.Contracts
Billing.Infrastructure   -> Shared.Contracts
```

Identity, Gateway, Domain e Application não referenciam `Shared.Contracts`. Se um consumer precisar chamar Application, Infrastructure converte primeiro a mensagem externa para um comando interno.

### 8.3 Entre serviços

É proibida qualquer referência de projeto entre Identity, Inventory e Billing. Comunicação futura ocorre somente por HTTP ou RabbitMQ conforme os ADRs.

### 8.4 Testes

- UnitTests podem referenciar Domain e Application do próprio serviço;
- IntegrationTests podem referenciar Api e Infrastructure do próprio serviço;
- Gateway.IntegrationTests referencia somente Gateway;
- ArchitectureTests pode inspecionar todos os assemblies, sem se tornar dependência deles;
- testes de um serviço não referenciam internals de outro para reutilizar fixtures.

---

## 9. Configuração central do .NET

### 9.1 `Directory.Build.props`

Deve aplicar aos projetos C#:

```text
TargetFramework = net10.0
Nullable = enable
ImplicitUsings = enable
TreatWarningsAsErrors = true
Deterministic = true
ContinuousIntegrationBuild = true quando executado no CI
AnalysisLevel = latest-recommended compatível com o SDK fixado
EnforceCodeStyleInBuild = true
```

Exceções de warning devem ser específicas, justificadas e aprovadas. Não haverá `NoWarn` amplo para fazer o primeiro build passar.

### 9.2 `Directory.Packages.props`

Deve habilitar `ManagePackageVersionsCentrally` e declarar cada versão NuGet uma única vez. Projetos usam `PackageReference` sem atributo `Version`.

Somente `nuget.org` será utilizado inicialmente. Uma nova fonte exige source mapping e decisão explícita.

Pacotes pertencentes a SDDs posteriores não serão adicionados antecipadamente. O setup instala apenas o necessário para os hosts, Gateway, OpenAPI e projetos de teste compilarem.

### 9.3 `global.json`

Deve fixar o SDK 10.0.302. Ausência de SDK compatível dentro da imagem deve falhar de forma clara; não será permitido selecionar preview.

---

## 10. Frontend inicial

O Angular será criado como aplicação standalone com:

- routing habilitado;
- SCSS;
- strict mode;
- sem SSR ou prerender nesta entrega;
- sem PWA;
- sem biblioteca de estado global;
- sem testes E2E adicionados neste setup;
- prefixo de selector `korp`;
- organização futura por feature, não por pastas globais de tipo.

Estrutura inicial:

```text
frontend/korp-erp-web/src/app/
|-- app.config.ts
|-- app.routes.ts
|-- app.ts
|-- core/
|-- layout/
`-- features/
    |-- auth/
    |-- products/
    `-- invoices/
```

As pastas podem conter apenas arquivos necessários ao bootstrap. Não criar services, guards, componentes ou models vazios para antecipar os SDDs funcionais.

Angular Material será instalado e seu tema-base será configurado, mas a identidade visual será especificada no SDD-09.

---

## 11. Docker-first e bootstrap

### 11.1 Princípio

O host necessita somente de Docker e Docker Compose. Nenhum comando oficial depende de `dotnet`, Node, npm, PostgreSQL ou RabbitMQ instalados localmente.

### 11.2 Imagens

Backend usa multi-stage:

```text
sdk:10.0 -> restore -> build -> publish
aspnet:10.0 -> runtime não-root
```

O build do frontend usa:

```text
node:24 -> npm ci -> build
```

O servidor estático definitivo, a imagem de runtime e a configuração de fallback SPA serão confirmados no SDD-09/SDD-11. Este setup comprova o build do frontend sem escolher antecipadamente o servidor de produção.

### 11.3 Criação dos projetos

A implementação do setup deve usar containers temporários ou serviços de tooling do Compose para executar os templates oficiais. A sequência conceitual é:

```text
1. criar Korp.Erp.sln com --format sln
2. criar class libraries e hosts
3. criar projetos de teste
4. adicionar projetos à solution
5. adicionar referências permitidas
6. criar workspace Angular
7. instalar Angular Material
8. restaurar dependências por lockfiles
9. executar build e testes vazios
10. construir imagens finais
```

Os comandos exatos serão registrados no relatório de implementação. Eles não devem gravar caches ou arquivos com proprietário incorreto no workspace.

### 11.4 Compose inicial

`compose.yaml` deve oferecer comandos ou profiles para:

- restaurar e compilar backend;
- executar testes backend;
- instalar com `npm ci` e compilar frontend;
- construir as imagens das aplicações.

PostgreSQL, RabbitMQ, migrators e topologia completa entram no SDD-11, salvo o mínimo estritamente necessário para validar uma imagem.

---

## 12. Arquivos de repositório

### `.gitignore`

Deve excluir, no mínimo:

- `bin/`, `obj/`, `TestResults/` e artefatos de cobertura;
- `node_modules/`, `dist/` e cache Angular;
- `.env` e arquivos locais de secrets;
- arquivos de IDE que não sejam configuração compartilhada aprovada;
- logs e volumes locais.

Não deve ignorar:

- migrations futuras;
- `package-lock.json`;
- `Directory.Packages.props`;
- documentação e configurações reproduzíveis.

### `.dockerignore`

Evita enviar documentação desnecessária ao estágio de build quando ela não fizer parte do contexto necessário, além de excluir Git, caches, resultados e secrets.

---

## 13. Requisitos de segurança do setup

- imagens finais executam como usuário não-root quando a imagem permitir;
- imagens finais não contêm SDK, source code desnecessário, lockfiles de secret ou ferramentas de diagnóstico não utilizadas;
- nenhuma credencial é embutida em Dockerfile, Compose ou repositório;
- nenhuma porta funcional é exposta além do necessário no setup;
- versões preview são proibidas;
- restore usa fontes declaradas;
- logs de build não imprimem variáveis sensíveis;
- frontend não recebe secret em build args.

---

## 14. Critérios de aceite

### CA-01 - Estrutura da solution

**Dado** o repositório após o setup,  
**quando** a solution for inspecionada,  
**então** todos os projetos previstos estarão presentes, com nomes em inglês e sem projeto genérico não aprovado.

### CA-02 - Regra de dependência

**Dado** qualquer projeto de serviço,  
**quando** suas referências forem verificadas,  
**então** elas obedecerão integralmente à seção 8 e nenhum serviço referenciará outro.

### CA-03 - Build do backend

**Dado** um host com Docker e Compose,  
**quando** o comando oficial de build for executado,  
**então** toda a solution compilará sem erros e sem warnings.

### CA-04 - Testes iniciais

**Dado** os projetos de teste criados,  
**quando** o comando oficial for executado,  
**então** todos serão descobertos e passarão, sem testes fictícios usados para inflar métricas.

### CA-05 - Build do frontend

**Dado** um host sem Node instalado,  
**quando** o comando Docker oficial executar `npm ci` e o build,  
**então** a aplicação Angular standalone será compilada com lockfile e strict mode.

### CA-06 - Imagens finais

**Dado** os Dockerfiles do backend e o build containerizado do frontend,  
**quando** as imagens forem construídas,  
**então** APIs usarão runtime ASP.NET sem SDK e o frontend produzirá artefatos estáticos sem exigir Node no host.

### CA-07 - Gerenciamento central

**Dado** os projetos .NET,  
**quando** os PackageReferences forem inspecionados,  
**então** versões explícitas estarão centralizadas em `Directory.Packages.props` e não duplicadas nos `.csproj`.

### CA-08 - Docker-first

**Dado** uma máquina com somente Docker e Compose,  
**quando** os comandos documentados forem seguidos,  
**então** restore, build e testes serão executáveis sem SDK local.

### CA-09 - Higiene do repositório

**Dado** o setup concluído,  
**quando** os arquivos forem auditados,  
**então** não existirão secrets, caches, binários, artefatos gerados ou arquivos de template sem função.

### CA-10 - Ausência de comportamento antecipado

**Dado** o escopo deste SDD,  
**quando** o código inicial for revisado,  
**então** não haverá entidade, regra de negócio, endpoint funcional, migration, consumer ou tela pertencente aos SDDs posteriores.

---

## 15. Testes e verificações previstos

| ID | Verificação | Tipo | Critérios |
|---|---|---|---|
| TST-SETUP-001 | Inspecionar lista de projetos da solution | Estrutural | CA-01 |
| TST-SETUP-002 | Validar ProjectReferences permitidas | Arquitetura | CA-02 |
| TST-SETUP-003 | Compilar solution em container Release | Build | CA-03, CA-07, CA-08 |
| TST-SETUP-004 | Executar descoberta e suíte inicial | Teste | CA-04 |
| TST-SETUP-005 | Executar `npm ci` e build Angular em container | Build | CA-05, CA-08 |
| TST-SETUP-006 | Construir imagens multi-stage | Container | CA-06 |
| TST-SETUP-007 | Inspecionar usuário, conteúdo e tamanho das imagens backend | Segurança | CA-06, CA-09 |
| TST-SETUP-008 | Varredura por secrets e artefatos ignoráveis | Segurança/higiene | CA-09 |
| TST-SETUP-009 | Revisar arquivos contra o escopo negativo | Auditoria | CA-10 |

O gate de 80% não é aplicável a projetos sem lógica nesta fase. Ele passa a ser obrigatório assim que código de produção manual for introduzido.

---

## 16. Plano de implementação proposto

Após aprovação deste SDD, o plano sujeito ao Gate B será:

1. criar arquivos centrais de configuração;
2. criar solution e projetos pelo SDK em container;
3. configurar referências e Central Package Management;
4. criar projetos de teste;
5. criar workspace Angular e Material;
6. criar Dockerfiles e Compose inicial;
7. executar restore, builds e testes;
8. remover resíduos de templates;
9. atualizar matriz com arquivos e evidências;
10. apresentar relatório do Gate C.

Nenhuma etapa implementará comportamento dos serviços.

---

## 17. Riscos e mitigação

| Risco | Impacto | Mitigação |
|---|---|---|
| Grande quantidade de projetos | Setup mais lento e navegação extensa | Estrutura já aprovada; não criar projetos adicionais |
| SDK e imagem divergentes | Build não reproduzível | `global.json`, validação da versão e registro do digest final |
| Angular ativo demais para o prazo | Mudanças e incompatibilidades | Angular 21 LTS em vez de Angular 22 ativo |
| Dependências adicionadas cedo | Acoplamento e restore maior | Cada SDD adiciona somente seus pacotes necessários |
| Arquivos criados com permissões inadequadas | Edição difícil no host | Validar usuário e ownership dos containers de tooling |
| Build Docker lento | Iteração prejudicada | Camadas de restore, caches BuildKit e contextos mínimos |
| Projeto vazio usado para esconder arquitetura artificial | Baixo valor técnico | Cada camada só recebe responsabilidades reais; sem classes placeholder |

---

## 18. Marcadores de qualidade

| Marcador | Exigência nesta fase |
|---|---|
| ESP | SDD aprovado antes da criação de projetos |
| RAS | IDs da matriz ligados aos critérios desta especificação |
| ARC | Referências obedecem Clean Architecture e isolamento entre serviços |
| DOM | Nenhum domínio é implementado antecipadamente |
| ERR | Build falha claramente diante de configuração inválida |
| SEG | Sem secrets, imagens não-root e dependências controladas |
| TST | Builds, testes iniciais e arquitetura verificáveis |
| INT | Docker é a fronteira real de execução do setup |
| OBS | Logs de build claros e sem dados sensíveis |
| DOC | Comandos e versões documentados |
| QA | Estrutura, imagens e resíduos de template auditados |

---

## 19. Condição de aprovação

A aprovação deste SDD confirma especificamente:

- Angular 21 LTS com Node 24 LTS e npm;
- solution `.sln` tradicional;
- estrutura de projetos e testes da seção 6;
- referências da seção 8;
- Central Package Management;
- criação e validação integralmente por Docker;
- ausência de implementação funcional nesta fase.

Depois da aprovação ainda será apresentado o plano de arquivos do Gate B antes de criar o código.
