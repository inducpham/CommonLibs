using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BinaryJSONSerializer : MonoBehaviour
{

    static string SEED = "V@#IXS0!@/,.ASD123";

    //public class Test
    //{
    //    public Dictionary<string, int> dict = new Dictionary<string, int>();
    //}

    //private void Start()
    //{
    //    var test = new Test();
    //    test.dict["a"] = 22;

    //    WriteToFile("", "test.bin", test);
    //    test = ReadFromFile<Test>("", "test.bin");
    //    Debug.Log(test.dict["a"]);
    //}

    public static void WriteToFile(string seed, string path, System.Object obj)
    {
        path = Path.Combine(Application.persistentDataPath, path);
        WriteToFileAbsolutePath(seed, path, obj);
    }

    public static void WriteToFileAbsolutePath(string seed, string path, System.Object obj)
    {
        path = Path.Combine(Application.persistentDataPath, path);
        var content = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        var seedbytes = StringToBytes(seed + SEED);
        var seedlen = seedbytes.Length;
        var contentbytes = StringToBytes(content);
        for (var i = 0; i < contentbytes.Length; i++)
            contentbytes[i] = (byte)(contentbytes[i] + seedbytes[i % seedlen]);
        System.IO.File.WriteAllBytes(path, contentbytes);
    }

    public static T ReadFromFile<T>(string seed, string path)
    {
        path = Path.Combine(Application.persistentDataPath, path);
        byte[] contentbytes;
        try
        {
            contentbytes = System.IO.File.ReadAllBytes(path);
        }
        catch (Exception)
        {
            return default(T);
        }

        return ReadFromBytes<T>(seed, contentbytes);
    }

    public static T ReadFromBytes<T>(string seed, Byte[] bytes)
    {
        var seedbytes = StringToBytes(seed + SEED);
        var seedlen = seedbytes.Length;
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(bytes[i] - seedbytes[i % seedlen]);

        string content = BytesToString(bytes);
        try
        {
            T result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content);
            return result;
        }
        catch
        {
            return default(T);
        }
    }

    public static string WriteToAscii(System.Object obj, string seed)
    {
        var seedbytes = StringToBytes(seed + SEED);
        var content = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        var seedlen = seedbytes.Length;
        var contentbytes = StringToBytes(content);
        for (var i = 0; i < contentbytes.Length; i++)
            contentbytes[i] = (byte)(contentbytes[i] + seedbytes[i % seedlen]);

        var readable_bytes = new byte[contentbytes.Length * 2];
        for (var i = 0; i < contentbytes.Length; i++)
        {
            var b = contentbytes[i];
            var b1 = (byte) ('a' + b / ((byte)8));
            var b2 = (byte)('0' + b % ((byte)8));
            readable_bytes[i * 2] = b1;
            readable_bytes[i * 2 + 1] = b2;
        }

        return System.Text.Encoding.ASCII.GetString(readable_bytes);
    }

    public static T ReadFromAscii<T>(string ascii, string seed)
    {
        var readable_bytes = System.Text.Encoding.ASCII.GetBytes(ascii);
        var bytes = new byte[readable_bytes.Length / 2];

        for (var i = 0; i < bytes.Length; i++)
        {
            var b1 = (byte) (readable_bytes[i * 2] - 'a');
            var b2 = (byte) (readable_bytes[i * 2 + 1] - '0');
            var b = (byte) (b1 * 8 + b2);
            bytes[i] = b;
        }

        var seedbytes = StringToBytes(seed + SEED);
        var seedlen = seedbytes.Length;
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(bytes[i] - seedbytes[i % seedlen]);

        string content = BytesToString(bytes);
        try
        {
            T result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content);
            return result;
        }
        catch
        {
            return default(T);
        }
    }

    static byte[] StringToBytes(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        return bytes;
    }

    static string BytesToString(byte[] bytes)
    {
        var result = System.Text.Encoding.UTF8.GetString(bytes);
        return result;
    }
}
