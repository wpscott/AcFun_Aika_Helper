using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AikaHelper.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using DynamicData;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;

namespace AikaHelper.ViewModels;

/// <summary>
///     主窗口的视图模型
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly string M3U8 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools",
        "N_m3u8DL-RE.exe");

    private static readonly string Ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");

    private readonly AcFun _acFun = Locator.Current.GetService<AcFun>()!;

    private readonly ObservableCollection<GetChannelData> _lives = [];
    private readonly ObservableCollection<GetChannelData> _local = [];

    private readonly ReadOnlyObservableCollection<string> _nicknames;
    private readonly StreamRepository _streamRepository = Locator.Current.GetService<StreamRepository>()!;
    private readonly ReadOnlyObservableCollection<long> _userIds;

    private readonly SourceList<LiveUser> _users = new();
    [Reactive] private int _count = 1;
    [Reactive] private int _current;
    [Reactive] private long _id;

    [Reactive] private bool _isFetching;

    [Reactive] private string _loginCaption = "AcFun 二维码登录";
    [Reactive] private bool _loginFailed;

    [Reactive] private string _name = string.Empty;

    [Reactive] private bool _needLogin = true;

    [ObservableAsProperty] private int _progressTarget = 1;
    [Reactive] private Bitmap? _qrCode;

    [Reactive] private bool _toolsAvailable;
    [Reactive] private string _uid = string.Empty;
    [Reactive] private Bitmap? _userAvatar;
    [Reactive] private long _userId;
    [Reactive] private string _userName = string.Empty;

    public string DownloadButtonCaption { get; }

    /// <summary>
    ///     初始化 <see cref="MainWindowViewModel" /> 类的新实例。
    /// </summary>
    public MainWindowViewModel()
    {
        var shared = _users.Connect().Publish();
        shared
            .Transform(x => x.UserId)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _userIds)
            .Subscribe();
        shared
            .Transform(x => x.Nickname)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _nicknames)
            .Subscribe();
        shared.Connect();
        Id = _streamRepository.GetLastStreamId();
        _progressTargetHelper = this.WhenAnyValue(x => x.Count, Math.Abs)
            .ToProperty(this, nameof(ProgressTarget));
        this.WhenActivated(disposable =>
        {
            if (NeedLogin) _ = QrLogin();

            _users.Edit(users =>
            {
                users.Clear();
                users.AddRange(_streamRepository.GetUsers());
            });

            Disposable.Create(() => { _acFun.Cancel(); }).DisposeWith(disposable);
        });

        ToolsAvailable = File.Exists(M3U8) && File.Exists(Ffmpeg);
        DownloadButtonCaption = ToolsAvailable ? "下载" : "复制下载链接";
    }

    /// <summary>
    ///     获取用户ID的只读集合。
    /// </summary>
    public ReadOnlyObservableCollection<long> UserIds => _userIds;

    /// <summary>
    ///     获取昵称的只读集合。
    /// </summary>
    public ReadOnlyObservableCollection<string> Nicknames => _nicknames;

    /// <summary>
    ///     获取直播数据的只读集合。
    /// </summary>
    public ReadOnlyObservableCollection<GetChannelData> Lives => new(_lives);

    /// <summary>
    ///     获取本地数据的只读集合。
    /// </summary>
    public ReadOnlyObservableCollection<GetChannelData> Local => new(_local);

    private IObservable<bool> CanQrLogin => this.WhenAnyValue(x => x.NeedLogin);

    /// <summary>
    ///     执行二维码登录。
    /// </summary>
    [ReactiveCommand(CanExecute = nameof(CanQrLogin))]
    private async Task QrLogin()
    {
        // 设置登录失败标志为 false
        LoginFailed = false;
        // 更新登录提示信息
        LoginCaption = "获取登陆二维码中";

        // 开始扫描二维码
        var imgData = await _acFun.StartScanAsync();
        if (imgData is null)
        {
            // 设置登录失败标志为 true
            LoginFailed = true;
            if (_acFun.IsCanceled)
            {
                // 更新登录提示信息
                LoginCaption = "取消登录";

                return;
            }

            // 更新登录提示信息
            LoginCaption = "获取登陆二维码失败";
            return;
        }

        // 将二维码数据转换为流
        using var stream = new MemoryStream(Convert.FromBase64String(imgData));
        // 将流转换为位图
        QrCode = new Bitmap(stream);

        // 更新登录提示信息
        LoginCaption = "请使用AcFun APP扫码登录";

        if (await _acFun.AcceptScan())
        {
            // 更新登录提示信息
            LoginCaption = "扫描成功，请在AcFun APP上确认登录";
            // 确认扫描
            var user = await _acFun.ConfirmScan();
            if (user == null)
            {
                // 设置登录失败标志为 true
                LoginFailed = true;
                if (_acFun.IsCanceled)
                {
                    // 更新登录提示信息
                    LoginCaption = "取消登录";

                    return;
                }

                // 更新登录提示信息
                LoginCaption = "登录失败";
                return;
            }

            // 设置用户的 cookies
            user.SetCookies(_acFun.Container);
            // 清空二维码
            QrCode = null;
            // 更新登录提示信息
            LoginCaption = "登录成功，正在初始化爱咔";

            // 获取 token
            var token = await _acFun.GetToken();
            // 登录爱咔
            var success = await _acFun.AikaLogin(user, token);
            if (!success)
            {
                // 更新登录提示信息
                LoginCaption = "登录失败";
                // 设置需要登录标志为 true
                NeedLogin = true;
                // 设置登录失败标志为 true
                LoginFailed = true;
            }

            else
            {
                // 设置用户 ID
                UserId = user.Id;
                // 设置用户名
                UserName = user.Username;
                using var client = new HttpClient();
                // 获取用户头像
                using var resp = await client.GetAsync(user.AvatarUri);
                // 设置用户头像
                if (resp.IsSuccessStatusCode) UserAvatar = new Bitmap(await resp.Content.ReadAsStreamAsync());
                // 设置需要登录标志为 false
                NeedLogin = false;
                // 设置登录失败标志为 false
                LoginFailed = false;
            }
        }
        else
        {
            // 设置登录失败标志为 true
            LoginFailed = true;

            // 清空二维码
            QrCode = null;

            if (_acFun.IsCanceled)
            {
                // 更新登录提示信息
                LoginCaption = "取消登录";

                return;
            }

            // 更新登录提示信息
            LoginCaption = "登录超时，请重新开始";
        }
    }

    /// <summary>
    ///     获取直播数据。
    /// </summary>
    [ReactiveCommand]
    private async Task FetchLive()
    {
        // 设置正在获取数据标志为 true
        IsFetching = true;
        // 清空直播数据集合
        _lives.Clear();

        // 判断是向前获取数据还是向后获取数据
        var isForward = Count > 0;
        var count = Math.Abs(Count);

        var failed = 0;
        var lastId = Id;
        // 重置 AcFun 实例
        _acFun.Reset();
        var toAdd = new List<GetChannelData>(128);

        // 循环获取直播数据
        for (Current = 0; Current < count; Current++)
        {
            if (_acFun.IsCanceled) break;

            var data = await _acFun.GetVideoDetail(isForward ? Id + Current : Id - Current);

            if (data is null)
            {
                if (_acFun.IsCanceled) break;

                failed++;
                if (failed > 10) break;
                continue;
            }

            failed = 0;
            lastId = data.StreamId;
            // 将获取到的数据插入到直播数据集合的开头
            _lives.Insert(0, data);
            toAdd.Add(data);

            if (toAdd.Count < 100) continue;
            await Task.Run(() =>
            {
                // 将获取到的数据添加到本地数据库
                _streamRepository.AddStream(toAdd);
                toAdd.Clear();
            });
        }

        // 设置正在获取数据标志为 false
        IsFetching = false;
        Id = lastId;

        if (toAdd.Count > 0)
            await Task.Run(() =>
                _streamRepository.AddStream(toAdd)
            );
    }

    /// <summary>
    ///     停止获取直播数据。
    /// </summary>
    [ReactiveCommand]
    private void Stop()
    {
        // 设置正在获取数据标志为 false
        IsFetching = false;
        // 取消 AcFun 实例的操作
        _acFun.Cancel();
    }

    /// <summary>
    ///     获取指定直播数据的详细信息。
    /// </summary>
    /// <param name="data">直播数据。</param>
    [ReactiveCommand]
    private async Task GetStream(GetChannelData data)
    {
        GetChannelData? detail;
        if (data.StreamUrlExpiredTime > DateTimeOffset.UtcNow)
        {
            // 如果直播数据的流 URL 未过期，直接使用该数据
            detail = data;
        }
        else
        {
            // 否则，重置 AcFun 实例并获取最新的直播数据
            _acFun.Reset();

            detail = await _acFun.GetVideoDetail(data.StreamId);

            if (detail == null) return;
            // 更新本地数据库中的直播数据
            _streamRepository.UpdateStream(detail);

            var index = _lives.IndexOf(data);
            _lives[index] = detail;
        }

        if (detail.StreamUrls is not { Length: not 0 }) return;

        if (!ToolsAvailable)
        {
            // 如果工具文件不存在，将流 URL 复制到剪贴板
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
            var clipboard = TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
            if (clipboard == null) return;

            await clipboard.SetTextAsync(detail.StreamUrls[0].ToString());
        }
        else
        {
            // 否则，使用工具下载直播数据
            var info = new ProcessStartInfo
            {
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                FileName = M3U8,
                Arguments =
                    $"\"{detail.StreamUrls[0]}\" --save-name \"[{TimeZoneInfo.ConvertTime(detail.StartTime, GetChannelData.TimeZoneInfo):yyyy-MM-dd HH-mm-ss}][{Sanitizer(detail.Nickname)}]{Sanitizer(detail.Name)}\" --ffmpeg-binary-path \"{Ffmpeg}\"",
                UseShellExecute = false,
                CreateNoWindow = false
            };
            using var process = Process.Start(info);
        }

        return;

        // 清理文件名中的非法字符
        static string Sanitizer(string name)
        {
            return Path.GetInvalidFileNameChars()
                .Aggregate(name, (current, @char) => current.Replace(@char.ToString(), string.Empty));
        }
    }

    /// <summary>
    ///     搜索本地数据。
    /// </summary>
    [ReactiveCommand]
    private void Search()
    {
        // 如果名称和用户ID都为空，则获取所有本地数据
        if (string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Uid))
        {
            _local.Clear();
            _local.AddRange(_streamRepository.GetAllStream());
        }
        // 如果名称不为空，则根据名称搜索本地数据
        else if (!string.IsNullOrEmpty(Name))
        {
            _local.Clear();
            _local.AddRange(_streamRepository.GetStreamByUserName(Name));
        }
        // 如果用户ID不为空且能解析为长整型，则根据用户ID搜索本地数据
        else if (!string.IsNullOrEmpty(Uid) && long.TryParse(Uid, out var uid))
        {
            _local.Clear();
            _local.AddRange(_streamRepository.GetStreamByUserId(uid));
        }
    }
}