using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AikaHelper.Models;

/// <summary>
///     AcFun 类，提供与 AcFun 相关的操作方法
/// </summary>
internal class AcFun
{
    private const string ScanHost = "https://scan.acfun.cn";
    private const string Prefix = "rest/pc-direct/qr/";
    private const string StartScanUri = $"{Prefix}start?type=WEB_LOGIN&_={{0}}";

    private const string AcceptScanUri =
        $"{Prefix}{{0}}?qrLoginToken={{1}}&qrLoginSignature={{2}}&_=&_={{3}}";

    private const string ConfirmScanUri =
        $"{Prefix}{{0}}?qrLoginToken={{1}}&qrLoginSignature={{2}}&_=&_={{3}}";

    private const string IdHost = "https://id.app.acfun.cn";
    private const string TokenUri = "/rest/web/token/get";

    private static readonly ILogger Logger = Log.ForContext<AcFun>();

    private readonly HttpClient _client;

    private CancellationTokenSource _cts = new();

    private string? _next;
    private string? _signature;
    private string? _token;

    /// <summary>
    ///     初始化 AcFun 类的新实例
    /// </summary>
    public AcFun()
    {
        Container = new CookieContainer();
        _client = new HttpClient(new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = Container,
            AllowAutoRedirect = true
        })
        {
            BaseAddress = new Uri(ScanHost)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
    }

    /// <summary>
    ///     获取或设置 Cookie 容器
    /// </summary>
    public CookieContainer Container { get; }

    /// <summary>
    ///     获取一个值，该值指示操作是否已取消
    /// </summary>
    public bool IsCanceled => _cts.IsCancellationRequested;

    /// <summary>
    ///     重置 CancellationTokenSource
    /// </summary>
    public void Reset()
    {
        // 释放当前的CancellationTokenSource
        _cts.Dispose();
        // 创建一个新的CancellationTokenSource
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    ///     取消操作
    /// </summary>
    public void Cancel()
    {
        // 取消当前的CancellationTokenSource
        _cts.Cancel();
    }

    /// <summary>
    ///     开始扫码登录
    /// </summary>
    /// <returns>返回二维码图像数据</returns>
    public async Task<string?> StartScanAsync()
    {
        // 重置 CancellationTokenSource
        Reset();
        // 清空 _next, _token 和 _signature
        _next = _token = _signature = null;

        try
        {
            // 发送 GET 请求以开始扫码
            using var resp =
                await _client.GetAsync(string.Format(StartScanUri, DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    _cts.Token);
            // 如果操作已取消，返回 null
            if (_cts.IsCancellationRequested) return null;
            // 记录调试信息
            Logger.Debug("Start Scan: {Content}", await resp.Content.ReadAsStringAsync());

            // 如果响应状态码不成功，返回 null
            if (!resp.IsSuccessStatusCode) return null;
            // 反序列化响应内容为 StartResult 对象
            var result = await JsonSerializer.DeserializeAsync<StartResult>(
                await resp.Content.ReadAsStreamAsync());

            // 如果结果不为 0，返回 null
            if (result?.Result != 0) return null;
            // 设置 _next
            _next = result.Next;
            // 设置 _token
            _token = result.QrLoginToken;
            // 设置 _signature
            _signature = result.QrLoginSignature;
            // 返回二维码图像数据
            return result.ImageData;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    ///     接受扫描
    /// </summary>
    /// <returns>返回是否接受成功</returns>
    public async Task<bool> AcceptScan()
    {
        try
        {
            // 发送 GET 请求以接受扫描
            using var resp = await _client.GetAsync(string.Format(AcceptScanUri, _next, _token, _signature,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), _cts.Token);
            // 如果操作已取消，返回 false
            if (_cts.IsCancellationRequested) return false;
            // 记录调试信息
            Logger.Debug("Accept Scan: {Content}", await resp.Content.ReadAsStringAsync());

            // 如果响应状态码不成功，返回 false
            if (!resp.IsSuccessStatusCode) return false;
            // 反序列化响应内容为 ScanResult 对象
            var result = await JsonSerializer.DeserializeAsync<ScanResult>(
                await resp.Content.ReadAsStreamAsync());

            // 如果结果不为 0，返回 false
            if (result?.Result != 0) return false;
            // 设置 _next
            _next = result.Next;
            // 设置 _signature
            _signature = result.QrLoginSignature;
            // 返回 true 表示接受成功
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    ///     确认扫描
    /// </summary>
    /// <returns>返回用户信息</returns>
    public async Task<User?> ConfirmScan()
    {
        try
        {
            // 发送 GET 请求以确认扫描
            using var resp = await _client.GetAsync(string.Format(ConfirmScanUri, _next, _token, _signature,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), _cts.Token);
            // 如果操作已取消，返回 null
            if (_cts.IsCancellationRequested) return null;
            // 记录调试信息
            Logger.Debug("Confirm Scan: {Content}", await resp.Content.ReadAsStringAsync());

            // 如果响应状态码不成功，返回 null
            if (!resp.IsSuccessStatusCode) return null;

            // 反序列化响应内容为 AcceptResult 对象
            var result = await JsonSerializer.DeserializeAsync<AcceptResult>(
                await resp.Content.ReadAsStreamAsync());

            // 如果结果不为 0，返回 null
            if (result?.Result != 0) return null;

            // 返回用户信息
            return new User
            {
                Id = result.UserId,
                Username = result.Username,
                Avatar = result.Avatar,
                Passtoken = result.Passtoken
            };
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    ///     获取令牌
    /// </summary>
    /// <returns>返回令牌信息</returns>
    public async Task<Token?> GetToken()
    {
        // 创建表单内容，包含 sid 参数
        using var form = new FormUrlEncodedContent([new KeyValuePair<string, string>("sid", "acfun.midground.api")]);
        // 发送 POST 请求以获取令牌
        using var resp = await _client.PostAsync($"{IdHost}{TokenUri}", form);

        // 记录调试信息
        Logger.Debug("Token: {Content}", await resp.Content.ReadAsStringAsync());

        // 如果响应状态码不成功，返回 null
        if (!resp.IsSuccessStatusCode) return null;

        // 反序列化响应内容为 Token 对象
        var token = await JsonSerializer.DeserializeAsync<Token>(await resp.Content.ReadAsStreamAsync());
        // 如果结果为 0，添加 Cookie
        if (token?.Result == 0)
            Container.Add(new Cookie(Token.ST, token.SToken, "/", User.CookieKuaishouDomain));

        // 返回令牌
        return token;
    }

    /// <summary>
    ///     爱咔登录
    /// </summary>
    /// <param name="user">用户信息</param>
    /// <param name="token">令牌信息</param>
    /// <returns>返回是否登录成功</returns>
    public async Task<bool> AikaLogin(User user, Token token)
    {
        {
            // 构建第一个请求的 URL
            var url =
                $"https://onvideoapi.kuaishou.com/rest/infra/sts?authToken={token.AToken}&sid=acfun.midground.api&followUrl=https://onvideo.kuaishou.com/hub/home?source=ac&passToken=null&userId={user.Id}";

            // 发送 GET 请求
            using var resp = await _client.GetAsync(url);

            // 如果响应状态码不成功，返回 false
            if (!resp.IsSuccessStatusCode) return false;
        }
        {
            // 构建第二个请求的 URL
            const string url =
                "https://onvideoapi.kuaishou.com/uaa/account/current?source=ac&__redirectURL=https%3A%2F%2Fonvideo.kuaishou.com%2Fhub%2Fhome%3Fsource%3Dac";
            // 发送 GET 请求
            using var resp = await _client.GetAsync(url);

            // 如果响应状态码不成功，返回 false
            if (!resp.IsSuccessStatusCode) return false;
            // 检查响应头中是否包含 Set-Cookie
            if (!resp.Headers.TryGetValues("Set-Cookie", out var cookies))
                // 如果没有包含特定的 Cookie，返回 false
                if (cookies?.Any(cookie => cookie.Contains("ks_onvideo_ps_token")) == false)
                    return false;

            // 设置请求头的 Origin 和 Referrer
            _client.DefaultRequestHeaders.Add("Origin", "https://onvideo.kuaishou.com");
            _client.DefaultRequestHeaders.Referrer = new Uri("https://onvideo.kuaishou.com/");
            // 返回响应状态码是否成功
            return resp.IsSuccessStatusCode;
        }
    }

    /// <summary>
    ///     获取视频详情
    /// </summary>
    /// <param name="id">视频 ID</param>
    /// <param name="retry">重试次数</param>
    /// <returns>返回视频详情数据</returns>
    public async Task<GetChannelData?> GetVideoDetail(long id, int retry = 0)
    {
        try
        {
            // 构建请求 URL
            var url = $"https://onvideoapi.kuaishou.com/api/live/get_channel/{id}?source=ac";
            // 发送 GET 请求
            using var resp = await _client.GetAsync(url, _cts.Token);
            // 检查操作是否已取消
            if (_cts.IsCancellationRequested) return null;
            // 记录调试信息
            Logger.Debug("Get channel: {Content}", await resp.Content.ReadAsStringAsync());
            // 检查响应状态码是否成功
            if (!resp.IsSuccessStatusCode) return null;

            // 反序列化响应内容为 BaseResponse<GetChannelData> 对象
            var json = await JsonSerializer.DeserializeAsync<BaseResponse<GetChannelData>>(
                await resp.Content.ReadAsStreamAsync());
            // 检查响应代码是否为 200
            if (json is { Code: 200 })
            {
                // 检查 StreamUrls 是否不为空
                if (json.Data.StreamUrls is { Length: not 0 }) return json.Data;
                // 如果重试次数不超过 3 次，递归调用 GetVideoDetail 方法
                if (retry <= 3) return await GetVideoDetail(id, retry + 1);
                // 设置 StreamUrls 为空数组
                json.Data.StreamUrls = [];
                return json.Data;
            }

            // 记录失败信息
            Logger.Debug("Failed to get response ({Id}): {Message}", id, json?.Msg);
            return null;
        }
        catch (IOException ex)
        {
            // 记录 IO 异常
            Logger.Error(ex, "Failed to get response");
            // 如果重试次数不超过 3 次，递归调用 GetVideoDetail 方法
            if (retry <= 3) return await GetVideoDetail(id, retry + 1);
            return null;
        }
        catch (JsonException ex)
        {
            // 记录 JSON 反序列化异常
            Logger.Error(ex, "Failed to deserialize response");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}

/// <summary>
///     令牌类
/// </summary>
public record Token
{
    public const string ST = "acfun.midground.api_st";
    public const string AT = "acfun.midground.api.at";

    [JsonPropertyName("result")] public int Result { get; init; } = -1;

    [JsonPropertyName(ST)] public string SToken { get; init; } = string.Empty;

    [JsonPropertyName(AT)] public string AToken { get; init; } = string.Empty;

    [JsonPropertyName("userId")] public long UserId { get; init; } = -1;
    [JsonPropertyName("ssecurity")] public string Ssecurity { get; init; } = string.Empty;

    [JsonPropertyName("error_msg")] public string ErrorMsg { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"{{ \"st\": {SToken}, \"ssecurity\": {Ssecurity} }}";
    }
}

/// <summary>
///     开始扫描结果类
/// </summary>
public record StartResult
{
    [JsonPropertyName("expireTime")] public long ExpireTime { get; init; }
    [JsonPropertyName("imageData")] public string ImageData { get; init; }
    [JsonPropertyName("next")] public string Next { get; init; }
    [JsonPropertyName("qrLoginSignature")] public string QrLoginSignature { get; init; }
    [JsonPropertyName("qrLoginToken")] public string QrLoginToken { get; init; }
    [JsonPropertyName("result")] public int Result { get; init; }
    [JsonPropertyName("error_msg")] public string ErrorMsg { get; init; }
}

/// <summary>
///     扫描结果类
/// </summary>
public record ScanResult
{
    [JsonPropertyName("next")] public string Next { get; init; }
    [JsonPropertyName("qrLoginSignature")] public string QrLoginSignature { get; init; }
    [JsonPropertyName("status")] private string Status { get; init; }
    [JsonPropertyName("result")] public int Result { get; init; }
    [JsonPropertyName("error_msg")] public string ErrorMsg { get; init; }
}

/// <summary>
///     接受结果类
/// </summary>
public record AcceptResult
{
    [JsonPropertyName("next")] public string Next { get; init; }
    [JsonPropertyName("qrLoginSignature")] public string QrLoginSignature { get; init; }
    [JsonPropertyName("status")] private string Status { get; init; }
    [JsonPropertyName("result")] public int Result { get; init; }
    [JsonPropertyName("userId")] public long UserId { get; init; }
    [JsonPropertyName("ac_username")] public string Username { get; init; }
    [JsonPropertyName("ac_userimg")] public string Avatar { get; init; }
    [JsonPropertyName("acPasstoken")] public string Passtoken { get; init; }
    [JsonPropertyName("error_msg")] public string ErrorMsg { get; init; }
}

/// <summary>
///     用户类
/// </summary>
public class User
{
    public const string CookieKuaishouDomain = ".kuaishouzt.com";
    public const string CookieUserId = "userId";

    public const string CookieAcFunDomain = ".acfun.cn";
    public const string CookieId = "auth_key";
    public const string CookieUsername = "ac_username";
    public const string CookieAvatar = "ac_userimg";
    public const string CookiePasstoken = "acPasstoken";
    public static readonly Uri CookieUri = new($"http://{CookieAcFunDomain[1..]}");

    public long Id { get; init; }
    public string Username { get; init; } = null!;
    public string Avatar { get; init; } = null!;
    public Uri AvatarUri => new(Avatar);

    public string Passtoken { get; init; } = null!;

    /// <summary>
    ///     设置用户的 Cookie
    /// </summary>
    /// <param name="container">Cookie 容器</param>
    public void SetCookies(in CookieContainer container)
    {
        container.Add(new Cookie(CookiePasstoken, Passtoken, "/", CookieAcFunDomain));
        container.Add(new Cookie(CookieId, Id.ToString(), "/", CookieAcFunDomain));
        container.Add(new Cookie(CookieUserId, Id.ToString(), "/", CookieKuaishouDomain));
    }

    /// <summary>
    ///     设置用户的 Cookie
    /// </summary>
    /// <param name="container">Cookie 容器</param>
    /// <param name="passtoken">通行令牌</param>
    /// <param name="id">用户 ID</param>
    public static void SetCookie(in CookieContainer container, in string passtoken, in long id)
    {
        container.Add(new Cookie(CookiePasstoken, passtoken, "/", CookieAcFunDomain));
        container.Add(new Cookie(CookieId, id.ToString(), "/", CookieAcFunDomain));
        container.Add(new Cookie(CookieUserId, id.ToString(), "/", CookieKuaishouDomain));
    }
}

/// <summary>
///     基础响应类
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class BaseResponse<T>
{
    [JsonPropertyName("code")] public long Code { get; init; }

    [JsonPropertyName("msg")] public string Msg { get; init; }

    [JsonPropertyName("data")] public T Data { get; init; }

    [JsonPropertyName("host")] public string Host { get; init; }
}

/// <summary>
///     获取直播数据类
/// </summary>
public class GetChannelData
{
    public static readonly TimeZoneInfo TimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    [JsonPropertyName("id")] public string Id { get; init; }

    [JsonPropertyName("streamId")] public long StreamId { get; init; }

    [JsonPropertyName("streamName")] public string StreamName { get; init; }

    [JsonPropertyName("name")] public string Name { get; init; }

    [JsonPropertyName("status")] public long Status { get; init; }
    [JsonPropertyName("streamUrls")] public Uri[]? StreamUrls { get; set; }
    [JsonPropertyName("streamInnerUrl")] public string StreamInnerUrl { get; init; }

    [JsonConverter(typeof(UnixTimestampMillisecondsConvert))]
    [JsonPropertyName("createdTime")]
    public DateTimeOffset CreatedTime { get; init; }

    [JsonConverter(typeof(UnixTimestampMillisecondsConvert))]
    [JsonPropertyName("startTime")]
    public DateTimeOffset StartTime { get; init; }

    [JsonConverter(typeof(UnixTimestampMillisecondsConvert))]
    [JsonPropertyName("endTime")]
    public DateTimeOffset EndTime { get; init; }

    [JsonPropertyName("thumbUrl")] public string ThumbUrl { get; init; }

    [JsonConverter(typeof(UnixTimestampMillisecondsConvert))]
    [JsonPropertyName("streamUrlExpiredTime")]
    public DateTimeOffset StreamUrlExpiredTime { get; init; }

    [JsonPropertyName("clientSourceId")] public long ClientSourceId { get; init; }

    [JsonPropertyName("type")] public long Type { get; init; }

    [JsonPropertyName("client")] public long Client { get; init; }

    [JsonPropertyName("weight")] public long Weight { get; init; }

    [JsonPropertyName("manage")] public long Manage { get; init; }

    [JsonPropertyName("ksUserId")] public long KsUserId { get; init; }

    [JsonPropertyName("nickname")] public string Nickname { get; init; }

    [JsonPropertyName("userId")] public long UserId { get; init; }

    [JsonPropertyName("sortId")] public long SortId { get; init; }

    [JsonPropertyName("privateLive")] public bool PrivateLive { get; init; }

    [JsonPropertyName("property")] public long Property { get; init; }

    public string Duration =>
        $"时长：{(EndTime == DateTimeOffset.UnixEpoch ? "N/A" : $"{EndTime - StartTime:g}")}";

    public string Time =>
        $"时间：{TimeZoneInfo.ConvertTime(StartTime, TimeZoneInfo):yyyy年MM月dd日 HH:mm:ss} ~ {(EndTime == DateTimeOffset.UnixEpoch ? "N/A" : $"{TimeZoneInfo.ConvertTime(EndTime, TimeZoneInfo):yyyy年MM月dd日HH:mm:ss}")}";

    public string ExpireTime =>
        $"下载链接过期时间：{TimeZoneInfo.ConvertTime(StreamUrlExpiredTime, TimeZoneInfo):yyyy年MM月dd日 HH:mm:ss}";

    public bool CanDownloadLocal =>
        StreamUrlExpiredTime > DateTimeOffset.UtcNow;

    public bool CanDownloadRemote =>
        StartTime > DateTimeOffset.UtcNow.AddMonths(-2);
}

public class UnixTimestampMillisecondsConvert : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Number
            ? DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64())
            : DateTimeOffset.UnixEpoch;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTimeMilliseconds());
    }
}