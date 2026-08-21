# Plano de Implementação - SDD-04

> Gate: B - Plano de implementação
> Status: Aprovado pelo engenheiro
> Data: 2026-08-20
> SDD: `SDD-04-IDENTITY-SERVICE.md`
> Dependências: SDD-01 validado, SDD-02 validado e baseline contratual do SDD-03 aprovada

---

## 1. Objetivo

Implementar o único fluxo funcional do Identity Service: autenticar o administrador por e-mail e senha, aplicar lockout, emitir JWT HS256 estrito e responder pelo contrato HTTP aprovado. A atividade também completa a validação segura de configuração e o inicializador idempotente já iniciado no SDD-02.

Não serão criados cadastro de usuário, refresh token, recuperação de senha, revogação, sessão, papéis adicionais, mensageria ou administração de identidade.

## 2. Auditoria da baseline

- commit atual: `d8b0084`, com worktree limpo;
- `ApplicationUser`, `IdentityDbContext`, migration inicial e seed básico já existem;
- a migration e a repetição simples do seed já possuem duas provas PostgreSQL;
- o seed atual valida apenas presença de e-mail e senha, não toda a política antes de alterar estado;
- Identity Application e Domain continuam sem comportamento, como previsto;
- a API contém DTOs de login, política JSON e OpenAPI vazio, sem endpoint;
- não há emissão JWT, validação de opções, login, lockout configurado, Problem Details ou logging de autenticação;
- Identity UnitTests permanece vazio;
- nenhum outro serviço referencia o banco ou tipos internos do Identity.

Não existe código funcional legado a preservar. A implementação ampliará a infraestrutura existente sem alterar a migration inicial, salvo descoberta documentada de incompatibilidade real.

## 3. Arquitetura da implementação

```text
POST /api/v1/auth/login
  -> valida contrato na API
  -> LoginHandler na Application
  -> ICredentialAuthenticator
       -> UserManager + SignInManager na Infrastructure
  -> IAccessTokenIssuer
       -> JsonWebTokenHandler + TimeProvider na Infrastructure
  -> LoginResponse na API
```

### 3.1 Application

Application define o caso de uso e as portas necessárias, sem ASP.NET Core, EF Core, `HttpContext`, JWT concreto ou `ProblemDetails`.

- request próprio de login;
- resultado discriminado entre sucesso e credencial inválida;
- identidade autenticada mínima;
- representação do token emitido;
- `ICredentialAuthenticator`;
- `IAccessTokenIssuer`;
- `LoginHandler`.

Credencial inválida é resultado esperado. Falha técnica não será convertida silenciosamente em credencial inválida.

### 3.2 Infrastructure

Infrastructure integra ASP.NET Core Identity, PostgreSQL e assinatura JWT:

- busca normalizada por e-mail;
- `CheckPasswordSignInAsync` com lockout habilitado;
- trabalho de hash para usuário inexistente;
- papéis ordenados ordinalmente;
- emissão HS256 com relógio injetável;
- validação antecipada da configuração JWT;
- configuração explícita de senha e lockout;
- tradução de indisponibilidade conhecida para exceção sanitizada da Application;
- validação completa do seed antes de qualquer alteração.

O adaptador não criará repositório genérico sobre `UserManager`.

### 3.3 API

A API implementa somente:

```text
POST /api/v1/auth/login
```

Responsabilidades:

- validação de e-mail e senha na fronteira;
- endpoint Minimal API organizado pela feature Auth;
- mapeamento de resultado para `200` ou `401`;
- `ProblemDetails` para `400`, `401`, `503` e `500`;
- correlação e sanitização aplicáveis;
- metadados OpenAPI completos;
- composição de dependências e validação no startup;
- nenhuma execução de migration ou seed no startup.

## 4. Dependências solicitadas

### Produção

`Microsoft.IdentityModel.JsonWebTokens` 8.22.0 será adicionado à Infrastructure. É o pacote oficial Microsoft para `JsonWebTokenHandler`, necessário para criar e validar JWS sem introduzir biblioteca sobreposta. A versão foi confirmada no catálogo oficial e suporta `CreateToken(SecurityTokenDescriptor)`.

### Testes

`Microsoft.AspNetCore.Mvc.Testing` 10.0.11 será adicionado somente a `Korp.Identity.IntegrationTests` para hospedar a API real em memória, mantendo PostgreSQL externo real. Não será usado mock de banco para substituir as provas do Identity.

Não serão adicionados MediatR, FluentValidation, biblioteca de mocks ou biblioteca JWT alternativa.

## 5. Arquivos previstos

### Identity Application

```text
src/Services/Identity/Korp.Identity.Application/Authentication/
|- LoginCommand.cs
|- LoginHandler.cs
|- LoginResult.cs
|- LoginStatus.cs
|- AuthenticatedIdentity.cs
|- AccessToken.cs
|- ICredentialAuthenticator.cs
|- IAccessTokenIssuer.cs
|- IdentityServiceUnavailableException.cs
```

### Identity Infrastructure

```text
src/Services/Identity/Korp.Identity.Infrastructure/Authentication/
|- IdentityCredentialAuthenticator.cs

src/Services/Identity/Korp.Identity.Infrastructure/Tokens/
|- JwtOptions.cs
|- JwtOptionsValidator.cs
|- JwtTokenIssuer.cs
|- JwtClaimNames.cs

src/Services/Identity/Korp.Identity.Infrastructure/Persistence/
|- IdentityDatabaseInitializer.cs          (alterado)
|- IdentitySeedOptions.cs                  (alterado)
|- IdentitySeedOptionsValidator.cs         (novo)

src/Services/Identity/Korp.Identity.Infrastructure/
|- DependencyInjection.cs
```

### Identity API

```text
src/Services/Identity/Korp.Identity.Api/Features/Auth/
|- LoginEndpoint.cs
|- LoginRequestValidator.cs

src/Services/Identity/Korp.Identity.Api/Errors/
|- ApiProblemDetails.cs
|- IdentityExceptionHandler.cs

src/Services/Identity/Korp.Identity.Api/
|- Program.cs                               (alterado)
|- appsettings.json                         (somente estrutura não secreta)
```

`.env.example` será alterado apenas se já existir. Caso não exista, sua criação fica no SDD-11, que define o contrato operacional completo dos secrets.

### Testes

```text
tests/Identity/Korp.Identity.UnitTests/Authentication/
|- LoginHandlerTests.cs

tests/Identity/Korp.Identity.IntegrationTests/Authentication/
|- LoginEndpointTests.cs
|- IdentityAuthenticationTests.cs
|- JwtTokenTests.cs
|- IdentityConfigurationTests.cs

tests/Identity/Korp.Identity.IntegrationTests/Persistence/
|- IdentityPersistenceTests.cs             (ampliado)

tests/Architecture/Korp.ArchitectureTests/
|- ProjectReferenceRulesTests.cs           (ampliado)
```

## 6. Mapeamento critério → implementação → teste

| Critério | Implementação | Provas principais |
|---|---|---|
| CA-ID-01 | Camadas e ownership já existentes, sem modelo Domain artificial | TST-ID-001, TST-ID-002 |
| CA-ID-02 | Initializer e validação antecipada do seed | TST-ID-003, TST-ID-004 |
| CA-ID-03 | Validadores de seed/JWT e options `ValidateOnStart` | TST-ID-005, TST-ID-006, TST-ID-013 |
| CA-ID-04 | LoginHandler, adapter Identity, issuer e endpoint | TST-ID-007 |
| CA-ID-05 | Resultado uniforme e hash para e-mail inexistente | TST-ID-008, TST-ID-009 |
| CA-ID-06 | IdentityOptions e SignInManager com lockout | TST-ID-010, TST-ID-011 |
| CA-ID-07 | JwtTokenIssuer, HS256, claims e TimeProvider | TST-ID-012, TST-ID-013 |
| CA-ID-08 | Matriz de validação do token emitido | TST-ID-014 a TST-ID-016; aplicação em APIs acumula nos SDDs 05, 06 e 08 |
| CA-ID-09 | Requisitos canônicos documentados e token com claims mínimas | TST-ID-016 e TST-ID-017; policies de APIs nos SDDs 05, 06 e 08 |
| CA-ID-10 | Sentinela de ausência de dependência dos serviços | TST-ID-018; validação HTTP independente nos SDDs 05, 06 e 08 |
| CA-ID-11 | Validator, endpoint e exception handler | TST-ID-017, TST-ID-019, TST-ID-023 |
| CA-ID-12 | Respostas, logs capturados e varredura de sentinelas | TST-ID-020 |
| CA-ID-13 | Endpoint metadata e documento gerado | TST-ID-021 |
| CA-ID-14 | Única rota funcional e inspeção negativa de tipos/rotas | TST-ID-022 |

CA-ID-08 a CA-ID-10 terão evidência própria do Identity nesta fase, mas sua conclusão distribuída será cumulativa com Gateway, Inventory e Billing. O relatório não declarará defesa em profundidade completa antes desses SDDs.

## 7. Cenários de teste

### Unitários

- handler não emite token para credencial inválida;
- handler emite somente depois da autenticação válida;
- papéis e dados são mapeados sem tipos HTTP;
- cancelamento é propagado;
- options JWT rejeitam Base64 inválido, chave curta, issuer e audience vazios;
- emissão usa relógio controlado, duração de 900 segundos e claims exatas.

### Integração PostgreSQL e Identity

- migration em banco vazio;
- seed repetido sem duplicação;
- senha existente preservada;
- configuração de seed inválida falha antes de criar papel ou usuário;
- login válido;
- senha errada, e-mail inexistente e lockout retornam resposta idêntica;
- cinco falhas bloqueiam, período expirado permite autenticação;
- sucesso limpa contador;
- banco indisponível produz `503`, não `401` ou `500`;
- cancellation não vira falha inesperada.

### JWT e segurança

- HS256 e somente claims aprovadas;
- `sub`, `jti`, `iat`, `nbf`, `exp`, issuer, audience e roles corretos;
- assinatura, algoritmo, issuer, audience, expiração e `nbf` inválidos rejeitados;
- claims mínimas ausentes rejeitadas pela avaliação correspondente;
- respostas, logs e OpenAPI não contêm senha, hash, token de teste, signing key ou connection string.

### HTTP/OpenAPI

- request inválido retorna `400 validation_failed`;
- credencial inválida retorna `401 invalid_credentials` sem `WWW-Authenticate` bearer;
- sucesso retorna contrato e expiração;
- indisponibilidade retorna `503 identity_unavailable`;
- OpenAPI contém somente login funcional, anônimo, com respostas aprovadas;
- rotas de cadastro, refresh, revogação e gestão não existem.

## 8. Configuração

As chaves obrigatórias permanecem:

```text
ConnectionStrings__IdentityDatabase
IdentitySeed__Email
IdentitySeed__Password
Jwt__SigningKey
Jwt__Issuer
Jwt__Audience
```

JWT e connection string usados pela API não terão defaults. O seed não será executado pela API. Testes fornecem valores exclusivos no próprio ambiente de teste e nunca reutilizam segredo demonstrativo de runtime.

## 9. Ordem de implementação

1. adicionar e fixar as duas dependências aprovadas;
2. criar tipos e testes unitários da Application;
3. implementar validação JWT e emissor com relógio controlável;
4. completar configuração Identity, autenticação e lockout;
5. endurecer validação e idempotência do seed;
6. implementar validação HTTP, endpoint e Problem Details;
7. gerar e inspecionar OpenAPI;
8. executar matriz de integração PostgreSQL, JWT e segurança;
9. executar regressão integral e cobertura Docker-first;
10. atualizar matriz, índice e relatório Gate C.

## 10. Riscos e contenções

| Risco | Contenção |
|---|---|
| Lockout tornar testes lentos | Controlar `LockoutEnd` pela API oficial do Identity; não reduzir a política de produção |
| E-mail inexistente ter caminho muito mais rápido | Executar o password hasher mesmo sem usuário |
| Exceção de infraestrutura virar credencial inválida | Resultado inválido e indisponibilidade permanecem tipos distintos |
| Startup usar segredo default | Validação obrigatória e ausência de fallback |
| Teste registrar token ou senha sentinela | Valores permanecem em memória e varredura examina respostas/logs |
| API executar migration/seed | Composition root registra serviços, mas não chama `Migrate`, `EnsureCreated` ou initializer |
| JWT aceitar algoritmo do header | Validação restringe explicitamente `HS256` |
| Expandir gestão de usuário | Sentinelas negativas de rotas e tipos excluídos |
| Declarar defesa em profundidade antes dos serviços | CA-ID-08 a CA-ID-10 permanecem cumulativos na matriz |

## 11. Validações do Gate C

- build Release sem erro e sem novo warning;
- todos os 23 testes planejados possuem prova ou deferimento cumulativo explícito;
- PostgreSQL real comprova Identity e lockout;
- OpenAPI gerado corresponde ao único endpoint;
- startup falha com configuração JWT ou banco ausente;
- nenhum segredo aparece em arquivo, resposta, log ou artefato;
- cobertura mínima de 80% por assembly manual aplicável;
- branch coverage publicada;
- nenhuma funcionalidade excluída foi introduzida;
- regressão dos 78 testes atuais permanece aprovada.

## 12. Decisões solicitadas

Aprovar ou ajustar:

1. `Microsoft.IdentityModel.JsonWebTokens` 8.22.0 como única dependência JWT de produção;
2. `Microsoft.AspNetCore.Mvc.Testing` 10.0.11 apenas para integração HTTP;
3. login com Application ports e adapters Identity na Infrastructure;
4. seed validado integralmente antes de alterar estado;
5. CA-ID-08 a CA-ID-10 como critérios cumulativos, concluídos após SDDs 05, 06 e 08.

Plano aprovado pelo engenheiro antes do início da implementação funcional.
