# Plano de Implementação - SDD-01

> Status: Executado
> Data: 2026-08-17
> SDD: `SDD-01-SETUP-E-ARQUITETURA.md`
> Gate: B - Plano de implementação
> Aprovação e execução confirmadas pelo Gate C: 2026-08-17

---

## 1. Resultado esperado

Ao final desta implementação, o repositório terá uma solution .NET e um workspace Angular criados por ferramentas oficiais dentro do Docker. Backend e frontend deverão restaurar, compilar e executar suas verificações iniciais sem SDK instalado no host.

Nenhuma regra funcional será implementada.

---

## 2. Arquivos centrais a criar

| Arquivo | Responsabilidade |
|---|---|
| `Korp.Erp.sln` | Registrar projetos backend e testes no formato tradicional `.sln` |
| `global.json` | Fixar SDK 10.0.302 e proibir preview |
| `Directory.Build.props` | Nullable, warnings, análise e propriedades comuns de build |
| `Directory.Packages.props` | Centralizar versões NuGet aprovadas nesta fase |
| `.editorconfig` | Formatação e estilo compartilhados entre editores e build |
| `.gitignore` | Excluir outputs, caches, secrets e arquivos locais |
| `.dockerignore` | Reduzir contextos de build e impedir inclusão de resíduos |
| `compose.yaml` | Comandos Docker-first de restore, build e testes |

`AGENTS.md` e os documentos aprovados não serão recriados.

---

## 3. Projetos backend a criar

### Identity

- `src/Services/Identity/Korp.Identity.Api/Korp.Identity.Api.csproj`
- `src/Services/Identity/Korp.Identity.Application/Korp.Identity.Application.csproj`
- `src/Services/Identity/Korp.Identity.Domain/Korp.Identity.Domain.csproj`
- `src/Services/Identity/Korp.Identity.Infrastructure/Korp.Identity.Infrastructure.csproj`

### Inventory

- `src/Services/Inventory/Korp.Inventory.Api/Korp.Inventory.Api.csproj`
- `src/Services/Inventory/Korp.Inventory.Application/Korp.Inventory.Application.csproj`
- `src/Services/Inventory/Korp.Inventory.Domain/Korp.Inventory.Domain.csproj`
- `src/Services/Inventory/Korp.Inventory.Infrastructure/Korp.Inventory.Infrastructure.csproj`

### Billing

- `src/Services/Billing/Korp.Billing.Api/Korp.Billing.Api.csproj`
- `src/Services/Billing/Korp.Billing.Application/Korp.Billing.Application.csproj`
- `src/Services/Billing/Korp.Billing.Domain/Korp.Billing.Domain.csproj`
- `src/Services/Billing/Korp.Billing.Infrastructure/Korp.Billing.Infrastructure.csproj`

### Gateway e contratos

- `src/Gateway/Korp.Gateway.Api/Korp.Gateway.Api.csproj`
- `src/Shared/Korp.Shared.Contracts/Korp.Shared.Contracts.csproj`

As APIs serão hosts mínimos compiláveis. Class libraries permanecerão sem classes placeholder.

---

## 4. Arquivos dos hosts

Cada API criada pelo template terá somente os arquivos indispensáveis:

- `Program.cs`;
- `Properties/launchSettings.json` somente se tiver utilidade fora do Docker;
- `appsettings.json`;
- `appsettings.Development.json` apenas quando existir configuração real diferente.

Arquivos de exemplo, endpoints meteorológicos e classes geradas sem responsabilidade serão removidos.

Dockerfiles previstos:

- `src/Services/Identity/Korp.Identity.Api/Dockerfile`;
- `src/Services/Inventory/Korp.Inventory.Api/Dockerfile`;
- `src/Services/Billing/Korp.Billing.Api/Dockerfile`;
- `src/Gateway/Korp.Gateway.Api/Dockerfile`.

Os quatro usarão build multi-stage, runtime ASP.NET 10.0.10 e usuário não-root.

---

## 5. Projetos de teste

- `tests/Identity/Korp.Identity.UnitTests/Korp.Identity.UnitTests.csproj`
- `tests/Identity/Korp.Identity.IntegrationTests/Korp.Identity.IntegrationTests.csproj`
- `tests/Inventory/Korp.Inventory.UnitTests/Korp.Inventory.UnitTests.csproj`
- `tests/Inventory/Korp.Inventory.IntegrationTests/Korp.Inventory.IntegrationTests.csproj`
- `tests/Billing/Korp.Billing.UnitTests/Korp.Billing.UnitTests.csproj`
- `tests/Billing/Korp.Billing.IntegrationTests/Korp.Billing.IntegrationTests.csproj`
- `tests/Gateway/Korp.Gateway.IntegrationTests/Korp.Gateway.IntegrationTests.csproj`
- `tests/Architecture/Korp.ArchitectureTests/Korp.ArchitectureTests.csproj`

Testes demonstrativos como `UnitTest1` serão removidos. O projeto de arquitetura receberá somente os primeiros testes reais de referência entre assemblies; os demais projetos podem permanecer sem testes até existir comportamento correspondente.

---

## 6. Referências a configurar

Para Identity, Inventory e Billing:

```text
Application     -> Domain
Infrastructure  -> Application + Domain
Api             -> Application + Infrastructure
```

Adicionalmente:

```text
Inventory.Infrastructure -> Korp.Shared.Contracts
Billing.Infrastructure   -> Korp.Shared.Contracts
```

Testes:

```text
*.UnitTests        -> Domain + Application do próprio serviço
*.IntegrationTests -> Api + Infrastructure do próprio serviço
Gateway.Tests      -> Gateway.Api
ArchitectureTests  -> assemblies inspecionados
```

Não haverá referência direta entre serviços.

---

## 7. Frontend a criar

Workspace:

```text
frontend/korp-erp-web/
```

Arquivos principais gerados e preservados quando necessários:

- `angular.json`;
- `package.json`;
- `package-lock.json`;
- `tsconfig.json`;
- `tsconfig.app.json`;
- `tsconfig.spec.json`;
- `src/main.ts`;
- `src/index.html`;
- `src/styles.scss`;
- `src/app/app.ts`;
- `src/app/app.html`;
- `src/app/app.scss`;
- `src/app/app.config.ts`;
- `src/app/app.routes.ts`.

Configuração:

- Angular 21 standalone;
- routing;
- strict mode;
- SCSS;
- selector prefix `korp`;
- Angular Material 21;
- sem SSR, PWA ou biblioteca de estado.

Será criado um Dockerfile de desenvolvimento/build para executar `npm ci`, testes iniciais e build. A imagem estática de produção ficará para SDD posterior.

---

## 8. Dependências desta fase

### Backend

Somente pacotes necessários para:

- geração OpenAPI básica dos hosts;
- YARP no Gateway;
- execução dos projetos xUnit;
- Coverlet collector já aprovado para a infraestrutura de testes;
- testes de arquitetura escritos com reflexão nativa, sem biblioteca arquitetural adicional.

EF Core, Npgsql, ASP.NET Core Identity, RabbitMQ.Client e demais bibliotecas não serão adicionados até os SDDs que os utilizam.

### Frontend

Somente dependências geradas pelo Angular CLI e Angular Material. Nenhuma segunda biblioteca visual será instalada.

Todas as versões ficarão em lockfiles ou gerenciamento central.

---

## 9. Ordem das alterações

1. criar configurações centrais e arquivos de ignore;
2. criar solution `.sln` pelo container SDK;
3. criar os 14 projetos de produção;
4. criar os 8 projetos de teste;
5. configurar ProjectReferences;
6. registrar projetos na solution;
7. remover arquivos de demonstração;
8. criar workspace Angular e instalar Material;
9. criar Dockerfiles;
10. criar `compose.yaml` com comandos de tooling;
11. restaurar e compilar backend;
12. executar testes backend;
13. instalar e compilar frontend;
14. construir e inspecionar imagens;
15. executar testes de arquitetura;
16. atualizar matriz e documentação com evidências.

---

## 10. Validações

Serão executadas dentro do Docker:

- versão efetiva do SDK;
- restore da solution;
- build Release com warnings como erro;
- execução de todos os projetos de teste;
- validação automatizada de referências arquiteturais;
- `npm ci`;
- build Angular de produção;
- testes frontend gerados que tenham comportamento real;
- build das quatro imagens backend;
- inspeção de usuário não-root e ausência do SDK nas imagens finais;
- busca por secrets, resíduos de template, `bin`, `obj`, `node_modules` e artefatos indevidos;
- conferência de todos os projetos registrados na solution.

---

## 11. Riscos durante a execução

| Risco | Resposta |
|---|---|
| Template atual divergir dos arquivos previstos | Preservar intenção do SDD e registrar diferença não comportamental |
| Pacote produzir warning incompatível com gate | Investigar; não silenciar amplamente |
| Container criar arquivos com ownership inadequado | Ajustar usuário do tooling antes de continuar |
| Angular Material exigir ajuste de versão | Usar mesma major do Angular e lockfile |
| Imagem exata do .NET não estar disponível | Parar e apresentar versão oficial equivalente; não trocar silenciosamente |
| Testes vazios retornarem comportamento inesperado | Manter pelo menos ArchitectureTests real; não criar teste fictício |

---

## 12. Arquivos que poderão ser atualizados após validação

- `docs/MATRIZ-RASTREABILIDADE.md`, com arquivos, testes e evidências;
- `docs/README.md`, alterando o estado do SDD somente conforme o Gate C;
- `docs/SDD-01-SETUP-E-ARQUITETURA.md`, somente se surgir diferença aprovada;
- este plano, caso o escopo precise ser revisado antes da execução.

---

## 13. Condição para iniciar

A implementação começa somente após aprovação explícita deste plano. A aprovação autoriza criar os arquivos listados, instalar as dependências delimitadas e executar as validações descritas; não autoriza funcionalidades dos SDDs posteriores.
