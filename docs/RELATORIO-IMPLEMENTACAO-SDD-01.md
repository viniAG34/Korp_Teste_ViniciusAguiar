# Relatório de Implementação do SDD-01

> Gate: C - Conclusão
> Status: Aprovado pelo engenheiro
> Data: 2026-08-17
> Especificação: `SDD-01-SETUP-E-ARQUITETURA.md`
> Plano aprovado: `PLANO-IMPLEMENTACAO-SDD-01.md`
> Aprovação do Gate C: 2026-08-17

---

## 1. Resultado

O setup técnico foi implementado sem antecipar regras de negócio. A solução contém os três serviços persistentes previstos, o Gateway, contratos de integração, projetos de teste, frontend Angular e ferramentas Docker-first.

Não foram criadas entidades, migrations, consumers RabbitMQ, persistência, autenticação funcional, rotas de negócio ou telas funcionais. Esses elementos permanecem sob responsabilidade dos SDDs posteriores.

---

## 2. Estrutura entregue

- solution tradicional `Korp.Erp.sln` com 22 projetos;
- Identity, Inventory e Billing divididos em Domain, Application, Infrastructure e Api;
- Gateway YARP stateless;
- `Korp.Shared.Contracts` inicialmente vazio;
- projetos UnitTests e IntegrationTests por serviço;
- IntegrationTests do Gateway;
- testes de arquitetura;
- Angular 21 standalone, strict, routing, SCSS e Angular Material 21;
- quatro Dockerfiles backend multi-stage;
- Dockerfile de build que entrega somente os artefatos estáticos do frontend;
- `compose.yaml` com perfis `tooling` e `applications`;
- gerenciamento central de versões NuGet.

---

## 3. Dependências e versões efetivas

| Componente | Versão fixada ou resolvida | Finalidade nesta fase |
|---|---:|---|
| .NET SDK | 10.0.302 | Templates, restore, build, testes e publish |
| ASP.NET Core Runtime | 10.0.10 | Imagens finais das APIs |
| Microsoft.AspNetCore.OpenApi | 10.0.10 | Geração OpenAPI básica dos serviços |
| Microsoft.OpenApi | 2.11.0 | Correção explícita de dependência transitiva vulnerável |
| Yarp.ReverseProxy | 2.3.0 | Host inicial do Gateway |
| xunit.v3 | 3.2.2 | Framework de testes .NET |
| xunit.runner.visualstudio | 3.1.5 | Descoberta via `dotnet test` e IDEs |
| Microsoft.NET.Test.Sdk | 18.8.1 | Integração com a plataforma de testes |
| coverlet.collector | 10.0.1 | Infraestrutura futura de cobertura |
| Node | 24 LTS | Build e testes Angular |
| Angular | 21.2.x | SPA standalone |
| Angular Material/CDK | 21.2.14 | Biblioteca visual única |

Durante o primeiro restore, a auditoria NuGet bloqueou `Microsoft.OpenApi 2.0.0` por vulnerabilidade de alta severidade. O alerta não foi suprimido. A linha compatível 2.x foi elevada explicitamente para 2.11.0, superior à versão corrigida mínima 2.7.5, e o restore subsequente passou.

---

## 4. Referências arquiteturais

As referências implementadas seguem:

```text
Application     -> Domain
Infrastructure  -> Application + Domain
Api             -> Application + Infrastructure
```

Exceções aprovadas:

```text
Inventory.Infrastructure -> Korp.Shared.Contracts
Billing.Infrastructure   -> Korp.Shared.Contracts
```

Identity, Gateway, Domain e Application não referenciam `Shared.Contracts`. Nenhum serviço referencia projetos internos de outro serviço. O Gateway não referencia nenhum serviço e não possui banco ou mensageria.

O teste `ProjectReferenceRulesTests` lê diretamente os arquivos `.csproj`, normaliza caminhos Windows/Linux e compara o conjunto real de referências ao conjunto permitido.

---

## 5. Comandos oficiais Docker-first

```text
docker compose --profile tooling run --rm backend-build
docker compose --profile tooling run --rm backend-test
docker compose --profile tooling run --rm frontend-build
docker compose --profile applications build
```

O host não precisa de .NET, Node ou npm instalados. Bancos, RabbitMQ e topologia de runtime não foram antecipados; serão introduzidos no SDD-11.

---

## 6. Evidências executadas

| ID | Evidência | Resultado |
|---|---|---|
| TST-SETUP-001 | Projetos registrados em `Korp.Erp.sln` | Aprovado: 14 projetos de produção e 8 de teste |
| TST-SETUP-002 | `ProjectReferenceRulesTests` | Aprovado: 4 de 4 testes |
| TST-SETUP-003 | `dotnet test Korp.Erp.sln --configuration Release` em SDK containerizado | Aprovado: build sem warnings/erros e testes arquiteturais passando |
| TST-SETUP-004 | Descoberta xUnit v3 via adaptador VSTest | Aprovado: projeto arquitetural descoberto; projetos funcionais permanecem vazios por ausência deliberada de comportamento |
| TST-SETUP-005 | `npm run build` em Node 24 | Aprovado: bundle de 215,13 kB bruto |
| TST-SETUP-005A | `npm test -- --watch=false` | Aprovado: 2 de 2 testes do shell Angular |
| TST-SETUP-006 | Build das cinco imagens | Aprovado |
| TST-SETUP-007 | Inspeção de usuário e tamanho | Aprovado: APIs usam UID 1654; artefato frontend não é executável |
| TST-SETUP-008 | Auditoria NuGet e npm | Aprovado: 0 vulnerabilidades npm; vulnerabilidade transitiva NuGet corrigida |
| TST-SETUP-009 | Revisão de escopo negativo | Aprovado: nenhum comportamento de SDD posterior implementado |

Imagens inspecionadas:

| Imagem | Usuário | Tamanho aproximado |
|---|---:|---:|
| `korp-erp-identity-api` | 1654 | 98,60 MB |
| `korp-erp-inventory-api` | 1654 | 98,60 MB |
| `korp-erp-billing-api` | 1654 | 98,60 MB |
| `korp-erp-gateway-api` | 1654 | 98,48 MB |
| `korp-erp-frontend-artifacts` | não executável | 79,75 kB |

---

## 7. Ocorrências e correções durante a validação

### 7.1 Dependência OpenAPI vulnerável

O restore falhou por `NU1903`, tratado pela atualização explícita descrita na seção 3. Não existe supressão de auditoria.

### 7.2 Descoberta do xUnit v3

O framework compilou inicialmente, mas os testes não eram descobertos pelo runner VSTest. Foi adicionado o adaptador oficial `xunit.runner.visualstudio` 3.1.5, mantendo compatibilidade com `dotnet test`, Visual Studio e VS Code.

### 7.3 Caminhos multiplataforma

O teste arquitetural identificou que referências gravadas com `\` não eram interpretadas como separadores no Linux. A normalização foi incorporada ao próprio teste, que agora passa em containers Linux sobre host Windows.

### 7.4 Contexto Docker do frontend

O contexto específico do frontend tentava enviar `node_modules` ao daemon. Foi criado `frontend/korp-erp-web/.dockerignore`; o contexto caiu para aproximadamente 322 kB e a imagem passou a construir normalmente.

---

## 8. Limitações conhecidas e escopo posterior

- os seis projetos de testes funcionais e o projeto de integração do Gateway ainda não contêm testes, pois não existe comportamento correspondente;
- cobertura de 80% ainda não é aplicável a assemblies sem lógica manual;
- o Gateway possui somente bootstrap YARP com topologia vazia;
- OpenAPI expõe apenas documentos vazios dos hosts;
- o frontend contém somente o shell técnico e o tema-base;
- não há servidor estático de produção para o frontend;
- não há PostgreSQL, RabbitMQ, health checks, observabilidade ou autenticação funcional.

Esses pontos são deliberados e correspondem aos limites do SDD-01, não a pendências ocultas desta implementação.

---

## 9. Avaliação dos marcadores

| Marcador | Estado | Evidência resumida |
|---|---|---|
| ESP | Atendido | SDD e plano aprovados antes do código |
| RAS | Atendido | Critérios ligados a testes e a este relatório |
| ARC | Atendido | 4 testes arquiteturais passando |
| DOM | Atendido | Nenhum domínio antecipado |
| ERR | Atendido | Builds falharam de forma explícita e causas foram corrigidas |
| SEG | Atendido | Sem credenciais; NuGet auditado; APIs não-root |
| TST | Atendido | Build completo, arquitetura e shell Angular verificados |
| INT | Atendido | Fluxo oficial executado em Docker |
| OBS | Atendido nesta fase | Logs técnicos de restore, build e teste disponíveis |
| DOC | Atendido | Índice, matriz e relatório atualizados |
| QA | Atendido | Templates, dependências, imagens e escopo revisados |

---

## 10. Recomendação para o Gate C

A implementação do SDD-01 está pronta para revisão do engenheiro. Após aprovação deste relatório, o SDD-01 pode mudar de `Implementado` para `Validado` e o próximo trabalho passa a ser a elaboração, revisão e aprovação do SDD-02, sem iniciar sua implementação antecipadamente.
