﻿using JNPF.Schedule;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JNPF.Common.Core.Job;

/// <summary>
/// 常量、公共方法配置类.
/// </summary>
internal static class Penetrates
{
    /// <summary>
    /// 获取默认的序列化对象.
    /// </summary>
    /// <returns><see cref="JsonSerializerOptions"/>.</returns>
    internal static JsonSerializerOptions GetDefaultJsonSerializerOptions()
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            AllowTrailingCommas = true
        };

        // 处理时间类型
        jsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());

        return jsonSerializerOptions;
    }

    /// <summary>
    /// 序列化对象.
    /// </summary>
    /// <param name="obj">对象.</param>
    /// <returns><see cref="string"/></returns>
    internal static string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, GetDefaultJsonSerializerOptions());
    }

    /// <summary>
    /// 反序列化对象.
    /// </summary>
    /// <param name="json">JSON 字符串.</param>
    /// <returns>T</returns>
    internal static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, GetDefaultJsonSerializerOptions());
    }

    /// <summary>
    /// 获取当前时间.
    /// </summary>
    /// <param name="useUtcTimestamp">是否使用 UTC 时间.</param>
    /// <returns><see cref="DateTime"/></returns>
    internal static DateTime GetNowTime(bool useUtcTimestamp)
    {
        return GetUnspecifiedTime(useUtcTimestamp ? DateTime.UtcNow : DateTime.Now);
    }

    /// <summary>
    /// 转换时间为 Unspecified 格式.
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/>.</param>
    /// <returns><see cref="DateTime"/></returns>
    internal static DateTime GetUnspecifiedTime(DateTime dateTime)
    {
        // 采用 DateTimeKind.Unspecified 转换当前时间并忽略毫秒之后部分（用于减少误差）
        return new DateTime(dateTime.Year,
            dateTime.Month,
            dateTime.Day,
            dateTime.Hour,
            dateTime.Minute,
            dateTime.Second,
            dateTime.Millisecond);
    }

    /// <summary>
    /// 将属性名切割成多个单词.
    /// </summary>
    /// <param name="propertyName">属性名.</param>
    /// <returns>单词数组.</returns>
    internal static string[] SplitToWords(string propertyName)
    {
        // 空检查
        if (propertyName == null) return Array.Empty<string>();

        // 处理包含空白问题
        if (string.IsNullOrWhiteSpace(propertyName)) return new string[] { propertyName };
        if (propertyName.Length == 1) return new string[] { propertyName };

        return Regex.Split(propertyName, @"(?=\p{Lu}\p{Ll})|(?<=\p{Ll})(?=\p{Lu})")
            .Where(u => u.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// 设置字符串首字母大/小写.
    /// </summary>
    /// <param name="str">字符串</param>
    /// <param name="isUpper">是否大写</param>
    /// <returns><see cref="string"/></returns>
    internal static string SetFirstLetterCase(string str, bool isUpper = true)
    {
        // 空检查
        if (string.IsNullOrWhiteSpace(str)) return str;

        // 获取首个字母
        var firstLetter = str.First().ToString();

        // 拼接最终返回字符串
        return string.Concat(
            isUpper
            ? firstLetter.ToUpper()
            : firstLetter.ToLower(), str.AsSpan(1));
    }

    /// <summary>
    /// 根据属性名获取指定的命名法.
    /// </summary>
    /// <param name="propertyName">属性名.</param>
    /// <param name="naming">命名法.</param>
    /// <returns><see cref="bool"/></returns>
    internal static string GetNaming(string propertyName, NamingConventions naming = NamingConventions.CamelCase)
    {
        var words = SplitToWords(propertyName);
        var tempWords = new List<string>();

        // 遍历每一个单词
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            switch (naming)
            {
                // 第一个单词首字母小写
                case NamingConventions.CamelCase:
                    tempWords.Add(SetFirstLetterCase(word, i != 0));
                    continue;

                // 每一个单词首字母大写
                case NamingConventions.Pascal:
                    tempWords.Add(SetFirstLetterCase(word));
                    continue;

                // 每次单词使用下划线连接且首字母都是小写
                case NamingConventions.UnderScoreCase:
                    tempWords.Add(SetFirstLetterCase(word, false));
                    continue;
            }
        }

        return string.Join(naming == NamingConventions.UnderScoreCase ? "_" : string.Empty, tempWords);
    }

    /// <summary>
    /// 高性能创建 JSON 字符串.
    /// </summary>
    /// <param name="writeAction"><see cref="Utf8JsonWriter"/></param>
    /// <returns><see cref="string"/></returns>
    internal static string Write(Action<Utf8JsonWriter> writeAction)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            // 解决中文乱码问题
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        writeAction?.Invoke(writer);

        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// 获取 JsonElement 实际的值.
    /// </summary>
    /// <param name="value">对象值</param>
    /// <returns><see cref="object"/></returns>
    internal static object GetJsonElementValue(object value)
    {
        if (value == null || value is not JsonElement ele) return value;

        // 处理 Array 类型的值
        if (ele.ValueKind == JsonValueKind.Array)
        {
            var arrEle = ele.EnumerateArray();
            var length = ele.GetArrayLength();
            var arr = new object[length];

            var i = 0;
            foreach (var item in arrEle)
            {
                // 递归处理
                arr[i] = GetJsonElementValue(item);
                i++;
            }

            return arr;
        }

        // 处理单个值
        object actValue = ele.ValueKind switch
        {
            JsonValueKind.String => ele.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => default,
            JsonValueKind.Number => ele.TryGetInt32(out var num) ? num : ele.GetInt64(),
            _ => throw new ArgumentException("Only int, long, string, boolean and null types or array types constructed by them are supported.")
        };

        // 处理 long 类型问题
        if (actValue is long longValue
            && longValue >= int.MinValue
            && longValue <= int.MaxValue)
        {
            actValue = (int)longValue;
        }

        return actValue;
    }

    /// <summary>
    /// 加载程序集.
    /// </summary>
    /// <param name="assemblyName">程序集名称.</param>
    /// <returns><see cref="Assembly"/></returns>
    internal static Assembly LoadAssembly(string assemblyName)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(ass => ass.GetName().Name == assemblyName);
        return assembly ?? Assembly.Load(assemblyName);
    }
}