using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace AikaHelper.Models;

/// <summary>
///     数据库帮助类
/// </summary>
internal static class DatabaseHelper
{
    private static readonly string DbPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AcFun爱咔", "app.db");

    /// <summary>
    ///     静态构造函数，初始化数据库连接并创建表
    /// </summary>
    static DatabaseHelper()
    {
        if (!File.Exists(DbPath)) Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

        Batteries_V2.Init();

        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS Stream (
                                  StreamId INTEGER NOT NULL PRIMARY KEY,
                                  Id TEXT NOT NULL,
                                  StreamName TEXT NOT NULL,
                                  Name TEXT NOT NULL,
                                  Status INTEGER NOT NULL,
                                  StreamUrls TEXT,
                                  StreamInnerUrl TEXT NOT NULL,
                                  CreatedTime INTEGER NOT NULL,
                                  StartTime INTEGER NOT NULL,
                                  EndTime INTEGER NOT NULL,
                                  ThumbUrl TEXT,
                                  StreamUrlExpiredTime INTEGER NOT NULL,
                                  ClientSourceId INTEGER NOT NULL,
                                  Type INTEGER NOT NULL,
                                  Client INTEGER NOT NULL,
                                  Weight INTEGER NOT NULL,
                                  Manage INTEGER NOT NULL,
                                  KsUserId INTEGER NOT NULL,
                                  Nickname TEXT NOT NULL,
                                  UserId INTEGER NOT NULL,
                                  SortId INTEGER NOT NULL,
                                  PrivateLive INTEGER NOT NULL,
                                  Property INTEGER NOT NULL
                              );
                              """;

        command.ExecuteNonQuery();
    }

    /// <summary>
    ///     获取数据库连接
    /// </summary>
    /// <returns>SqliteConnection对象</returns>
    public static SqliteConnection GetConnection()
    {
        return new SqliteConnection($"Data Source={DbPath}");
    }
}

/// <summary>
///     SQLite类型处理器抽象类
/// </summary>
/// <typeparam name="T">处理的类型</typeparam>
internal abstract class SqliteTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    /// <summary>
    ///     设置参数值
    /// </summary>
    /// <param name="parameter">数据库参数</param>
    /// <param name="value">值</param>
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = value;
    }
}

/// <summary>
///     DateTimeOffset类型处理器
/// </summary>
internal class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    /// <summary>
    ///     设置DateTimeOffset参数值
    /// </summary>
    /// <param name="parameter">数据库参数</param>
    /// <param name="value">DateTimeOffset值</param>
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = value.ToUnixTimeMilliseconds();
    }

    /// <summary>
    ///     解析DateTimeOffset值
    /// </summary>
    /// <param name="value">数据库值</param>
    /// <returns>DateTimeOffset对象</returns>
    public override DateTimeOffset Parse(object value)
    {
        return value switch
        {
            long ms => DateTimeOffset.FromUnixTimeMilliseconds(ms),
            string str => DateTimeOffset.Parse(str),
            _ => DateTimeOffset.UnixEpoch
        };
    }
}

/// <summary>
///     Guid类型处理器
/// </summary>
internal class GuidHandler : SqliteTypeHandler<Guid>
{
    /// <summary>
    ///     解析Guid值
    /// </summary>
    /// <param name="value">数据库值</param>
    /// <returns>Guid对象</returns>
    public override Guid Parse(object value)
    {
        return Guid.Parse((string)value);
    }
}

/// <summary>
///     TimeSpan类型处理器
/// </summary>
internal class TimeSpanHandler : SqliteTypeHandler<TimeSpan>
{
    /// <summary>
    ///     解析TimeSpan值
    /// </summary>
    /// <param name="value">数据库值</param>
    /// <returns>TimeSpan对象</returns>
    public override TimeSpan Parse(object value)
    {
        return TimeSpan.Parse((string)value);
    }
}

/// <summary>
///     可空Uri数组类型处理器
/// </summary>
public class NullableUriArrayTypeHandler : SqlMapper.TypeHandler<Uri[]?>
{
    /// <summary>
    ///     设置Uri数组参数值
    /// </summary>
    /// <param name="parameter">数据库参数</param>
    /// <param name="value">Uri数组值</param>
    public override void SetValue(IDbDataParameter parameter, Uri[]? value)
    {
        parameter.Value = value == null ? null : string.Join('|', value.Select(uri => uri.ToString()));
    }

    /// <summary>
    ///     解析Uri数组值
    /// </summary>
    /// <param name="value">数据库值</param>
    /// <returns>Uri数组对象</returns>
    public override Uri[]? Parse(object value)
    {
        return value is not string str ? null : str.Split('|').Select(uri => new Uri(uri)).ToArray();
    }
}

/// <summary>
///     Uri数组类型处理器
/// </summary>
public class UriArrayTypeHandler : SqlMapper.TypeHandler<Uri[]>
{
    /// <summary>
    ///     设置Uri数组参数值
    /// </summary>
    /// <param name="parameter">数据库参数</param>
    /// <param name="value">Uri数组值</param>
    public override void SetValue(IDbDataParameter parameter, Uri[]? value)
    {
        parameter.Value = value == null ? null : string.Join('|', value.Select(uri => uri.ToString()));
    }

    /// <summary>
    ///     解析Uri数组值
    /// </summary>
    /// <param name="value">数据库值</param>
    /// <returns>Uri数组对象</returns>
    public override Uri[] Parse(object value)
    {
        return value is not string str ? [] :
            string.IsNullOrEmpty(str) ? [] : str.Split('|').Select(uri => new Uri(uri)).ToArray();
    }
}

public class StreamRepository
{
    static StreamRepository()
    {
        SqlMapper.AddTypeHandler(new NullableUriArrayTypeHandler());
        SqlMapper.AddTypeHandler(new UriArrayTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
    }

    public List<GetChannelData> GetAllStream()
    {
        using var connection = DatabaseHelper.GetConnection();
        return connection.Query<GetChannelData>("SELECT * FROM Stream ORDER BY StreamId DESC LIMIT 100").ToList();
    }

    public bool StreamAvailable(long start, long end)
    {
        if (start > end) return StreamAvailable(end, start);
        using var connection = DatabaseHelper.GetConnection();

        const string sql = """
                           SELECT EXISTS (
                               SELECT 1 
                               FROM Stream 
                               WHERE StreamId BETWEEN @StartStreamId AND @EndStreamId
                               LIMIT 1
                           )
                           """;

        var parameters = new { StartStreamId = start, EndStreamId = end };

        var result = connection.ExecuteScalar<int>(sql, parameters);

        return result == 1;
    }

    public bool StreamAvailable(long id)
    {
        using var connection = DatabaseHelper.GetConnection();

        const string sql = """
                           SELECT EXISTS (
                               SELECT 1 
                               FROM Stream 
                               WHERE StreamId = @Id
                               LIMIT 1
                           )
                           """;

        var parameters = new { Id = id };

        var result = connection.ExecuteScalar<int>(sql, parameters);

        return result == 1;
    }

    public LiveUser[] GetUsers()
    {
        using var connection = DatabaseHelper.GetConnection();
        return connection.Query<LiveUser>("SELECT DISTINCT UserId, Nickname FROM Stream ORDER BY UserId").ToArray();
    }

    public long GetLastStreamId()
    {
        using var connection = DatabaseHelper.GetConnection();
        return connection.QuerySingleOrDefault<long>("SELECT COALESCE(MAX(StreamId), 0) FROM Stream");
    }

    public GetChannelData[] GetValidStream()
    {
        using var connection = DatabaseHelper.GetConnection();

        const string sql =
            "SELECT * FROM Stream WHERE StartTime >= unixepoch ('now', 'start of day', '-2 month', 'subsec') * 1000 ORDER BY StreamId DESC";
        return connection.Query<GetChannelData>(sql).ToArray();
    }

    public GetChannelData[] GetStreamByUserName(string name)
    {
        using var connection = DatabaseHelper.GetConnection();
        return connection.Query<GetChannelData>(
            "SELECT * FROM Stream WHERE Nickname LIKE @Name ORDER BY StreamId DESC",
            new { Name = $"%{name}%" }).ToArray();
    }

    public GetChannelData[] GetStreamByUserId(long userId)
    {
        using var connection = DatabaseHelper.GetConnection();
        return connection.Query<GetChannelData>(
            "SELECT * FROM Stream WHERE UserId = @UserId ORDER BY StreamId DESC",
            new { UserId = userId }).ToArray();
    }

    public GetChannelData[] GetStreamByRange(long start, long end)
    {
        if (start > end) return GetStreamByRange(end, start);
        using var connection = DatabaseHelper.GetConnection();
        return connection.Query<GetChannelData>(
            "SELECT * FROM Stream WHERE StreamId BETWEEN @Start AND @End ORDER BY StreamId DESC",
            new { Start = start, End = end }).ToArray();
    }

    public GetChannelData[] GetStreamByDate(DateTimeOffset date)
    {
        using var connection = DatabaseHelper.GetConnection();
        return connection.Query<GetChannelData>(
                "SELECT * FROM Stream WHERE StartTime BETWEEN @StartDate AND @EndDate ORDER BY StreamId DESC",
                new { StartDate = date.ToUnixTimeMilliseconds(), EndDate = date.AddDays(1).ToUnixTimeMilliseconds() })
            .ToArray();
    }

    public GetChannelData? GetStreamById(long id)
    {
        using var connection = DatabaseHelper.GetConnection();
        return connection.QuerySingleOrDefault<GetChannelData>(
            "SELECT * FROM Stream WHERE StreamId = @Id",
            new { Id = id });
    }

    public void AddStream(GetChannelData item)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Execute("""
                           INSERT OR REPLACE INTO Stream (
                               Id, StreamId, StreamName, Name, Status, 
                               StreamUrls, StreamInnerUrl, CreatedTime, StartTime, EndTime,
                               ThumbUrl, StreamUrlExpiredTime, ClientSourceId, Type, Client,
                               Weight, Manage, KsUserId, Nickname, UserId,
                               SortId, PrivateLive, Property
                           ) VALUES (
                               @Id, @StreamId, @StreamName, @Name, @Status,
                               @StreamUrls, @StreamInnerUrl, @CreatedTime, @StartTime, @EndTime,
                               @ThumbUrl, @StreamUrlExpiredTime, @ClientSourceId, @Type, @Client,
                               @Weight, @Manage, @KsUserId, @Nickname, @UserId,
                               @SortId, @PrivateLive, @Property
                           )
                           """,
            item);
    }

    public void AddStream(IEnumerable<GetChannelData> items)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT OR REPLACE INTO Stream (
                                  Id, StreamId, StreamName, Name, Status, 
                                  StreamUrls, StreamInnerUrl, CreatedTime, StartTime, EndTime,
                                  ThumbUrl, StreamUrlExpiredTime, ClientSourceId, Type, Client,
                                  Weight, Manage, KsUserId, Nickname, UserId,
                                  SortId, PrivateLive, Property
                              ) VALUES (
                                  $Id, $StreamId, $StreamName, $Name, $Status,
                                  $StreamUrls, $StreamInnerUrl, $CreatedTime, $StartTime, $EndTime,
                                  $ThumbUrl, $StreamUrlExpiredTime, $ClientSourceId, $Type, $Client,
                                  $Weight, $Manage, $KsUserId, $Nickname, $UserId,
                                  $SortId, $PrivateLive, $Property
                              )
                              """;

        // Create parameters
        var idParam = command.CreateParameter();
        idParam.ParameterName = "$Id";
        command.Parameters.Add(idParam);

        var streamIdParam = command.CreateParameter();
        streamIdParam.ParameterName = "$StreamId";
        command.Parameters.Add(streamIdParam);

        var streamNameParam = command.CreateParameter();
        streamNameParam.ParameterName = "$StreamName";
        command.Parameters.Add(streamNameParam);

        var nameParam = command.CreateParameter();
        nameParam.ParameterName = "$Name";
        command.Parameters.Add(nameParam);

        var statusParam = command.CreateParameter();
        statusParam.ParameterName = "$Status";
        command.Parameters.Add(statusParam);

        var streamUrlsParam = command.CreateParameter();
        streamUrlsParam.ParameterName = "$StreamUrls";
        command.Parameters.Add(streamUrlsParam);

        var streamInnerUrlParam = command.CreateParameter();
        streamInnerUrlParam.ParameterName = "$StreamInnerUrl";
        command.Parameters.Add(streamInnerUrlParam);

        var createdTimeParam = command.CreateParameter();
        createdTimeParam.ParameterName = "$CreatedTime";
        command.Parameters.Add(createdTimeParam);

        var startTimeParam = command.CreateParameter();
        startTimeParam.ParameterName = "$StartTime";
        command.Parameters.Add(startTimeParam);

        var endTimeParam = command.CreateParameter();
        endTimeParam.ParameterName = "$EndTime";
        command.Parameters.Add(endTimeParam);

        var thumbUrlParam = command.CreateParameter();
        thumbUrlParam.ParameterName = "$ThumbUrl";
        command.Parameters.Add(thumbUrlParam);

        var streamUrlExpiredTimeParam = command.CreateParameter();
        streamUrlExpiredTimeParam.ParameterName = "$StreamUrlExpiredTime";
        command.Parameters.Add(streamUrlExpiredTimeParam);

        var clientSourceIdParam = command.CreateParameter();
        clientSourceIdParam.ParameterName = "$ClientSourceId";
        command.Parameters.Add(clientSourceIdParam);

        var typeParam = command.CreateParameter();
        typeParam.ParameterName = "$Type";
        command.Parameters.Add(typeParam);

        var clientParam = command.CreateParameter();
        clientParam.ParameterName = "$Client";
        command.Parameters.Add(clientParam);

        var weightParam = command.CreateParameter();
        weightParam.ParameterName = "$Weight";
        command.Parameters.Add(weightParam);

        var manageParam = command.CreateParameter();
        manageParam.ParameterName = "$Manage";
        command.Parameters.Add(manageParam);

        var ksUserIdParam = command.CreateParameter();
        ksUserIdParam.ParameterName = "$KsUserId";
        command.Parameters.Add(ksUserIdParam);

        var nicknameParam = command.CreateParameter();
        nicknameParam.ParameterName = "$Nickname";
        command.Parameters.Add(nicknameParam);

        var userIdParam = command.CreateParameter();
        userIdParam.ParameterName = "$UserId";
        command.Parameters.Add(userIdParam);

        var sortIdParam = command.CreateParameter();
        sortIdParam.ParameterName = "$SortId";
        command.Parameters.Add(sortIdParam);

        var privateLiveParam = command.CreateParameter();
        privateLiveParam.ParameterName = "$PrivateLive";
        command.Parameters.Add(privateLiveParam);

        var propertyParam = command.CreateParameter();
        propertyParam.ParameterName = "$Property";
        command.Parameters.Add(propertyParam);

        foreach (var item in items)
        {
            // Set parameter values
            idParam.Value = item.Id;
            streamIdParam.Value = item.StreamId;
            streamNameParam.Value = item.StreamName;
            nameParam.Value = item.Name;
            statusParam.Value = item.Status;
            streamUrlsParam.Value = item.StreamUrls == null
                ? DBNull.Value
                : string.Join('|', item.StreamUrls.Select(uri => uri.ToString()));
            streamInnerUrlParam.Value = item.StreamInnerUrl;
            createdTimeParam.Value = item.CreatedTime.ToUnixTimeMilliseconds();
            startTimeParam.Value = item.StartTime.ToUnixTimeMilliseconds();
            endTimeParam.Value = item.EndTime.ToUnixTimeMilliseconds();
            thumbUrlParam.Value = item.ThumbUrl;
            streamUrlExpiredTimeParam.Value = item.StreamUrlExpiredTime.ToUnixTimeMilliseconds();
            clientSourceIdParam.Value = item.ClientSourceId;
            typeParam.Value = item.Type;
            clientParam.Value = item.Client;
            weightParam.Value = item.Weight;
            manageParam.Value = item.Manage;
            ksUserIdParam.Value = item.KsUserId;
            nicknameParam.Value = item.Nickname;
            userIdParam.Value = item.UserId;
            sortIdParam.Value = item.SortId;
            privateLiveParam.Value = item.PrivateLive;
            propertyParam.Value = item.Property;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void UpdateStream(GetChannelData item)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Execute("""
                           UPDATE Stream SET
                               StreamUrls = @StreamUrls,
                               StreamInnerUrl = @StreamInnerUrl,
                               CreatedTime = @CreatedTime,
                               StartTime = @StartTime,
                               EndTime = @EndTime,
                               StreamUrlExpiredTime = @StreamUrlExpiredTime,
                               KsUserId = @KsUserId,
                               Nickname = @Nickname,
                               UserId = @UserId,
                               SortId = @SortId
                           WHERE StreamId = @StreamId
                           """,
            item);
    }
}

public record LiveUser(long UserId, string Nickname);