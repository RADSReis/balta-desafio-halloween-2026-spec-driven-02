# Encurtador de links

## Problema
Links longos quebram em mensagens, e-mails e materiais impressos, e não dá para
medir quantas pessoas clicaram neles.

## Objetivo
Permitir que uma pessoa transforme uma URL longa em um link curto, compartilhe
esse link e acompanhe quantos acessos intencionais ele recebeu.

## Usuários
- Criador do link: gera links curtos e acompanha os acessos
- Visitante: clica no link curto e chega ao destino

## Histórias
- Como criador, quero encurtar uma URL para compartilhar um link menor
- Como criador, quero escolher o código do link para usar um nome legível
- Como criador, quero ver quantos acessos meu link recebeu
- Como visitante, quero ser redirecionado ao destino sem passar por tela intermediária

## Requisitos funcionais
- RF-01: o sistema recebe uma URL de destino e uma data/hora de expiração e
  devolve a URL curta completa, montada com o esquema e o host da própria
  requisição (ex.: `https://host-da-requisicao/ABC1234`)
- RF-02: o código do link tem 7 caracteres gerados automaticamente, usando apenas
  letras maiúsculas de A a Z e dígitos de 0 a 9
- RF-03: o criador pode informar um código próprio de 4 a 20 caracteres; o
  sistema remove espaços do início e do fim, converte para maiúsculas e só
  então valida
- RF-04: acessar um link curto válido redireciona para a URL de destino com
  redirecionamento temporário (302); redirecionamento permanente (301) é proibido
- RF-05: cada acesso intencional a um link válido incrementa o contador daquele
  link, incluindo acessos repetidos da mesma pessoa; acessos de robôs e de
  pré-visualizadores de link não são contados, mas continuam sendo redirecionados
- RF-06: o criador informa obrigatoriamente uma data e hora de expiração; se o
  valor vier sem fuso horário, o sistema assume o fuso padrão
  `America/Sao_Paulo`; o valor é convertido para UTC e a data UTC resultante é a
  data de expiração do link
- RF-07: o sistema devolve o total de acessos de um link a qualquer pessoa que
  conheça seu código, inclusive de links expirados
- RF-08: os links e seus dados devem ser persistidos em banco de dados para que
  não se percam após reinício da aplicação
- RF-09: o código informado no acesso é convertido para maiúsculas antes da
  busca, então `/abc1234` e `/ABC1234` levam ao mesmo link
- RF-10: se a URL de destino normalizada já tiver um link, o sistema não cria
  outro: substitui a expiração do link existente pela expiração da requisição,
  ignora um eventual código personalizado e devolve a mesma URL curta; isso vale
  também para link já expirado, que volta a ser válido

## Regras de negócio
- RN-01: o código do link é único em todo o sistema; o código de um link expirado
  não é liberado para reutilização
- RN-02: a URL de destino precisa usar http ou https
- RN-03: a URL de destino precisa ser pública: o host não pode ser `localhost`
  nem um endereço IP de loopback, de rede privada ou de link-local; a verificação
  é feita apenas sobre o texto da URL, sem resolver DNS nem fazer requisição
- RN-04: a URL de destino tem no máximo 2048 caracteres
- RN-05: link expirado não redireciona e não conta acesso
- RN-06: o contador de acessos nunca diminui
- RN-07: códigos automáticos e personalizados aceitam somente A-Z e 0-9 depois
  da normalização (trim e maiúsculas); espaços internos e caracteres especiais
  não são permitidos
- RN-08: o código personalizado tem de 4 a 20 caracteres depois do trim
- RN-09: se um código gerado automaticamente já estiver em uso, o sistema gera
  outro, com no máximo 5 tentativas
- RN-10: a data de expiração (UTC) não pode ser anterior à data UTC atual nem
  posterior à data UTC de 1 ano depois do momento da requisição (criação ou
  atualização)
- RN-11: o link é válido até 23:59:59.999 UTC da sua data de expiração; a partir
  de 00:00:00 UTC do dia seguinte ele está expirado
- RN-12: acesso feito com o método HEAD ou por um user-agent identificado como
  robô ou pré-visualizador de link não incrementa o contador
- RN-13: a URL de destino é normalizada antes de ser salva e comparada: remover
  espaços do início e do fim, esquema e host em minúsculas, remover a porta
  padrão (80 para http, 443 para https) e remover a `/` final quando o caminho
  for só `/`; caminho, query e fragmento são mantidos como vieram

## Requisitos não funcionais
- RNF-01: o incremento do contador é atômico, para que acessos simultâneos não
  percam contagem

## Respostas de erro
Todos os erros retornam ProblemDetails com mensagem em português.
- Erro de validação (URL, código, expiração, código já em uso): 400
- Código inexistente: 404
- Link expirado: 410
- Esgotadas as 5 tentativas de gerar código automático: 500

## Casos de borda
- Código personalizado já em uso: recusar com 400 e explicar o motivo
- Código gerado automaticamente já em uso: gerar outro, até 5 tentativas
- Código inexistente no acesso: responder 404
- Código em minúsculas no acesso: converter e redirecionar
- URL de destino com mais de 2048 caracteres: recusar com 400
- URL de destino com host local ou privado: recusar com 400
- Expiração ausente, no passado ou a mais de 1 ano: recusar com 400
- Expiração sem fuso horário: assumir `America/Sao_Paulo`
- URL de destino já encurtada (mesmo texto após normalização): substituir a
  expiração e devolver a mesma URL curta, mesmo que o link estivesse expirado

## Fora de escopo
- Cadastro e login de usuários
- Domínio personalizado
- Relatório com origem geográfica do acesso
- Edição do destino de um link já criado
- Exclusão de links
- Purge de links expirados (previsto para o futuro)

## Critérios de aceite
- Criar um link e acessá-lo redireciona com 302 para o destino informado
- A criação devolve a URL curta completa, com o esquema e o host da requisição
- Código automático tem 7 caracteres, só com A-Z e 0-9
- Código personalizado `  meu-link ` é recusado com 400 (hífen); `  meulink `
  é aceito e salvo como `MEULINK`
- Código personalizado com menos de 4 ou mais de 20 caracteres é recusado com 400
- Dois links não podem ter o mesmo código; a segunda criação recebe 400
- Colisão no código automático gera um novo código; após 5 colisões seguidas a
  criação falha com 500
- URL com esquema diferente de http/https é recusada com 400
- URL com mais de 2048 caracteres é recusada com 400
- URL com host `localhost`, `127.0.0.1` ou `192.168.0.10` é recusada com 400
- Expiração no passado ou a mais de 1 ano é recusada com 400
- Expiração informada como `2026-10-24T22:00:00` (sem fuso) é tratada como
  `America/Sao_Paulo`, vira `2026-10-25T01:00:00Z` e o link vale até
  `2026-10-25T23:59:59.999Z`
- Link funciona até 23:59:59.999 UTC da data de expiração e responde 410 a partir
  de 00:00:00 UTC do dia seguinte, sem incrementar o contador
- Código inexistente responde 404
- Acessar `/abc1234` redireciona para o mesmo destino de `/ABC1234`
- Encurtar `HTTPS://Exemplo.com:443/` depois de `https://exemplo.com` devolve a
  mesma URL curta, substitui a expiração e preserva o contador
- Encurtar de novo a URL de um link expirado reativa o link com a nova expiração
- Acesso por user-agent de robô ou pré-visualizador e requisição HEAD não
  incrementam o contador
- Acessos simultâneos ao mesmo link incrementam o contador sem perda
- Qualquer pessoa que conheça o código pode consultar o total de acessos,
  inclusive de link expirado
- Após reiniciar a aplicação, os links persistidos no banco continuam funcionando
