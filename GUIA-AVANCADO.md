# Guia avançado do Baixaki 1.1.0

Este conteúdo também está disponível na Central de ajuda (F1). Os nomes dos controles correspondem à interface em português.

## 1. Começar e reconhecer URLs

> Atenção: A análise automática também consulta a fonte. Os limites pedidos ao yt-dlp dependem do comportamento do extrator.

Cole uma URL HTTP(S) de uma página de mídia ou coleção. A análise usa metadados do yt-dlp e solicita uma prévia de até uma entrada, com tempo limitado. Alguns extratores precisam consultar mais de uma página para identificar o recurso.

Detectar automaticamente escolhe entre mídia individual e coleção. Se a análise falhar ou o resultado for ambíguo, use Um vídeo / áudio ou Collection manualmente. A seleção manual não concede acesso a conteúdo indisponível.

Uma coleção abre o mesmo Coleção de mídia, qualquer que seja o serviço. A enumeração ampliada não começa ao colar a URL: revise as opções e use Buscar itens. O botão apresenta um aviso antes das solicitações adicionais.

Para um item individual, escolha o modo e a pasta, clique em Adicionar à fila e depois em Iniciar downloads / Retomar fila. Novas URLs podem ser adicionadas enquanto a fila está em processamento.

A interface usa cartões para agrupar opções, rótulos em português e mensagens de próxima ação. Opções avançadas e atividade técnica podem ser expandidas quando precisar. O tamanho inicial acompanha o conteúdo, dentro do espaço disponível na tela.

## 2. Perfis: riscos e primeiro teste

> Atenção: Enumerar perfis pode causar limites de acesso e restrições na conta. Cookies associam solicitações à conta autenticada. Nenhum ritmo de acesso garante ausência de bloqueios.

Use somente páginas às quais você pode acessar. Antes de enumerar um perfil, prefira um teste pequeno: mantenha Buscar até em 10, deixe Cookies do navegador em Não usar se não precisar de login e use a espera padrão de 5 segundos ou um intervalo maior.

Clique em Buscar itens somente após ler o aviso. Revise o resultado e enfileire um lote pequeno. Confirme os arquivos antes de ampliar o limite. Não use Todos os disponíveis como primeiro teste de um perfil.

O limite de entradas não é um orçamento exato de requisições. Um extrator pode visitar páginas adicionais, retornar dados incompletos, não fornecer um total ou exigir autenticação. Um post também pode resultar em várias entradas de mídia.

Ao aparecer um limite de acesso, login obrigatório ou outro desafio claro, o programa suspende novas solicitações à fonte. Não libere repetidamente a fonte nem insista com novas tentativas sem resolver a causa. Pare e verifique a situação pelo acesso normal ao serviço.

O programa não contorna CAPTCHA, não gira proxies e não promete evitar detecção ou restrições.

Limitação confirmada do motor distribuído: instagram:user aparece como CURRENTLY BROKEN no yt-dlp 2026.10.21. Uma página como a aba /usuario/reels/ não tem suporte garantido. Links individuais e páginas de perfil são capacidades diferentes. Seleção manual ou cookies não consertam um extrator ausente ou quebrado. Não repita consultas ao perfil esperando que a autenticação corrija essa limitação.

## 3. Descoberta e filtros de coleções

> Atenção: Todos os disponíveis amplia a enumeração e pode gerar muitas solicitações. Descoberta e download são operações distintas.

O painel mostra a fonte e o título retornados pelo extrator. Use a lista de coleções para restaurar trabalhos anteriores.

Buscar até limita a enumeração solicitada: 10, 50, 100, Todos os disponíveis ou Custom. O limite inicial é 10. Buscar itens começa uma enumeração explícita e tenta preservar entradas durante o processo. Parar busca interrompe a descoberta sem apagar o que já foi salvo.

Toda mídia acessível encaminha os tipos acessíveis para a tentativa normal de vídeo/áudio. Vídeos com tipo identificado e Com áudio confirmado usam somente informações conhecidas; entradas sem metadados suficientes não são classificadas por suposição. Outros tipos que o pipeline de vídeo/MP3 não atende são indicados como não suportados.

Criar subpasta da coleção cria uma subpasta identificada pela coleção. O modo, a conversão e a pasta principais são copiados para os novos itens quando você enfileira o lote. Alterá-los não modifica tarefas já criadas.

Uma descoberta parcial não prova que todas as entradas foram encontradas. Repetir a descoberta reconcilia IDs conhecidos; não equivale a retomar um cursor de paginação universal.

## 4. Smart Batch: trabalhar com perfis grandes

> Atenção: Um lote de 50 não limita a descoberta a 50 requisições. O limite da descoberta e o tamanho do lote controlam processos diferentes.

Itens por lote define quantas entradas elegíveis serão adicionadas à fila. O padrão é 50, com um download simultâneo. Para um teste inicial de perfil, reduza também o lote, por exemplo para 10.

Adicionar lote à fila transforma entradas descobertas em tarefas. Iniciar downloads / Retomar fila começa a fila. O próximo lote exige uma nova ação manual quando não houver itens pendentes ou pausados do lote atual. Falhas permanecem disponíveis para Tentar novamente.

Você pode descobrir 100 entradas e baixar lotes de 10 ou 50, sem reenumerar a coleção a cada lote. Não é necessário escolher Todos os disponíveis para usar lotes.

Exemplo conservador: descubra 10, baixe 10, confira os arquivos e o comportamento da fonte. Depois, se adequado, aumente o limite para 50 e execute Buscar itens novamente. IDs já vistos são reconciliados.

Coleções com milhares de entradas dependem do extrator. A lógica de persistência e reconciliação foi testada com 2.005 entradas simuladas; isso não garante a extração de milhares de itens de qualquer perfil real.

## 5. Pausar, cancelar, remover e tentar novamente

> Atenção: Cancelar ou remover pode apagar arquivos temporários e exigir nova transferência desses dados. Pausar é a opção indicada para continuar depois.

Pausar interrompe o processo ativo e conserva parciais. Iniciar downloads / Retomar fila retoma a fila; a retomada dos bytes depende do servidor e do formato. A fila abre parada depois de um reinício.

Cancelar itens cancela itens ativos ou pendentes selecionados. Os parciais da tarefa são limpos; uma fonte já concluída é preservada para recuperação de conversão. Uma confirmação explica o efeito antes da operação.

Remover da fila remove tarefas da fila e limpa seus temporários. Uma tarefa ativa é cancelada antes da remoção. Arquivos finais e histórico não são apagados por esse botão.

Tentar novamente recoloca uma falha, item pausado/cancelado ou bloqueado em espera para nova tentativa, desde que não esteja ativo. Uma fonte bloqueada precisa ser resolvida e liberada antes de voltar a receber requisições.

Limpar concluídos remove tarefas concluídas da lista, preservando o histórico e os arquivos. Essa ação não libera espaço dos vídeos já baixados.

A fila tem filtros com contadores: Todos, Em andamento (downloads e conversões), Aguardando, Concluídos, Com erro e Pausados/cancelados. Eles mudam apenas a visualização; não removem nem interrompem tarefas. As barras mostram a porcentagem real quando há um total conhecido. Sem total significa que o motor ainda não informou o tamanho completo. Use Ver detalhes para o erro completo, Abrir arquivo para reproduzir uma saída concluída ou Abrir pasta para localizá-la. Se o arquivo foi apagado, a linha avisa que ele está ausente e permite tentar novamente.

## 6. Modos e conversão para Premiere

> Atenção: Converter exige CPU, espaço adicional e tempo. O programa mantém a fonte para recuperar falhas; confira o destino e o espaço disponível.

Vídeo + áudio busca vídeo e áudio. Vídeo sem áudio produz um arquivo sem faixa de áudio; se a fonte vier com áudio junto, o FFmpeg remove essa faixa. Áudio MP3 extrai e converte o áudio.

Preparar para Premiere é uma opção independente para os modos de vídeo. Ela gera H.264 em MP4, pixels yuv420p e, quando aplicável, áudio AAC. O modo Video Only continua sem áudio após a conversão.

No modo MP3 a conversão Premiere fica desabilitada. Os formatos são tratados como variantes distintas no histórico.

Se o download terminar e a conversão falhar, use Tentar novamente: a fonte local ainda existente é reutilizada. Se ela foi apagada ou movida, poderá ser necessário baixar novamente. Os temporários e fontes de recuperação ficam dentro de .baixaki, na pasta de destino.

Um arquivo H.264 compatível não garante que todas as versões do Premiere aceitarão toda combinação de mídia ou configuração. Confira o resultado no seu editor.

## 7. Smart Download e prevenção de duplicatas

> Atenção: Aumentar o intervalo pode reduzir a frequência de acesso, mas não garante permissão nem evita restrições do serviço.

Intervalo (segundos) controla a espera entre itens e o intervalo solicitado ao yt-dlp entre requisições de extração. O padrão é 5 segundos; a interface aceita de 1 a 120. Um download pode envolver outras transferências internas do motor.

Há um download simultâneo. Para falhas reconhecidas como transitórias, o programa permite até duas novas tentativas com espera conservadora. Erros permanentes não recebem esse ciclo automático.

Ignorar arquivos já baixados evita duplicatas na fila e em resultados já concluídos quando consegue identificar o item e a variante. O arquivo de saída precisa continuar existindo. Identificadores estáveis do extrator são usados quando disponíveis; a URL serve de alternativa.

O arquivo de downloads usado pelo yt-dlp é preparado por tarefa e variante a partir de saídas conhecidas ainda existentes. MP3, vídeo sem áudio e conversões não são tratados como o mesmo resultado.

Se apagar uma saída, uma nova inclusão pode baixá-la novamente. Se mover o arquivo sem atualizar o estado, o programa pode considerá-lo ausente. Não apague ou edite o estado manualmente enquanto a aplicação estiver aberta.

## 8. Cookies do navegador e privacidade

> Atenção: Cookies podem dar acesso à sua sessão. As requisições feitas com eles podem afetar sua conta. Use apenas um perfil seu e somente quando necessário.

Cookies do navegador é opcional e começa em Não usar em cada sessão. Ao escolher um navegador, um aviso explica o acesso antes de ativá-lo.

O yt-dlp tentará ler os cookies do navegador escolhido usando seu mecanismo de suporte. Isso depende do navegador, do perfil padrão, das proteções do Windows e da versão do motor. O Baixaki não oferece campos para senhas, tokens, arquivos de cookies ou seleção de perfis personalizados.

Os valores dos cookies não são copiados para a fila, para o histórico ou para os registros do aplicativo. A escolha de navegador também não é persistida entre sessões. Diagnósticos sensíveis são ocultados.

Prefira Não usar para conteúdo público quando funcionar. Uma sessão autenticada não garante acesso nem permissão para enumerar um perfil. Não cole URLs que contenham senhas ou tokens; o programa rejeita credenciais embutidas e parâmetros comuns de autenticação.

Ao compartilhar um erro, revise também nomes de arquivos, títulos, pastas e capturas de tela para evitar divulgar informações privadas.

## 9. Limites de acesso e fontes bloqueadas

> Atenção: Revisar fontes em pausa não resolve um bloqueio no serviço. Liberar repetidamente e tentar de novo pode agravar a restrição.

Um erro claro de limite, autenticação ou desafio de acesso coloca a fonte em espera. Os novos itens da mesma fonte permanecem sem processamento; trabalhos de outras fontes podem continuar.

Quando o yt-dlp expõe Retry-After nos diagnósticos, o prazo é registrado e não pode ser removido antes do vencimento. O aplicativo não inspeciona cabeçalhos que o motor não disponibiliza.

Sem um prazo utilizável, a espera exige revisão manual. Verifique a causa pelo acesso normal ao serviço. Espere quando houver limite; use cookies somente quando o acesso autenticado for apropriado e permitido. CAPTCHA e outras verificações devem ser resolvidos pelos meios normais do serviço.

Depois de resolver a causa, Revisar fontes em pausa pede confirmação. Use Tentar novamente nos itens bloqueados e depois Iniciar downloads / Retomar fila. Não há tentativas de contornar controles de acesso.

Fontes são agrupadas pelo domínio da página, com associação à coleção original. Isso é uma proteção conservadora; o aplicativo não conhece necessariamente o escopo exato de uma restrição de conta ou de IP.

## 10. Estado, histórico e cópia de segurança

> Atenção: Apagar state.json ou a pasta .baixaki pode perder a fila ou impedir a retomada. Feche o programa antes de fazer manutenção manual.

As preferências, tarefas, coleções e histórico ficam em %APPDATA%\Baixaki\state.json. As gravações substituem o arquivo de forma atômica e mantêm state.json.bak. Se a leitura falhar, o programa tenta a cópia e avisa no registro.

Para uma cópia de segurança, feche o Baixaki e copie a pasta %APPDATA%\Baixaki. Se quiser manter a recuperação de transferências e conversões, preserve também as pastas de destino e suas subpastas .baixaki.

Itens interrompidos voltam pausados. A descoberta interrompida volta como parcial. A fila não começa a fazer downloads automaticamente ao abrir o aplicativo.

A opção de destino da versão anterior em folder.txt é migrada quando ainda não existe um novo estado utilizável. Alterar Salvar em afeta novas tarefas; itens existentes conservam sua pasta original.

Os arquivos de estado podem conter URLs públicas fornecidas, títulos e caminhos locais. Não os publique indiscriminadamente. Credenciais não devem ser incluídas nas URLs.

## 11. Atualizar motor e aplicativo

> Atenção: Atualizações substituem executáveis. Atualizar o motor pode mudar o comportamento dos extratores; instalar uma atualização do aplicativo fecha a aplicação.

Atualizar yt-dlp executa a atualização oficial do yt-dlp. Pausar a fila, pare a descoberta e aguarde o término dos processos antes de atualizar. Essa ação não atualiza o FFmpeg nem o Baixaki.

Atualizações do Baixaki consulta as releases de TheOrionEXE/Baixaki. Se houver versão mais recente com BaixakiSetup.exe, o programa pede confirmação para baixar e executar o instalador. A fila é salva durante o fechamento e abre parada na nova sessão.

O aplicativo instalado por usuário normalmente permite atualizar o motor sem elevação. Uma pasta sem permissão de escrita pode impedir a atualização.

Se você compila o projeto, build.bat gera o executável; build-installer.bat gera o instalador. Esses scripts não publicam nada no GitHub.

A atualização real pela rede e a instalação sobre todas as versões antigas não foram executadas nos testes desta entrega. Os testes cobrem o comando, a compilação e a compatibilidade estática do instalador.

## 12. Diagnóstico e solução de problemas

> Atenção: Não compartilhe cookies, senhas, tokens, URLs privadas ou arquivos de estado completos ao pedir suporte.

Analysis unavailable: atualize o yt-dlp se apropriado e tente novamente; se o recurso for conhecido, selecione Um vídeo / áudio ou Collection manualmente. Uma escolha manual não supera restrições.

Empty / unsupported: o extrator não retornou entradas acessíveis. Confira a URL, o acesso normal à página e o suporte do motor. Não repita tentativas em massa.

Partial: as entradas já descobertas foram mantidas, mas a enumeração não foi confirmada como completa. Revise o erro, a fonte e o limite antes de Buscar itens novamente.

No eligible entries: o filtro pode excluir metadados desconhecidos ou as entradas já podem ter sido enfileiradas. Revise Toda mídia acessível e os estados da coleção.

Falha Premiere: confira espaço, permissões e a presença do FFmpeg. Tentar novamente reutiliza a fonte mantida. Falha de pasta: selecione um destino gravável para novas tarefas.

Para relatar um problema, informe versão do Baixaki, versão do yt-dlp, modo escolhido, passos para reproduzir e o erro já revisado. Identifique o serviço e o tipo de recurso sem fornecer dados privados.

Link não suportado: o motor informou URL sem suporte ou extrator quebrado. O programa não o trata como um pedido de login. Prefira links individuais atendidos pelo motor; uma coleção não é garantida para toda plataforma.

