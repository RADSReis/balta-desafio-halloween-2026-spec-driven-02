# Tarefas

| ID | Tarefa | Origem | Depende de | Concluída quando |
|---|---|---|---|---|
| T-01 | Criar projeto e estrutura de pastas | Constituição | | Projeto compila |
| T-02 | Modelar a entidade Link e a migração | RF-01, RF-08, RN-11, D-07 | T-01 | Migração aplica no banco |
| T-03 | Gerar código aleatório de 7 caracteres e validar código próprio | RF-02, RF-03, RN-07, RN-08, D-03 | T-01 | Teste unitário cobre formato e alfabeto |
| T-04 | Validar e normalizar URL de destino e expiração | RN-02, RN-03, RN-04, RN-10, RN-13, RF-06 | T-01 | Teste cobre http, https, hosts privados, 2048, fuso e inválidos |
| T-05 | Criar link com código automático | RF-01, RF-10, RN-09 | T-02, T-03, T-04 | Teste de integração cria e retorna 201 |
| T-06 | Aceitar código próprio com unicidade | RF-03, RN-01, D-04 | T-05 | Código duplicado retorna 400 |
| T-07 | Redirecionar por código | RF-04, RF-09, RN-05, RN-11 | T-05 | Teste de integração retorna 302 |
| T-08 | Incrementar contador de forma atômica | RF-05, RN-12, RNF-01, D-02 | T-07 | Teste concorrente não perde contagem |
| T-09 | Endpoint de estatísticas | RF-07, D-05 | T-05 | Teste de integração devolve o total, inclusive de link expirado |
| T-10 | Cache em memória dos links mais acessados | D-08 | T-07 | Teste unitário mostra que o segundo redirect não consulta o banco |
