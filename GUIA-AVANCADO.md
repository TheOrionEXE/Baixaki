# Guia avançado do Baixaki 1.2.0

Este guia reúne os mesmos tópicos da Central de ajuda do aplicativo (tecla F1, ou Configurações > Ajuda).

## 1. Começar e reconhecer links

> Atenção: A análise consulta a fonte. O tempo e o resultado dependem do site e do extrator do yt-dlp.

Cole um link http(s) de uma página de vídeo ou playlist no campo do topo. O Baixaki identifica o link sozinho, sem esperas artificiais, e mostra abaixo a miniatura, o título, o canal e a duração.

Pressione Enter ou clique em Adicionar. Um vídeo individual entra na Fila e começa a baixar. Uma playlist ou coleção abre uma linha própria na Fila para você escolher os vídeos.

As etiquetas abaixo do campo mostram o formato, a qualidade e a pasta de destino atuais. Clique em cada uma para mudar. As demais opções ficam na engrenagem, no canto superior direito.

A janela tem tamanho fixo mínimo e nunca muda de tamanho sozinha: abas, playlists e configurações aparecem dentro dela. Você pode colar novos links enquanto os downloads acontecem.

## 2. Perfis: riscos e o que não funciona

> Atenção: Listar perfis inteiros pode causar limites de acesso e restrições na conta. Cookies associam as solicitações à conta autenticada. Nenhum ritmo de acesso garante ausência de bloqueios.

Use somente páginas às quais você pode acessar. Fora do YouTube, o Baixaki pede confirmação antes de listar uma coleção ou perfil, porque isso gera muitas solicitações. Comece com Itens por playlist em 10, deixe Cookies do navegador em Não usar se não precisar de login e mantenha a espera automática.

O limite de itens não é um orçamento exato de requisições. Um extrator pode visitar páginas adicionais, retornar dados incompletos ou exigir autenticação.

Ao aparecer um limite de acesso, login obrigatório ou outro desafio, o programa suspende novas solicitações àquela fonte. Não libere a fonte repetidamente nem insista sem resolver a causa. Verifique a situação pelo acesso normal ao serviço.

O programa não contorna CAPTCHA, não usa proxies e não promete evitar detecção ou restrições.

Perfis do Instagram não são suportados: o extrator de perfis do yt-dlp (instagram:user) está marcado como quebrado na versão 2026.10.21. O Baixaki avisa na hora, sem fazer nenhuma requisição. Links de posts e reels individuais continuam funcionando. Cookies não consertam um extrator quebrado.

## 3. Playlists e coleções

> Atenção: Listar todos os itens de uma coleção grande gera muitas solicitações. Listar e baixar são etapas separadas.

Ao adicionar uma playlist do YouTube, o Baixaki lista os vídeos automaticamente. Em outros sites, a linha mostra Listar itens e pede confirmação antes.

A linha da playlist mostra quantos vídeos foram encontrados e quantos estão marcados. Clique na linha, ou na seta, para abrir a lista com miniaturas e duração. Marque ou desmarque cada vídeo, ou use Selecionar todos.

O botão Adicionar N à fila envia os vídeos marcados para baixar. Os vídeos que você deixou desmarcados não são baixados, e a playlist sai da Fila. O X da linha descarta a playlist.

Itens por playlist (nas configurações) define quantos vídeos listar: Todos, 10 ou 50. O padrão é Todos. Subpasta por playlist guarda os vídeos de cada playlist em uma pasta separada, dentro do destino.

Uma listagem parcial não prova que todos os vídeos foram encontrados. Se uma playlist ainda tem vídeos em andamento, espere terminarem antes de adicionar mais da mesma lista.

## 4. Fila e Concluídos

> Atenção: Os downloads concluídos saem da lista depois de 12 dias. Os arquivos baixados nunca são apagados.

A aba Fila mostra o que está acontecendo: BAIXANDO (com barra de progresso e velocidade), AGUARDANDO e COM PROBLEMA (falhas, fontes em pausa e cancelados). A aba Concluídos mostra os downloads prontos, agrupados por dia, com busca por título.

Os números nas abas mostram quantos itens há em cada uma. Quando um download termina, ele passa sozinho da Fila para Concluídos.

Em Concluídos, o botão de reproduzir abre o arquivo, a pasta mostra o arquivo no Explorer e o X remove só a linha da lista. Limpar lista remove todas as linhas concluídas; os arquivos continuam na pasta. Se um arquivo foi movido ou apagado, a linha avisa e oferece Tentar de novo.

Uma linha concluída sai da lista automaticamente depois de 12 dias. Isso afeta apenas a lista, não os arquivos.

## 5. Pausar, cancelar, remover e tentar de novo

> Atenção: Cancelar ou remover pode apagar arquivos temporários e exigir nova transferência. Pausar é a opção indicada para continuar depois.

Adicionar um item já inicia o download. O botão Pausar, acima da lista, interrompe o processo ativo e conserva os parciais; Retomar continua de onde parou, conforme o servidor e o formato permitirem. Em uma nova sessão, a fila abre parada.

O X de um item em andamento cancela o download e limpa os parciais dele. O X de um item aguardando remove o item da fila. Arquivos finais já salvos nunca são apagados por esses botões.

Tentar de novo recoloca uma falha, um item cancelado ou bloqueado na fila, desde que não esteja ativo. Uma fonte bloqueada precisa ser resolvida e liberada antes de voltar a receber solicitações.

Se preferir iniciar manualmente, desligue Iniciar ao adicionar nas configurações e use o botão Baixar tudo.

## 6. Formatos e qualidade

> Atenção: Converter exige CPU, espaço adicional e tempo. O programa mantém a fonte para recuperar falhas; confira o destino e o espaço disponível.

O menu de formato, na primeira etiqueta abaixo do campo, tem:

MP4: vídeo + áudio, o padrão.
Premiere: H.264 em MP4, pixels yuv420p e áudio AAC, para abrir bem em editores.
GIF: um trecho do vídeo convertido em GIF. Ao escolher, defina o início, a duração (até 60 s) e a largura. Só o trecho é baixado.
MP3: apenas o áudio, compatível com tudo.
WAV: áudio sem perda, para edição.
Miniatura: só a imagem de capa em JPG.

A etiqueta de qualidade (Melhor, 1080p, 720p, 480p) aparece nos formatos de vídeo e limita a altura do vídeo. A mesma mídia em outro formato, outra qualidade ou outro trecho de GIF conta como um download diferente.

Se o download terminar e a conversão falhar, Tentar de novo reutiliza a fonte local. Os temporários ficam na subpasta .baixaki do destino.

Um arquivo H.264 compatível não garante que todas as versões do Premiere aceitem qualquer combinação de mídia. Confira o resultado no seu editor.

## 7. Espera entre itens e duplicatas

> Atenção: Aumentar a espera pode reduzir a frequência de acesso, mas não garante permissão nem evita restrições do serviço.

Espera entre itens controla o intervalo entre downloads e entre requisições do yt-dlp. No modo Automático, é de 1 segundo quando há menos de 10 itens abertos na fila e de 5 segundos a partir disso. Você também pode fixar 1, 3, 5, 10 ou 30 segundos. A análise de um link nunca espera.

Há um download por vez. Para falhas temporárias, o programa tenta mais duas vezes, com espera conservadora. Erros permanentes não são repetidos sozinhos.

Pular já baixados evita baixar de novo um vídeo que já existe na pasta, no mesmo formato e qualidade. O arquivo precisa continuar existindo. Se você apagar ou mover o arquivo, uma nova inclusão baixa de novo.

Não apague nem edite o arquivo de estado manualmente enquanto o aplicativo estiver aberto.

## 8. Cookies do navegador e privacidade

> Atenção: Cookies podem dar acesso à sua sessão. As requisições feitas com eles podem afetar sua conta. Use apenas um perfil seu e somente quando necessário.

Cookies do navegador, nas configurações, é opcional e começa em Não usar a cada sessão. Ao escolher um navegador, um aviso explica o acesso antes de ativá-lo.

O yt-dlp tentará ler os cookies do navegador escolhido. Isso depende do navegador, do perfil padrão, das proteções do Windows e da versão do motor. O Baixaki não tem campos para senhas, tokens, arquivos de cookies ou seleção de perfis.

Os valores dos cookies não são copiados para a fila, o histórico ou o registro. A escolha de navegador também não é salva entre sessões.

Prefira Não usar para conteúdo público. Uma sessão autenticada não garante acesso nem permissão para listar um perfil. O programa rejeita links com senhas ou tokens embutidos.

Ao compartilhar um erro, revise também nomes de arquivos, títulos, pastas e capturas de tela.

## 9. Limites de acesso e fontes em pausa

> Atenção: Liberar uma fonte não resolve um bloqueio no serviço. Liberar repetidamente e tentar de novo pode agravar a restrição.

Um erro claro de limite, autenticação ou desafio de acesso coloca a fonte em pausa. Os novos itens da mesma fonte ficam sem processamento; downloads de outras fontes continuam.

Quando o yt-dlp informa um prazo (Retry-After), ele é registrado e não pode ser removido antes de vencer.

Sem prazo, a liberação é manual. Verifique a causa pelo acesso normal ao serviço, espere quando houver limite e só então use Fontes em pausa, na área Diagnóstico das configurações (o botão só aparece quando há uma fonte em pausa). Depois, use Tentar de novo nos itens bloqueados.

As fontes são agrupadas pelo domínio da página. Isso é uma proteção conservadora: o aplicativo não sabe o alcance exato de uma restrição de conta ou de IP.

## 10. Estado, histórico e cópia de segurança

> Atenção: Apagar o arquivo de estado ou a pasta .baixaki pode perder a fila ou impedir a retomada. Feche o programa antes de qualquer manutenção manual.

As preferências, a fila, as playlists e o histórico ficam em %APPDATA%\Baixaki\state.json. As gravações substituem o arquivo de forma segura e mantêm state.json.bak. Se a leitura falhar, o programa tenta a cópia e avisa no registro. O botão Dados do app, nas configurações, abre essa pasta.

Para uma cópia de segurança, feche o Baixaki e copie a pasta %APPDATA%\Baixaki. Para manter a recuperação de transferências, preserve também as pastas de destino e suas subpastas .baixaki.

Itens interrompidos voltam pausados. A fila não começa a baixar sozinha ao abrir o aplicativo.

As miniaturas ficam em um cache em %APPDATA%\Baixaki\thumbs, apagado sozinho depois de 12 dias. Mudar a pasta padrão afeta só os novos downloads.

Os arquivos de estado podem conter links públicos, títulos e caminhos locais. Não os publique indiscriminadamente.

## 11. Atualizar o motor e o aplicativo

> Atenção: Atualizações substituem executáveis. Atualizar o motor pode mudar o comportamento dos extratores; instalar uma atualização do aplicativo fecha a aplicação.

O yt-dlp se atualiza sozinho, no máximo uma vez por dia, quando nada está sendo baixado. Em Configurações, o botão Verificar ao lado do yt-dlp faz a verificação na hora. Essa ação não atualiza o FFmpeg nem o Baixaki.

O botão Verificar ao lado do Baixaki consulta as versões publicadas em TheOrionEXE/Baixaki. Se houver uma versão mais nova com BaixakiSetup.exe, o programa pede confirmação para baixar e executar o instalador. A fila é salva ao fechar e abre parada na nova sessão.

O aplicativo instalado por usuário normalmente atualiza o motor sem permissão de administrador. Uma pasta sem permissão de escrita pode impedir a atualização.

Se você compila o projeto, build.bat gera o executável e build-installer.bat gera o instalador. Esses scripts não publicam nada no GitHub.

## 12. Diagnóstico e solução de problemas

> Atenção: Não compartilhe cookies, senhas, tokens, links privados ou arquivos de estado completos ao pedir suporte.

Não foi possível identificar o link: atualize o yt-dlp (Configurações > Verificar) e tente de novo. Confirme que a página abre normalmente no navegador.

Link não suportado: o motor informou um endereço sem suporte ou um extrator quebrado. O programa não trata isso como pedido de login. Prefira links de vídeos individuais.

Lista incompleta: os vídeos já encontrados foram mantidos, mas a listagem não foi confirmada como completa. Revise o erro e a fonte antes de listar de novo.

Falha na conversão (Premiere ou GIF): confira o espaço, as permissões e a presença do FFmpeg. Tentar de novo reutiliza a fonte mantida. Falha de pasta: escolha um destino com permissão de gravação.

O botão Registro, nas configurações, mostra o registro técnico da sessão, sem links, cookies ou credenciais.

Para relatar um problema, informe a versão do Baixaki, a versão do yt-dlp, o formato escolhido, os passos para reproduzir e o erro já revisado. Identifique o serviço e o tipo de recurso sem dados privados.
