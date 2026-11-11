using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
public static class UiText
{
    public static string HoldReason(string reason)
    {
        return reason == "Rate limit" ? "o site limitou os acessos" : "o site pediu login ou uma verificação";
    }
    // Why a source is paused and when it comes back, in one sentence.
    public static string Hold(SourceHold hold)
    {
        if (hold == null) return "A fonte está em pausa por uma restrição de acesso.";
        string when = hold.Until.HasValue ? " Volta sozinha às " + hold.Until.Value.ToLocalTime().ToString("HH:mm") + "." : "";
        return "A fonte " + hold.Source + " está em pausa: " + HoldReason(hold.Reason) + "." + when + " Se já resolveu, libere em Configurações > Fontes em pausa.";
    }
    // Turns known engine errors into one clear sentence; anything else is shown as the engine reported it.
    public static string Friendly(string error)
    {
        if (String.IsNullOrEmpty(error)) return "Erro desconhecido";
        if (Regex.IsMatch(error, @"(?i)WinError 448|untrusted mount|ponto de montagem n.o confi")) return "O Windows bloqueou o caminho de uma pasta (junção ou pasta redirecionada). Escolha uma pasta de destino comum, como Vídeos.";
        return error;
    }
    public static readonly string[] Filters={"All accessible media","Videos only (known metadata)","Audio-compatible only (known metadata)","Other media: unsupported"};
    public const string Unsupported="Este link não é atendido pelo yt-dlp instalado. Links de vídeos individuais ainda podem funcionar. Escolher Coleção ou ativar cookies não corrige um extrator ausente ou quebrado.";
    public static string Mode(MediaMode mode,bool premiere)
    {
        switch(mode)
        {
            case MediaMode.Mp3:return "MP3";
            case MediaMode.Wav:return "WAV";
            case MediaMode.Gif:return "GIF";
            case MediaMode.Thumbnail:return "Miniatura";
            case MediaMode.VideoOnly:return premiere?"Premiere · sem áudio":"MP4 sem áudio";
            default:return premiere?"Premiere":"MP4";
        }
    }
    // Chip label of the format selector, with the quality when it applies.
    public static string Format(MediaMode mode,bool premiere){return mode==MediaMode.Mp3||mode==MediaMode.Wav||mode==MediaMode.Gif||mode==MediaMode.Thumbnail?Mode(mode,false):Mode(mode,premiere);}
    public static string Quality(string quality){return String.IsNullOrEmpty(quality)||quality=="best"?"Melhor qualidade":quality+"p";}
    public static string Day(DateTime local)
    {
        DateTime today=DateTime.Now.Date;
        return local.Date==today?"HOJE":local.Date==today.AddDays(-1)?"ONTEM":local.ToString("dd/MM/yyyy");
    }
    public static string Status(string status)
    {
        switch(status){case "Pending":return "Aguardando";case "Downloading":return "Baixando";case "Converting":return "Convertendo";case "Paused":return "Pausado";case "Completed":return "Concluído";case "Cancelled":return "Cancelado";case "Failed":return "Falhou";case "Blocked":return "Fonte em pausa";case "Unsupported":return "Não suportado";default:return status;}
    }
    public static string Collection(CollectionJob job)
    {
        if(job.Status=="Discovering")return "Buscando itens. Você pode parar sem perder os resultados já salvos.";
        if(job.Status=="Unsupported")return Unsupported;
        if(job.Status=="Blocked")return "A fonte está em pausa por uma restrição. Resolva a causa antes de tentar novamente.";
        if(job.Status=="Partial")return "Busca parcial: os itens encontrados foram salvos. Consulte os detalhes antes de continuar.";
        if(job.Status.StartsWith("Preview"))return "Prévia aberta. Use Buscar itens para consultar a coleção, após revisar o limite e o aviso.";
        if(job.Status.StartsWith("Batch"))return "Lote "+job.BatchNumber+" adicionado. Inicie a fila; o próximo lote depende de uma nova ação sua.";
        if(job.Status.StartsWith("Empty"))return "Nenhum item acessível foi retornado. Verifique o suporte e o acesso à página.";
        if(job.Status.StartsWith("No eligible"))return "Nenhum novo item elegível. Revise o filtro, os resultados e os itens já adicionados.";
        return "Busca encerrada. Os resultados podem ser incompletos; o total da fonte pode ser desconhecido.";
    }
    public static string Speed(string value)
    {
        double bytes;if(String.IsNullOrEmpty(value)||!Double.TryParse(value.Replace(" B/s",""),NumberStyles.Float,CultureInfo.InvariantCulture,out bytes))return "—";
        return bytes>=1048576?(bytes/1048576).ToString("0.0")+" MB/s":(bytes/1024).ToString("0.0")+" KB/s";
    }
    public static string Message(string value)
    {
        if(value==UnsupportedResourceException.Guidance)return Unsupported;
        if(value.StartsWith("DONE: "))return "Concluído: "+value.Substring(6);
        if(value.StartsWith("FAIL: "))return "Erro: "+value.Substring(6);
        if(value=="Transient failure; retrying conservatively.")return "Falha temporária. Aguardando antes de uma nova tentativa.";
        return value;
    }
}
