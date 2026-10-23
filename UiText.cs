using System;
using System.Collections.Generic;
using System.Globalization;
public static class UiText
{
    public static readonly string[] Filters={"All accessible media","Videos only (known metadata)","Audio-compatible only (known metadata)","Other media: unsupported"};
    public const string Unsupported="Este link não é atendido pelo yt-dlp instalado. Links de vídeos individuais ainda podem funcionar. Escolher Coleção ou ativar cookies não corrige um extrator ausente ou quebrado.";
    public static string Mode(MediaMode mode,bool premiere)
    {return (mode==MediaMode.Mp3?"Áudio MP3":mode==MediaMode.VideoOnly?"Vídeo sem áudio":"Vídeo + áudio")+(premiere&&mode!=MediaMode.Mp3?" · Premiere":"");}
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
