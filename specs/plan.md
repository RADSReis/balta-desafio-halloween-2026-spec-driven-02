# Plano técnico

## Contexto
Implementa a especificação do encurtador de links, respeitando a constituição
do projeto.

## Arquitetura
- API HTTP em Minimal API, um arquivo de endpoints por recurso
- Camada de aplicação com LinkService, dona das regras de negócio RN-01 a RN-13
- Camada de dados com EF Core e um repositório por agregado
- Endpoint só valida entrada, chama o serviço e traduz o resultado em resposta

## Decisões
- D-01: SQLite como armazenamento, porque o RF-08 exige persistência
  entre reinícios
- D-02: contador incrementado por UPDATE atômico no banco, e não lido e
  reescrito na aplicação, para atender a RNF-01 sob acesso concorrente
- D-03: código do link gerado com o alfabeto A-Z e 0-9 exigido pelo RF-02 e
  pela RN-07, sorteado com gerador criptográfico para não ser previsível
- D-04: unicidade do código garantida por índice único no banco, além da
  checagem no serviço, porque a checagem sozinha tem corrida
- D-05: estatísticas são públicas para quem conhece o código (RF-07); não há
  chave de gerenciamento
- D-06: documentação da API com Swagger, para explorar e testar os endpoints
  manualmente; o documento OpenAPI é gerado pelo pacote oficial
  Microsoft.AspNetCore.OpenApi e exibido pelo Swashbuckle.AspNetCore.SwaggerUI;
  ambos ficam habilitados apenas no ambiente de desenvolvimento
- D-07: dependências de persistência e de testes, todas oficiais da Microsoft:
  Microsoft.EntityFrameworkCore.Sqlite é o provedor do D-01;
  Microsoft.EntityFrameworkCore.Design e a ferramenta local dotnet-ef geram as
  migrações; Microsoft.AspNetCore.Mvc.Testing sobe a API em memória para os
  testes de integração exigidos pela constituição
- D-08: cache em memória (IMemoryCache, já incluso no ASP.NET Core) do código
  para o destino e a expiração, com limite de tamanho e expiração deslizante,
  para que os links mais acessados não consultem o banco no redirect; o
  contador continua sendo incrementado no banco (D-02) e a entrada é removida
  quando a expiração do link é substituída (RF-10)

## Modelo de dados
Link
- Id, Code (único), TargetUrl (normalizada, indexada), CreatedAtUtc,
  ExpiresAtUtc (obrigatório), AccessCount

## Contratos
- POST /api/links entrada: targetUrl, code opcional, expiresAt obrigatório
  (com ou sem fuso horário)
- POST /api/links saída: shortUrl, code, expiresAtUtc; 201 quando cria, 200
  quando reaproveita o link da mesma URL (RF-10)
- GET /{code} (e HEAD) redireciona com 302, ou 404 se não existe, ou 410 se
  expirou
- GET /api/links/{code}/stats devolve code e accessCount, ou 404 se não existe

## Riscos
- Concorrência na criação de código próprio, mitigada por D-04
- Duas criações simultâneas da mesma URL podem gerar dois links, porque a
  TargetUrl não tem índice único
- Crescimento da tabela de links sem política de limpeza, fora de escopo por ora
