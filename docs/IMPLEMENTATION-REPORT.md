# Relatório de implementação — Baixaki 1.1.0

Data: 23 de outubro de 2026.

A atualização foi implementada no código local e compilada com o compilador C# do .NET Framework do Windows. O projeto continua em Windows Forms, sem NuGet, mantendo a paleta e os motores externos, com novo ícone solicitado pelo usuário, instalação Inno Setup e integração com releases de `TheOrionEXE/Baixaki`.

## Entrega

- `Baixaki.exe`: aplicativo v1.1.0; versão de arquivo e assembly `1.1.0.0`.
- `Output/BaixakiSetup.exe`: instalador v1.1.0, com o mesmo AppId e o mesmo nome esperado pelo atualizador.
- `README.md`: apresentação, instalação, uso e compilação para o GitHub.
- `GUIA-AVANCADO.md`: instruções detalhadas das ferramentas avançadas.
- Central de Ajuda integrada, por botão e F1, com 12 tópicos em português. O conteúdo está incorporado ao executável por `HelpTopics.json`; não exige conexão para leitura.

Este relatório registra a validação local da implementação 1.1.0, anterior à organização do repositório. Os executáveis citados são artefatos de compilação; sua disponibilidade para download depende da publicação em Releases.

## Arquitetura e comportamento

`DownloadState.cs` contém os modelos da fila, coleções, histórico e bloqueios de fonte, a persistência atômica e as verificações de argumentos e pastas de trabalho. `DownloadEngine.cs` integra metadados do yt-dlp, descoberta incremental, processamento sequencial, progresso, conversão e recuperação. `Baixaki.cs` reúne o fluxo universal da interface; `AppUpdater.cs` preserva a integração de atualizações. `HelpCenter.cs` apresenta a ajuda nativa.

A fila registra IDs de tarefas, URLs originais, identificadores de extrator e mídia, variantes de formato, destinos, caminhos de arquivos, estado, progresso, falhas, tentativas e datas. Itens interrompidos retornam pausados. O histórico considera a existência dos arquivos e distingue as variantes de saída. A conversão pode ser repetida a partir da fonte local mantida.

A classificação usa metadados estruturados e uma solicitação limitada a uma entrada, com prazo de 60 segundos. Não há uma lista de serviços na lógica da interface. A descoberta é uma operação separada, transmitindo entradas JSON conforme o motor as retorna, com checkpoints a cada 25 novas entradas e ao encerrar. Uma nova enumeração reconcilia IDs, em vez de prometer um cursor genérico.

O comportamento foi refinado conforme a preocupação do usuário com perfis: colar uma coleção abre o painel e preserva uma prévia quando disponível; não inicia a descoberta ampliada. O usuário revisa um aviso e inicia explicitamente Discover / reconcile. O limite inicial é 10 entradas; o lote padrão continua sendo 50 itens elegíveis, com continuação manual.

Os filtros utilizam somente metadados conhecidos. A presença de vídeo não é tomada como prova de presença de áudio. Outros tipos que o pipeline de vídeo/MP3 não suporta não são enfileirados como se fossem suportados.

## Proteções e recuperação

- Seletor exclusivo de três modos; Premiere independente para vídeo. A saída Video Only passa por remoção explícita de áudio quando necessário, inclusive na conversão Premiere.
- Argumentos escapados para execução direta, sem interpolação em shell.
- Processos e cancelamento pertencem à operação que os criou; os leitores são drenados antes do retorno. A retomada rápida não cria um segundo trabalhador concorrente.
- Temporários isolados por tarefa, sem busca de arquivos recentes no destino e sem limpeza por horário. Pastas de trabalho com junction/symlink são rejeitadas.
- Pausa preserva parciais. Cancelamento e remoção explicam o efeito e pedem confirmação; não apagam saídas finais de outras tarefas.
- Uma operação por vez na fila, espera padrão de 5 segundos e até duas novas tentativas para falhas identificadas como transitórias.
- Restrições claras de acesso/auth e limites interrompem novas solicitações à fonte afetada. Retry-After é considerado quando exposto nos diagnósticos. O prazo não pode ser limpo prematuramente.
- Cookies opcionais, desativados a cada sessão e precedidos de aviso sobre a conta. A aplicação não salva valores de cookies; diagnósticos sensíveis são ocultados e URLs com credenciais ou parâmetros comuns de autenticação são rejeitadas.
- Estado em `%APPDATA%/Baixaki/state.json`, com gravação em temporário, flush, substituição atômica e backup. A preferência antiga de pasta é aproveitada.

Essas proteções não garantem ausência de bloqueios ou restrições de conta. O limite de entradas não corresponde necessariamente ao número de requisições realizadas pelo extrator.

## Validação executada

| Grupo | Resultado | Escopo |
| --- | --- | --- |
| Lógica e persistência | 88 verificações aprovadas | Classificação, quatro identidades de extrator, 2.005 entradas por coleção, reconciliação, lotes, variantes, duplicatas, arquivos apagados, falhas, retomada, cancelamento, limpeza isolada, bloqueios, Retry-After, backup e versão |
| Interface e ajuda | 31 verificações aprovadas | Mesmo painel e fila para respostas simuladas Youtube, Instagram, TikTok e Generic; prévia sem enumeração automática; limite inicial 10; fechamento com checkpoint; 12 tópicos de ajuda e aviso de risco |
| Motores reais | 8 verificações aprovadas | Classificação Generic de MP4 local servido em loopback; vídeo+áudio, vídeo sem áudio e MP3; Premiere com e sem áudio; cancelamento real do FFmpeg e execução posterior sem interferência |
| Layout | Renderizações verificadas | Janelas de 1120×840, 800×650 e 1500×1050; escala geométrica de 150%; central de ajuda. A janela menor oferece rolagem para manter os controles acessíveis |
| Compilação | Aprovada | C# do Windows, executável com ícone e versão, Inno Setup 6.7.3, dependências e avisos de licença incluídos |

São **127 verificações funcionais aprovadas**, além das verificações de layout e compilação. As identidades de redes sociais são dados simulados; isso não equivale a testes de acesso real nesses serviços.

As verificações reais usaram yt-dlp `2026.10.21` e FFmpeg `9.0.2-full_build-www.gyan.dev`. Um servidor HTTP temporário em `127.0.0.1` forneceu mídia sintética de um segundo. A conexão de loopback inicialmente foi bloqueada pelo sandbox e passou ao ser executada com a permissão correspondente. Nenhum perfil real, cookie ou conta pessoal foi usado.

O teste do filtro de áudio foi refinado depois da integração local; a suíte final de lógica e interface foi executada novamente. Esse refinamento não altera os caminhos de transferência ou conversão verificados na integração.

Logs: `test-output/Tests-results.txt`, `test-output/UiWorkflowTests-results.txt`, `test-output/integration-results.txt` e `test-output/LayoutTests-results.txt`. Há PNGs de evidência de layout em `test-output`; não devem ser publicados sem revisar os caminhos pessoais visíveis.

## Limites da validação

Não foram executados login em navegadores reais, enumeração de perfis públicos/privados, downloads em serviços externos, atualização real do yt-dlp pela rede, instalação sobre a versão antiga, atualização efetiva pelo GitHub ou testes em múltiplos monitores com DPI diferente. A atualização do motor foi testada com um simulador do comando e de seu retorno; o instalador foi compilado e sua identidade/versionamento foram preservados, mas ele não foi instalado nesta máquina.

A escala de 150% foi exercitada por escala programática de controles; não é um teste completo de DPI por monitor. Totais de coleções podem permanecer desconhecidos. Uma descoberta longa tem limite de tempo de 15 minutos e conserva resultados parciais; pode ser necessário reenumerar com reconciliação. Metadados ausentes limitam os filtros. Entradas sem URL utilizável, coleções aninhadas não achatadas e outros tipos não suportados são preservados com status informativo.

O aplicativo não promete acesso universal a todos os recursos de todo serviço suportado pelo motor, nem retomada byte a byte em todo servidor. Antes de distribuir, faça um teste manual do instalador e um teste pequeno dos extratores que a equipe realmente utiliza.

## Reproduzir

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1 -Integration
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -Version 1.1.0 -NonInteractive -NoOpen
```

Neste ambiente, foi necessário informar `-IsccPath` com a instalação existente do Inno Setup, porque o sandbox redireciona LOCALAPPDATA. O fluxo padrão continua procurando as instalações usuais. A compilação não precisou baixar dependências.

A documentação principal do [yt-dlp](https://github.com/yt-dlp/yt-dlp#usage-and-options) e a ajuda do executável instalado foram consultadas para conferir as opções utilizadas.


## Interface revisada e perfis não suportados

A interface principal passou a usar português (Brasil), fonte de corpo de 11 pt, títulos/campos maiores, cartões escuros e botões com cantos arredondados. As opções avançadas e a atividade técnica são expansíveis. A abertura sem tarefas foi verificada em 1120×687 de área útil em 96 DPI, com conteúdo de 685 px e sem uma grande sobra no rodapé. O tamanho se adapta ao conteúdo e à área útil da tela; a janela continua redimensionável.

A fila oferece as categorias Todos, Em andamento, Aguardando, Concluídos, Com erro e Pausados/cancelados, cada uma com contador. Os seis filtros foram exercitados por cliques nos botões reais da interface. As barras mostram percentuais somente quando conhecidos, e o item ativo recebe destaque. Há ações para detalhes, arquivo e pasta; abrir arquivo exige uma saída concluída existente, com extensão de mídia permitida. Os testes verificam a habilitação dessa ação, sem lançar aplicativos externos.

O catálogo offline do yt-dlp instalado lista `instagram:user (CURRENTLY BROKEN)`. Isso explica por que links individuais e páginas de perfil não podem ser anunciados como capacidades equivalentes. Não foi feita uma enumeração real do perfil enviado pelo usuário. O tratamento genérico de URL não suportada/extrator quebrado agora é distinto de autenticação: não recomenda cookies como reparo e não enfileira automaticamente a página rejeitada. README e ajuda registram essa limitação.

O novo ícone magenta representa download e fila. O executável e o instalador usam `assets/baixaki.ico`, com dez resoluções de 16 a 256 px. As fontes estão em `assets/baixaki-icon.svg` e `tools/BuildIcon.cs`; `build-icon.ps1` regenera os artefatos sem bibliotecas externas. O ícone legado da raiz foi removido do repositório; os scripts usam assets/baixaki.ico.


O seletor de cookies e sua explicação permanecem agrupados em três tamanhos de janela. Atualizações sem mudanças não reescrevem as células da fila. Os checkboxes persistem entre filtros e controlam ações em lote; o cabeçalho alterna apenas os itens visíveis. O crédito Created by OrionEXE fica no extremo superior direito do cabeçalho principal do aplicativo.

## Integridade da compilação final revisada

- Baixaki.exe: 188928 bytes; versão 1.1.0.0.
- BaixakiSetup.exe: 79796854 bytes; versão 1.1.0                                             .
- SHA-256 de Baixaki.exe: E8391D98106308A49199CA0E4E6CAB605E89274DDB4C27C54DE146D2554D8B2F
- SHA-256 de BaixakiSetup.exe: D858E3113036408520615353E9D057E0FE71815B62C6DAA6FF4393BA5B068231
- Instalador final recompilado após posicionar o crédito no topo direito do programa.
