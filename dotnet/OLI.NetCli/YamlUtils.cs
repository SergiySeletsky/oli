using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class YamlUtils
{
    static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
    static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static T Read<T>(string path)
    {
        var yaml = File.ReadAllText(path);
        return Deserializer.Deserialize<T>(yaml);
    }

    public static void Write(string path, object obj)
    {
        var yaml = Serializer.Serialize(obj);
        File.WriteAllText(path, yaml);
    }
}
